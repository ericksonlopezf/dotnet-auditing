// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using EricksonLopez.Auditing;
using EricksonLopez.Auditing.OpenTelemetry;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EricksonLopez.Auditing.OpenTelemetry.Tests;

public sealed class OpenTelemetryDecoratorTests
{
    public class FakeAuditStore : IAuditStore
    {
        public List<AuditRecord> Appended { get; } = new();
        public bool ShouldThrowOnAppend { get; set; }
        public bool ShouldThrowOnAppendBatch { get; set; }
        public bool ShouldThrowOnQuery { get; set; }

        public ValueTask AppendAsync(AuditRecord record, CancellationToken cancellationToken = default)
        {
            if (ShouldThrowOnAppend)
            {
                throw new InvalidOperationException("Store append failed");
            }

            Appended.Add(record);
            return ValueTask.CompletedTask;
        }

        public ValueTask AppendBatchAsync(IReadOnlyList<AuditRecord> records, CancellationToken cancellationToken = default)
        {
            if (ShouldThrowOnAppendBatch)
            {
                throw new InvalidOperationException("Store append batch failed");
            }

            Appended.AddRange(records);
            return ValueTask.CompletedTask;
        }

        public ValueTask<AuditQueryResult> QueryAsync(AuditQuery query, CancellationToken cancellationToken = default)
        {
            if (ShouldThrowOnQuery)
            {
                throw new InvalidOperationException("Store query failed");
            }

            return ValueTask.FromResult(new AuditQueryResult(Appended, null, false));
        }
    }

    private sealed class FakeIntegrityVerifier : IAuditIntegrityVerifier
    {
        public bool ShouldPass { get; set; } = true;

        public ValueTask<AuditIntegrityVerificationResult> VerifyChainAsync(
            string tenantId,
            DateTimeOffset from,
            DateTimeOffset until,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(new AuditIntegrityVerificationResult(
                IsValid: ShouldPass,
                VerifiedCount: 10,
                FirstFailedRecordId: ShouldPass ? null : Guid.NewGuid(),
                FailureReason: ShouldPass ? null : "Tampered"));
        }
    }

    private sealed class FakeAuditBuilder : IAuditBuilder
    {
        public IServiceCollection Services { get; } = new ServiceCollection();

        public IAuditBuilder UseActorProvider<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TProvider>()
            where TProvider : class, IAuditActorProvider => this;

        public IAuditBuilder EnableIntegrityChain() => this;

        public IAuditBuilder UseStore<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TStore>()
            where TStore : class, IAuditStore => this;
    }

    private sealed class MetricMeasurement
    {
        public string Name { get; init; } = "";
        public object Value { get; init; } = 0;
        public Dictionary<string, object?> Tags { get; init; } = new();
    }

    private static (MeterListener Listener, List<MetricMeasurement> Measurements) StartMeterListener()
    {
        var measurements = new List<MetricMeasurement>();
        var listener = new MeterListener();
        listener.InstrumentPublished = (inst, l) =>
        {
            if (inst.Meter.Name == "EricksonLopez.Auditing")
            {
                l.EnableMeasurementEvents(inst);
            }
        };
        listener.SetMeasurementEventCallback<long>((inst, val, tags, state) =>
        {
            var dict = new Dictionary<string, object?>();
            foreach (var t in tags)
            {
                dict[t.Key] = t.Value;
            }
            lock (measurements)
            {
                measurements.Add(new MetricMeasurement { Name = inst.Name, Value = val, Tags = dict });
            }
        });
        listener.SetMeasurementEventCallback<double>((inst, val, tags, state) =>
        {
            var dict = new Dictionary<string, object?>();
            foreach (var t in tags)
            {
                dict[t.Key] = t.Value;
            }
            lock (measurements)
            {
                measurements.Add(new MetricMeasurement { Name = inst.Name, Value = val, Tags = dict });
            }
        });
        listener.Start();
        return (listener, measurements);
    }

    [Fact]
    public void OpenTelemetryAuditStoreDecorator_Constructor_NullInnerStore_ThrowsArgumentNullException()
    {
        Action act = () => _ = new OpenTelemetryAuditStoreDecorator(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("innerStore");
    }

    [Fact]
    public async Task OpenTelemetryAuditStoreDecorator_AppendAsync_NullRecord_ThrowsArgumentNullException()
    {
        var decorator = new OpenTelemetryAuditStoreDecorator(new FakeAuditStore());
        Func<Task> act = async () => await decorator.AppendAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("record");
    }

    [Fact]
    public async Task OpenTelemetryAuditStoreDecorator_AppendAsync_CreatesActivityAndDelegates()
    {
        var (meterListener, measurements) = StartMeterListener();
        using (meterListener)
        {
            Activity? capturedActivity = null;
            using var listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == "EricksonLopez.Auditing",
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped = act => capturedActivity = act
            };
            ActivitySource.AddActivityListener(listener);

            var inner = new FakeAuditStore();
            var decorator = new OpenTelemetryAuditStoreDecorator(inner);

            var record = new AuditRecord
            {
                Id = Guid.NewGuid(),
                OccurredAt = DateTimeOffset.UtcNow,
                Actor = new AuditActor(AuditActorType.User, "usr-1", "Bob"),
                Action = AuditAction.Create,
                Resource = new AuditResource("Document", "doc-1"),
                Outcome = AuditOutcome.Success,
                Context = new AuditContext("tenant-a", "App")
            };

            await decorator.AppendAsync(record);

            inner.Appended.Should().ContainSingle();
            inner.Appended[0].Id.Should().Be(record.Id);
            capturedActivity.Should().NotBeNull();
            capturedActivity!.OperationName.Should().Be("Audit.Append");
            capturedActivity.GetTagItem(AuditActivitySource.Tags.RecordId).Should().Be(record.Id.ToString());
            capturedActivity.GetTagItem(AuditActivitySource.Tags.TenantId).Should().Be("tenant-a");

            var metric = measurements.Should().Contain(m => m.Name == "audit.records_appended").Which;
            metric.Value.Should().Be(1L);
            metric.Tags[AuditActivitySource.Tags.TenantId].Should().Be("tenant-a");
            metric.Tags["audit.outcome"].Should().Be("Success");

            measurements.Should().Contain(m => m.Name == "audit.append.duration_ms");
        }
    }

    [Fact]
    public async Task OpenTelemetryAuditStoreDecorator_AppendAsync_WhenExceptionThrown_SetsActivityErrorAndThrows()
    {
        var (meterListener, measurements) = StartMeterListener();
        using (meterListener)
        {
            Activity? capturedActivity = null;
            using var listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == "EricksonLopez.Auditing",
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped = act =>
                {
                    if (act.OperationName == "Audit.Append")
                    {
                        capturedActivity = act;
                    }
                }
            };
            ActivitySource.AddActivityListener(listener);

            var inner = new FakeAuditStore { ShouldThrowOnAppend = true };
            var decorator = new OpenTelemetryAuditStoreDecorator(inner);

            var record = new AuditRecord
            {
                Id = Guid.NewGuid(),
                OccurredAt = DateTimeOffset.UtcNow,
                Actor = new AuditActor(AuditActorType.User, "usr-1"),
                Action = AuditAction.Create,
                Resource = new AuditResource("Doc", "1"),
                Outcome = AuditOutcome.Failure,
                Context = new AuditContext("tenant-err", "App")
            };

            Func<Task> act = async () => await decorator.AppendAsync(record);
            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Store append failed");

            capturedActivity.Should().NotBeNull();
            capturedActivity!.Status.Should().Be(ActivityStatusCode.Error);
            capturedActivity.StatusDescription.Should().Be("Store append failed");

            var metric = measurements.Should().Contain(m => m.Name == "audit.records_failed").Which;
            metric.Value.Should().Be(1L);
            metric.Tags[AuditActivitySource.Tags.TenantId].Should().Be("tenant-err");
            metric.Tags["audit.outcome"].Should().Be("Failure");

            measurements.Should().Contain(m => m.Name == "audit.append.duration_ms");
        }
    }

    [Fact]
    public async Task OpenTelemetryAuditStoreDecorator_AppendBatchAsync_NullRecords_ThrowsArgumentNullException()
    {
        var decorator = new OpenTelemetryAuditStoreDecorator(new FakeAuditStore());
        Func<Task> act = async () => await decorator.AppendBatchAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("records");
    }

    [Fact]
    public async Task OpenTelemetryAuditStoreDecorator_AppendBatchAsync_EmptyRecords_ReturnsImmediately()
    {
        var inner = new FakeAuditStore();
        var decorator = new OpenTelemetryAuditStoreDecorator(inner);

        await decorator.AppendBatchAsync(Array.Empty<AuditRecord>());

        inner.Appended.Should().BeEmpty();
    }

    [Fact]
    public async Task OpenTelemetryAuditStoreDecorator_AppendBatchAsync_DelegatesAndSetsBatchTags()
    {
        var (meterListener, measurements) = StartMeterListener();
        using (meterListener)
        {
            Activity? capturedActivity = null;
            using var listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == "EricksonLopez.Auditing",
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped = act => capturedActivity = act
            };
            ActivitySource.AddActivityListener(listener);

            var inner = new FakeAuditStore();
            var decorator = new OpenTelemetryAuditStoreDecorator(inner);

            var records = new[]
            {
                new AuditRecord
                {
                    Id = Guid.NewGuid(),
                    OccurredAt = DateTimeOffset.UtcNow,
                    Actor = new AuditActor(AuditActorType.User, "usr-1"),
                    Action = AuditAction.Create,
                    Resource = new AuditResource("Doc", "1"),
                    Outcome = AuditOutcome.Success,
                    Context = new AuditContext("t1", "App")
                },
                new AuditRecord
                {
                    Id = Guid.NewGuid(),
                    OccurredAt = DateTimeOffset.UtcNow,
                    Actor = new AuditActor(AuditActorType.User, "usr-2"),
                    Action = AuditAction.Update,
                    Resource = new AuditResource("Doc", "2"),
                    Outcome = AuditOutcome.Success,
                    Context = new AuditContext("t1", "App")
                }
            };

            await decorator.AppendBatchAsync(records);

            inner.Appended.Should().HaveCount(2);
            capturedActivity.Should().NotBeNull();
            capturedActivity!.OperationName.Should().Be("Audit.AppendBatch");
            capturedActivity.GetTagItem(AuditActivitySource.Tags.TenantId).Should().Be("t1");
            capturedActivity.GetTagItem("audit.batch_size").Should().Be(2);

            var metric = measurements.Should().Contain(m => m.Name == "audit.records_appended").Which;
            metric.Value.Should().Be(2L);
            metric.Tags[AuditActivitySource.Tags.TenantId].Should().Be("t1");

            measurements.Should().Contain(m => m.Name == "audit.append.duration_ms");
        }
    }

    [Fact]
    public async Task OpenTelemetryAuditStoreDecorator_AppendBatchAsync_WhenExceptionThrown_SetsActivityErrorAndThrows()
    {
        var (meterListener, measurements) = StartMeterListener();
        using (meterListener)
        {
            Activity? capturedActivity = null;
            using var listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == "EricksonLopez.Auditing",
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped = act => capturedActivity = act
            };
            ActivitySource.AddActivityListener(listener);

            var inner = new FakeAuditStore { ShouldThrowOnAppendBatch = true };
            var decorator = new OpenTelemetryAuditStoreDecorator(inner);

            var records = new[]
            {
                new AuditRecord
                {
                    Id = Guid.NewGuid(),
                    OccurredAt = DateTimeOffset.UtcNow,
                    Actor = new AuditActor(AuditActorType.User, "usr-1"),
                    Action = AuditAction.Create,
                    Resource = new AuditResource("Doc", "1"),
                    Outcome = AuditOutcome.Success,
                    Context = new AuditContext("t1", "App")
                }
            };

            Func<Task> act = async () => await decorator.AppendBatchAsync(records);
            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Store append batch failed");

            capturedActivity.Should().NotBeNull();
            capturedActivity!.Status.Should().Be(ActivityStatusCode.Error);
            capturedActivity.StatusDescription.Should().Be("Store append batch failed");

            var metric = measurements.Should().Contain(m => m.Name == "audit.records_failed").Which;
            metric.Value.Should().Be(1L);
            metric.Tags[AuditActivitySource.Tags.TenantId].Should().Be("t1");

            measurements.Should().Contain(m => m.Name == "audit.append.duration_ms");
        }
    }

    [Fact]
    public async Task OpenTelemetryAuditStoreDecorator_QueryAsync_NullQuery_ThrowsArgumentNullException()
    {
        var decorator = new OpenTelemetryAuditStoreDecorator(new FakeAuditStore());
        Func<Task> act = async () => await decorator.QueryAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("query");
    }

    [Fact]
    public async Task OpenTelemetryAuditStoreDecorator_QueryAsync_DelegatesAndSetsTenantTag()
    {
        var (meterListener, measurements) = StartMeterListener();
        using (meterListener)
        {
            Activity? capturedActivity = null;
            using var listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == "EricksonLopez.Auditing",
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped = act => capturedActivity = act
            };
            ActivitySource.AddActivityListener(listener);

            var inner = new FakeAuditStore();
            var decorator = new OpenTelemetryAuditStoreDecorator(inner);

            var result = await decorator.QueryAsync(new AuditQuery { TenantId = "t1" });

            result.Should().NotBeNull();
            capturedActivity.Should().NotBeNull();
            capturedActivity!.OperationName.Should().Be("Audit.Query");
            capturedActivity.GetTagItem(AuditActivitySource.Tags.TenantId).Should().Be("t1");

            var metric = measurements.Should().Contain(m => m.Name == "audit.queries_executed").Which;
            metric.Value.Should().Be(1L);
            metric.Tags[AuditActivitySource.Tags.TenantId].Should().Be("t1");

            measurements.Should().Contain(m => m.Name == "audit.query.duration_ms");
        }
    }

    [Fact]
    public async Task OpenTelemetryAuditStoreDecorator_QueryAsync_WhenExceptionThrown_SetsActivityErrorAndThrows()
    {
        var (meterListener, measurements) = StartMeterListener();
        using (meterListener)
        {
            Activity? capturedActivity = null;
            using var listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == "EricksonLopez.Auditing",
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped = act => capturedActivity = act
            };
            ActivitySource.AddActivityListener(listener);

            var inner = new FakeAuditStore { ShouldThrowOnQuery = true };
            var decorator = new OpenTelemetryAuditStoreDecorator(inner);

            Func<Task> act = async () => await decorator.QueryAsync(new AuditQuery { TenantId = "t-fail" });
            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Store query failed");

            capturedActivity.Should().NotBeNull();
            capturedActivity!.Status.Should().Be(ActivityStatusCode.Error);
            capturedActivity.StatusDescription.Should().Be("Store query failed");

            measurements.Should().Contain(m => m.Name == "audit.query.duration_ms");
        }
    }

    [Fact]
    public void OpenTelemetryAuditIntegrityVerifierDecorator_Constructor_NullInner_ThrowsArgumentNullException()
    {
        Action act = () => _ = new OpenTelemetryAuditIntegrityVerifierDecorator(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("innerVerifier");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task OpenTelemetryAuditIntegrityVerifierDecorator_VerifyChainAsync_DelegatesAndRecordsStatus(bool shouldPass)
    {
        var (meterListener, measurements) = StartMeterListener();
        using (meterListener)
        {
            Activity? capturedActivity = null;
            using var listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == "EricksonLopez.Auditing",
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped = act => capturedActivity = act
            };
            ActivitySource.AddActivityListener(listener);

            var inner = new FakeIntegrityVerifier { ShouldPass = shouldPass };
            var decorator = new OpenTelemetryAuditIntegrityVerifierDecorator(inner);

            var result = await decorator.VerifyChainAsync("tenant-1", DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow);

            result.IsValid.Should().Be(shouldPass);
            result.VerifiedCount.Should().Be(10);
            capturedActivity.Should().NotBeNull();
            capturedActivity!.OperationName.Should().Be("Audit.VerifyChain");
            capturedActivity.GetTagItem(AuditActivitySource.Tags.TenantId).Should().Be("tenant-1");

            var metric = measurements.Should().Contain(m => m.Name == "audit.integrity_verifications").Which;
            metric.Value.Should().Be(1L);
            metric.Tags[AuditActivitySource.Tags.TenantId].Should().Be("tenant-1");
            metric.Tags["audit.verification_valid"].Should().Be(shouldPass);
        }
    }

    [Fact]
    public void AddOpenTelemetryInstrumentation_NullBuilder_ThrowsArgumentNullException()
    {
        IAuditBuilder builder = null!;
        Action act = () => builder.AddOpenTelemetryInstrumentation();
        act.Should().Throw<ArgumentNullException>().WithParameterName("builder");
    }

    [Fact]
    public void AddOpenTelemetryInstrumentation_NoAuditStoreRegistered_DoesNotThrow()
    {
        var builder = new FakeAuditBuilder();
        var returned = builder.AddOpenTelemetryInstrumentation();
        returned.Should().BeSameAs(builder);
        builder.Services.Should().BeEmpty();
    }

    [Fact]
    public void AddOpenTelemetryInstrumentation_WithImplementationInstance_WrapsWithDecorator()
    {
        var builder = new FakeAuditBuilder();
        var rawStore = new FakeAuditStore();
        builder.Services.AddSingleton<IAuditStore>(rawStore);

        builder.AddOpenTelemetryInstrumentation();

        using var sp = builder.Services.BuildServiceProvider();
        var resolved = sp.GetRequiredService<IAuditStore>();
        resolved.Should().BeOfType<OpenTelemetryAuditStoreDecorator>();
    }

    [Fact]
    public void AddOpenTelemetryInstrumentation_WithImplementationFactory_WrapsWithDecorator()
    {
        var builder = new FakeAuditBuilder();
        builder.Services.AddSingleton<IAuditStore>(_ => new FakeAuditStore());

        builder.AddOpenTelemetryInstrumentation();

        using var sp = builder.Services.BuildServiceProvider();
        var resolved = sp.GetRequiredService<IAuditStore>();
        resolved.Should().BeOfType<OpenTelemetryAuditStoreDecorator>();
    }

    [Fact]
    public void AddOpenTelemetryInstrumentation_WithImplementationType_WrapsWithDecorator()
    {
        var builder = new FakeAuditBuilder();
        builder.Services.AddSingleton<IAuditStore, FakeAuditStore>();

        builder.AddOpenTelemetryInstrumentation();

        using var sp = builder.Services.BuildServiceProvider();
        var resolved = sp.GetRequiredService<IAuditStore>();
        resolved.Should().BeOfType<OpenTelemetryAuditStoreDecorator>();
    }

    [Fact]
    public void AddOpenTelemetryInstrumentation_WithMultipleServices_PreservesOthersAndReplacesStore()
    {
        var builder = new FakeAuditBuilder();
        builder.Services.AddSingleton("prefix_service");
        var rawStore = new FakeAuditStore();
        builder.Services.AddSingleton<IAuditStore>(rawStore);
        builder.Services.AddSingleton(new List<string>());

        builder.AddOpenTelemetryInstrumentation();

        builder.Services.Count.Should().Be(3);
        builder.Services.Should().Contain(d => d.ServiceType == typeof(string));
        builder.Services.Should().Contain(d => d.ServiceType == typeof(List<string>));
        var storeDesc = builder.Services.Single(d => d.ServiceType == typeof(IAuditStore));
        storeDesc.ImplementationInstance.Should().BeOfType<OpenTelemetryAuditStoreDecorator>();
    }

    [Fact]
    public void AddOpenTelemetryInstrumentation_WithMultipleImplementationTypes_OnlyDecoratesLastOne()
    {
        var builder = new FakeAuditBuilder();
        builder.Services.AddSingleton<IAuditStore, FakeAuditStore>();
        builder.Services.AddSingleton<IAuditStore, AlternativeAuditStore>();

        builder.AddOpenTelemetryInstrumentation();

        builder.Services.Should().Contain(d => d.ServiceType == typeof(IAuditStore) && d.ImplementationType == typeof(FakeAuditStore));
        builder.Services.Count(d => d.ServiceType == typeof(IAuditStore) && d.ImplementationFactory != null).Should().Be(1);
    }

    public sealed class AlternativeAuditStore : FakeAuditStore { }
}
