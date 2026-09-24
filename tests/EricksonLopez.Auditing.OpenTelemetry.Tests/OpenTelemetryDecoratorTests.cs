// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
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
    public sealed class FakeAuditStore : IAuditStore
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
    }

    [Fact]
    public async Task OpenTelemetryAuditStoreDecorator_AppendAsync_WhenExceptionThrown_SetsActivityErrorAndThrows()
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
    }

    [Fact]
    public async Task OpenTelemetryAuditStoreDecorator_AppendBatchAsync_WhenExceptionThrown_SetsActivityErrorAndThrows()
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
    }

    [Fact]
    public async Task OpenTelemetryAuditStoreDecorator_QueryAsync_WhenExceptionThrown_SetsActivityErrorAndThrows()
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
}
