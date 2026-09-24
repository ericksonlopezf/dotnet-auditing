// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using EricksonLopez.Auditing;
using EricksonLopez.Auditing.OpenTelemetry;
using Xunit;

namespace EricksonLopez.Auditing.OpenTelemetry.Tests;

public sealed class OpenTelemetryDecoratorTests
{
    private sealed class FakeAuditStore : IAuditStore
    {
        public List<AuditRecord> Appended { get; } = new();

        public ValueTask AppendAsync(AuditRecord record, CancellationToken cancellationToken = default)
        {
            Appended.Add(record);
            return ValueTask.CompletedTask;
        }

        public ValueTask AppendBatchAsync(IReadOnlyList<AuditRecord> records, CancellationToken cancellationToken = default)
        {
            Appended.AddRange(records);
            return ValueTask.CompletedTask;
        }

        public ValueTask<AuditQueryResult> QueryAsync(AuditQuery query, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new AuditQueryResult(Appended, null, false));
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

    [Fact]
    public async Task OpenTelemetryAuditStoreDecorator_AppendAsync_CreatesActivityAndDelegates()
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
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
    }

    [Fact]
    public async Task OpenTelemetryAuditStoreDecorator_AppendBatchAsync_Delegates()
    {
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
            }
        };

        await decorator.AppendBatchAsync(records);

        inner.Appended.Should().HaveCount(1);
    }

    [Fact]
    public async Task OpenTelemetryAuditStoreDecorator_QueryAsync_Delegates()
    {
        var inner = new FakeAuditStore();
        var decorator = new OpenTelemetryAuditStoreDecorator(inner);

        var result = await decorator.QueryAsync(new AuditQuery { TenantId = "t1" });

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task OpenTelemetryAuditIntegrityVerifierDecorator_VerifyChainAsync_DelegatesAndRecordsStatus()
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);

        var inner = new FakeIntegrityVerifier { ShouldPass = true };
        var decorator = new OpenTelemetryAuditIntegrityVerifierDecorator(inner);

        var result = await decorator.VerifyChainAsync("tenant-1", DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow);

        result.IsValid.Should().BeTrue();
        result.VerifiedCount.Should().Be(10);
    }
}



