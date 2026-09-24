// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using AwesomeAssertions;
using Xunit;

namespace EricksonLopez.Auditing.Tests;

public sealed class AuditRecordBuilderTests
{
    [Fact]
    public void Build_Defaults_AreSetCorrectly()
    {
        var record = AuditRecordBuilder.Create().Build();

        record.Id.Should().NotBeEmpty();
        record.OccurredAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
        record.Actor.Type.Should().Be(AuditActorType.SystemProcess);
        record.Actor.Id.Should().Be("system");
        record.Actor.DisplayName.Should().Be("System");
        record.Action.Should().Be(AuditAction.Create);
        record.Resource.Type.Should().Be("General");
        record.Resource.Id.Should().Be(record.Id.ToString());
        record.Outcome.Should().Be(AuditOutcome.Success);
        record.Context.TenantId.Value.Should().Be("default");
        record.Context.Source.Should().Be("Application");
        record.Context.CorrelationId.Should().BeNull();
        record.Context.CausationId.Should().BeNull();
        record.Context.RequestId.Should().BeNull();
        record.Context.IpAddress.Should().BeNull();
        record.Context.UserAgent.Should().BeNull();
        record.ErrorCode.Should().BeNull();
        record.IntegrityHash.Should().BeNull();
        record.PreviousHash.Should().BeNull();
        record.Changes.Should().BeNull();
    }

    [Fact]
    public void WithActor_Null_ThrowsArgumentNullException()
    {
        var builder = AuditRecordBuilder.Create();
        Action act = () => builder.WithActor(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("actor");
    }

    [Fact]
    public void WithResource_Null_ThrowsArgumentNullException()
    {
        var builder = AuditRecordBuilder.Create();
        Action act = () => builder.WithResource(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("resource");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void WithTenant_NullOrEmpty_ThrowsArgumentException(string? tenantId)
    {
        var builder = AuditRecordBuilder.Create();
        Action act = () => builder.WithTenant(tenantId!);
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void WithSource_NullOrEmpty_ThrowsArgumentException(string? source)
    {
        var builder = AuditRecordBuilder.Create();
        Action act = () => builder.WithSource(source!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AllProperties_SetAndBuilt_Correctly()
    {
        var id = Guid.NewGuid();
        var occurredAt = DateTimeOffset.UtcNow.AddMinutes(-10);
        var actor = new AuditActor(AuditActorType.User, "user-1", "User One");
        var action = new AuditAction("CUSTOM_ACTION");
        var resource = new AuditResource("Document", "doc-1", "Workspace", "ws-1");

        var record = AuditRecordBuilder.Create()
            .WithId(id)
            .WithOccurredAt(occurredAt)
            .WithActor(actor)
            .WithAction(action)
            .WithResource(resource)
            .WithOutcome(AuditOutcome.Denied)
            .WithTenant("tenant-corp")
            .WithSource("PaymentGateway")
            .WithCorrelationId("corr-123")
            .WithCausationId("cause-456")
            .WithRequestId("req-789")
            .WithIpAddress("192.168.1.1")
            .WithUserAgent("Mozilla/5.0")
            .WithErrorCode("ERR_FORBIDDEN")
            .WithIntegrityHash("hash-abc", "prev-hash-xyz")
            .AddChange("FieldA", "old", "new")
            .AddRedactedChange("SecretField")
            .Build();

        record.Id.Should().Be(id);
        record.OccurredAt.Year.Should().Be(occurredAt.Year);
        record.Actor.Should().Be(actor);
        record.Action.Should().Be(action);
        record.Resource.Should().Be(resource);
        record.Outcome.Should().Be(AuditOutcome.Denied);
        record.Context.TenantId.Value.Should().Be("tenant-corp");
        record.Context.Source.Should().Be("PaymentGateway");
        record.Context.CorrelationId.Should().Be("corr-123");
        record.Context.CausationId.Should().Be("cause-456");
        record.Context.RequestId.Should().Be("req-789");
        record.Context.IpAddress.Should().Be("192.168.1.1");
        record.Context.UserAgent.Should().Be("Mozilla/5.0");
        record.ErrorCode.Should().Be("ERR_FORBIDDEN");
        record.IntegrityHash.Should().Be("hash-abc");
        record.PreviousHash.Should().Be("prev-hash-xyz");
        record.Changes.Should().NotBeNull();
        record.Changes!.Count.Should().Be(2);
        record.Changes[0].Field.Should().Be("FieldA");
        record.Changes[0].OldValue.Should().Be("old");
        record.Changes[0].NewValue.Should().Be("new");
        record.Changes[1].Field.Should().Be("SecretField");
        record.Changes[1].IsRedacted.Should().BeTrue();
    }

    [Fact]
    public void OverloadMethods_BuildProperly()
    {
        var record = AuditRecordBuilder.Create()
            .WithActor(AuditActorType.Service, "svc-account", "Svc")
            .WithAction("UPDATE")
            .WithResource("Order", "ord-1", "Store", "store-1")
            .WithPreviousHash("prev-111")
            .WithChanges(new[] { new AuditChange("Item", "A", "B") })
            .Build();

        record.Actor.Type.Should().Be(AuditActorType.Service);
        record.Actor.Id.Should().Be("svc-account");
        record.Actor.DisplayName.Should().Be("Svc");
        record.Action.Code.Should().Be("UPDATE");
        record.Resource.Type.Should().Be("Order");
        record.Resource.Id.Should().Be("ord-1");
        record.Resource.AggregateType.Should().Be("Store");
        record.Resource.AggregateId.Should().Be("store-1");
        record.PreviousHash.Should().Be("prev-111");
        record.Changes!.Count.Should().Be(1);

        var nullChangesRecord = AuditRecordBuilder.Create().WithChanges(null).Build();
        nullChangesRecord.Changes.Should().BeNull();
    }

    [Fact]
    public void AuditConfiguration_MaxStringLength_ClampingAndBehavior()
    {
        var config = new AuditConfiguration();

        // Default
        config.MaxStringLength.Should().Be(4000);

        // Setting a value less than 8192 should keep that value
        config.MaxStringLength = 2000;
        config.MaxStringLength.Should().Be(2000);

        // Setting a value exactly 8192
        config.MaxStringLength = 8192;
        config.MaxStringLength.Should().Be(8192);

        // Setting a value greater than 8192 should be clamped to 8192 (kills Math.Min vs Math.Max mutant!)
        config.MaxStringLength = 10000;
        config.MaxStringLength.Should().Be(8192);

        // Batch properties
        config.BatchChannelCapacity = 5000;
        config.BatchChannelCapacity.Should().Be(5000);

        config.BatchSize = 250;
        config.BatchSize.Should().Be(250);

        config.BatchFlushInterval = TimeSpan.FromSeconds(10);
        config.BatchFlushInterval.Should().Be(TimeSpan.FromSeconds(10));
    }
}
