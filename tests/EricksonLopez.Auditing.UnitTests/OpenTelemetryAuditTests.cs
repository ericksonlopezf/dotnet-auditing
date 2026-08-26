// Copyright © Erickson Lopez. MIT License.
using System;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using AwesomeAssertions;
using EricksonLopez.Auditing;
using EricksonLopez.Auditing.OpenTelemetry;
using Xunit;

namespace EricksonLopez.Auditing.UnitTests;

public sealed class OpenTelemetryAuditTests
{
    [Fact]
    public void AuditActivitySource_ConstantsAndSource_AreConfiguredProperly()
    {
        AuditActivitySource.ActivitySourceName.Should().Be("EricksonLopez.Auditing");
        AuditActivitySource.Source.Name.Should().Be("EricksonLopez.Auditing");
        AuditActivitySource.Source.Version.Should().Be("1.0.0");
    }

    [Fact]
    public void AuditActivitySource_Tags_HaveExpectedSemanticNames()
    {
        AuditActivitySource.Tags.TenantId.Should().Be("audit.tenant_id");
        AuditActivitySource.Tags.ActionCode.Should().Be("audit.action_code");
        AuditActivitySource.Tags.ResourceType.Should().Be("audit.resource_type");
        AuditActivitySource.Tags.ResourceId.Should().Be("audit.resource_id");
        AuditActivitySource.Tags.ActorId.Should().Be("audit.actor_id");
        AuditActivitySource.Tags.ActorType.Should().Be("audit.actor_type");
        AuditActivitySource.Tags.Outcome.Should().Be("audit.outcome");
        AuditActivitySource.Tags.RecordId.Should().Be("audit.record_id");
    }

    [Fact]
    public void AuditMetrics_MeterAndCounters_AreInitializedWithDescriptions()
    {
        AuditMetrics.MeterName.Should().Be("EricksonLopez.Auditing");

        AuditMetrics.RecordsAppended.Should().NotBeNull();
        AuditMetrics.RecordsAppended.Name.Should().Be("audit.records_appended");
        AuditMetrics.RecordsAppended.Description.Should().Be("Number of audit records successfully persisted.");
        AuditMetrics.RecordsAppended.Meter.Name.Should().Be("EricksonLopez.Auditing");
        AuditMetrics.RecordsAppended.Meter.Version.Should().Be("1.0.0");

        AuditMetrics.QueriesExecuted.Should().NotBeNull();
        AuditMetrics.QueriesExecuted.Name.Should().Be("audit.queries_executed");
        AuditMetrics.QueriesExecuted.Description.Should().Be("Number of audit query operations executed.");
        AuditMetrics.QueriesExecuted.Meter.Name.Should().Be("EricksonLopez.Auditing");
        AuditMetrics.QueriesExecuted.Meter.Version.Should().Be("1.0.0");

        AuditMetrics.IntegrityVerifications.Should().NotBeNull();
        AuditMetrics.IntegrityVerifications.Name.Should().Be("audit.integrity_verifications");
        AuditMetrics.IntegrityVerifications.Description.Should().Be("Number of cryptographic audit integrity chain verifications performed.");
        AuditMetrics.IntegrityVerifications.Meter.Name.Should().Be("EricksonLopez.Auditing");
        AuditMetrics.IntegrityVerifications.Meter.Version.Should().Be("1.0.0");

        // Record metrics to verify functional execution of counters
        AuditMetrics.RecordsAppended.Add(1);
        AuditMetrics.QueriesExecuted.Add(1);
        AuditMetrics.IntegrityVerifications.Add(1);
    }

    [Fact]
    public void EnrichCurrentActivity_WithValidRecord_SetsActivityTags()
    {
        // Arrange
        using var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);

        using var activity = AuditActivitySource.Source.StartActivity("TestAudit");
        activity.Should().NotBeNull();

        var record = new AuditRecord
        {
            Id = Guid.Parse("018f9d0c-1234-7000-8000-000000000001"),
            OccurredAt = DateTimeOffset.UtcNow,
            Actor = new AuditActor(AuditActorType.User, "usr-42", "Alice"),
            Action = AuditAction.Create,
            Resource = new AuditResource("Order", "ord-101"),
            Outcome = AuditOutcome.Success,
            Context = new AuditContext("tenant-99", "CheckoutService")
        };

        // Act
        record.EnrichCurrentActivity();

        // Assert
        activity!.GetTagItem(AuditActivitySource.Tags.TenantId).Should().Be("tenant-99");
        activity.GetTagItem(AuditActivitySource.Tags.ActionCode).Should().Be("Create");
        activity.GetTagItem(AuditActivitySource.Tags.ResourceType).Should().Be("Order");
        activity.GetTagItem(AuditActivitySource.Tags.ResourceId).Should().Be("ord-101");
        activity.GetTagItem(AuditActivitySource.Tags.ActorId).Should().Be("usr-42");
        activity.GetTagItem(AuditActivitySource.Tags.ActorType).Should().Be(AuditActorType.User.ToString());
        activity.GetTagItem(AuditActivitySource.Tags.Outcome).Should().Be(AuditOutcome.Success.ToString());
        activity.GetTagItem(AuditActivitySource.Tags.RecordId).Should().Be("018f9d0c-1234-7000-8000-000000000001");
    }

    [Fact]
    public void EnrichCurrentActivity_NullRecord_WithActiveActivity_DoesNotThrowAndDoesNotSetTags()
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);

        using var activity = AuditActivitySource.Source.StartActivity("TestNullRecordActivity");
        activity.Should().NotBeNull();
        Activity.Current.Should().BeSameAs(activity);

        AuditRecord record = null!;
        Action act = () => record.EnrichCurrentActivity();
        act.Should().NotThrow();

        activity!.Tags.Should().BeEmpty();
    }

    [Fact]
    public void EnrichCurrentActivity_NoCurrentActivity_DoesNotThrow()
    {
        Activity.Current = null;
        var record = new AuditRecord
        {
            Id = Guid.NewGuid(),
            OccurredAt = DateTimeOffset.UtcNow,
            Actor = new AuditActor(AuditActorType.Service, "svc-1"),
            Action = AuditAction.Delete,
            Resource = new AuditResource("Session", "sess-1"),
            Outcome = AuditOutcome.Failure,
            Context = new AuditContext("tenant-1", "AuthService")
        };

        Action act = () => record.EnrichCurrentActivity();
        act.Should().NotThrow();
    }
}


