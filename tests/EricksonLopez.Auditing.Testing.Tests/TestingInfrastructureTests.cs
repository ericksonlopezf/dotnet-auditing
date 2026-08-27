// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using AwesomeAssertions;
using EricksonLopez.Auditing;
using EricksonLopez.Auditing.Testing;
using Xunit;

namespace EricksonLopez.Auditing.Testing.Tests;

public sealed class TestingInfrastructureTests
{
    [Fact]
    public void AuditRecordBuilder_AllFluentMethods_BuildExpectedRecord()
    {
        var id = Guid.NewGuid();
        var occurredAt = new DateTimeOffset(2026, 8, 22, 10, 30, 0, TimeSpan.Zero);
        var actor = new AuditActor(AuditActorType.Service, "svc-auth", "Auth Service");
        var action = new AuditAction("CUSTOM_ACTION");
        var resource = new AuditResource("Account", "acc-123", "User", "usr-456");

        var record = AuditRecordBuilder.Create()
            .WithId(id)
            .WithOccurredAt(occurredAt)
            .WithActor(actor)
            .WithAction(action)
            .WithResource(resource)
            .WithOutcome(AuditOutcome.Partial)
            .WithTenant("tenant-corp")
            .WithSource("PaymentGateway")
            .WithCorrelationId("corr-999")
            .WithCausationId("cause-888")
            .WithRequestId("req-777")
            .WithIpAddress("192.168.1.1")
            .WithUserAgent("CustomAgent/1.0")
            .WithErrorCode("ERR_PARTIAL")
            .WithIntegrityHash("hash-abc")
            .WithPreviousHash("hash-prev")
            .AddChange("Balance", "100", "50", false)
            .AddRedactedChange("SecretKey")
            .Build();

        record.Id.Should().Be(id);
        record.OccurredAt.Should().Be(occurredAt);
        record.Actor.Should().Be(actor);
        record.Action.Should().Be(action);
        record.Resource.Should().Be(resource);
        record.Outcome.Should().Be(AuditOutcome.Partial);
        record.Context.TenantId.Should().Be("tenant-corp");
        record.Context.Source.Should().Be("PaymentGateway");
        record.Context.CorrelationId.Should().Be("corr-999");
        record.Context.CausationId.Should().Be("cause-888");
        record.Context.RequestId.Should().Be("req-777");
        record.Context.IpAddress.Should().Be("192.168.1.1");
        record.Context.UserAgent.Should().Be("CustomAgent/1.0");
        record.ErrorCode.Should().Be("ERR_PARTIAL");
        record.IntegrityHash.Should().Be("hash-abc");
        record.PreviousHash.Should().Be("hash-prev");
        record.Changes.Should().HaveCount(2);
        record.Changes![0].Field.Should().Be("Balance");
        record.Changes[0].IsRedacted.Should().BeFalse();
        record.Changes[1].Field.Should().Be("SecretKey");
        record.Changes[1].IsRedacted.Should().BeTrue();
    }

    [Fact]
    public void AuditRecordBuilder_ConvenienceOverloads_WorkAsExpected()
    {
        var record = AuditRecordBuilder.Create()
            .WithActor(AuditActorType.ScheduledJob, "cron-cleaner", "Cleaner Job")
            .WithAction("PURGE_LOGS")
            .WithResource("AuditLog", "log-001", "Tenant", "tenant-1")
            .WithChanges(new List<AuditChange>
            {
                new("DeletedCount", "0", "50")
            })
            .Build();

        record.Actor.Type.Should().Be(AuditActorType.ScheduledJob);
        record.Actor.Id.Should().Be("cron-cleaner");
        record.Actor.DisplayName.Should().Be("Cleaner Job");
        record.Action.Code.Should().Be("PURGE_LOGS");
        record.Resource.Type.Should().Be("AuditLog");
        record.Resource.Id.Should().Be("log-001");
        record.Resource.AggregateType.Should().Be("Tenant");
        record.Resource.AggregateId.Should().Be("tenant-1");
        record.Changes.Should().HaveCount(1);
    }

    [Fact]
    public void AuditRecordBuilder_NullGuards_ThrowArgumentNullException()
    {
        var builder = AuditRecordBuilder.Create();

        Action nullActor = () => builder.WithActor(null!);
        nullActor.Should().Throw<ArgumentNullException>();

        Action nullResource = () => builder.WithResource(null!);
        nullResource.Should().Throw<ArgumentNullException>();

        Action nullTenant = () => builder.WithTenant(null!);
        nullTenant.Should().Throw<ArgumentNullException>();

        Action nullSource = () => builder.WithSource(null!);
        nullSource.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AuditRecordBuilder_DefaultValues_AreCorrect()
    {
        var before = DateTimeOffset.UtcNow.AddSeconds(-2);
        var record = AuditRecordBuilder.Create().Build();
        var after = DateTimeOffset.UtcNow.AddSeconds(2);

        record.Id.Should().NotBe(Guid.Empty);
        record.OccurredAt.Should().BeOnOrAfter(before);
        record.OccurredAt.Should().BeOnOrBefore(after);
        (record.OccurredAt.Ticks % TimeSpan.TicksPerMillisecond).Should().Be(0);

        record.Actor.Type.Should().Be(AuditActorType.User);
        record.Actor.Id.Should().Be("user-42");
        record.Actor.DisplayName.Should().Be("Alice");

        record.Action.Should().Be(AuditAction.Create);
        record.Action.Code.Should().Be(AuditAction.Create.Code);

        record.Resource.Type.Should().Be("Order");
        record.Resource.Id.Should().Be("order-1");
        record.Resource.AggregateType.Should().BeNull();
        record.Resource.AggregateId.Should().BeNull();

        record.Outcome.Should().Be(AuditOutcome.Success);
        record.ErrorCode.Should().BeNull();

        record.Context.TenantId.Should().Be("tenant-a");
        record.Context.Source.Should().Be("OrderService");
        record.Context.CorrelationId.Should().BeNull();
        record.Context.CausationId.Should().BeNull();
        record.Context.RequestId.Should().BeNull();
        record.Context.IpAddress.Should().BeNull();
        record.Context.UserAgent.Should().BeNull();

        record.Changes.Should().BeNull();
        record.IntegrityHash.Should().BeNull();
        record.PreviousHash.Should().BeNull();
    }

    [Fact]
    public void AuditRecordBuilder_BuildDefault_Parameterless_ReturnsExpectedDefaults()
    {
        var record = AuditRecordBuilder.BuildDefault();

        record.Context.TenantId.Should().Be("tenant-a");
        record.Actor.Type.Should().Be(AuditActorType.User);
        record.Actor.Id.Should().Be("user-42");
        record.Actor.DisplayName.Should().Be("Alice");
        record.Resource.Type.Should().Be("Order");
        record.Resource.Id.Should().Be("order-1");
        record.Outcome.Should().Be(AuditOutcome.Success);
        record.Context.CorrelationId.Should().BeNull();
    }

    [Fact]
    public void AuditRecordBuilder_BuildDefault_WithCustomParameters_AppliesAllOverrides()
    {
        var record = AuditRecordBuilder.BuildDefault(
            tenantId: "tenant-custom",
            actorId: "actor-custom",
            resourceType: "Invoice",
            resourceId: "inv-999",
            outcome: AuditOutcome.Failure,
            correlationId: "corr-custom");

        record.Context.TenantId.Should().Be("tenant-custom");
        record.Actor.Type.Should().Be(AuditActorType.User);
        record.Actor.Id.Should().Be("actor-custom");
        record.Actor.DisplayName.Should().Be("Alice");
        record.Resource.Type.Should().Be("Invoice");
        record.Resource.Id.Should().Be("inv-999");
        record.Outcome.Should().Be(AuditOutcome.Failure);
        record.Context.CorrelationId.Should().Be("corr-custom");
    }

    [Fact]
    public void AuditRecordBuilder_TruncatesOccurredAtToMilliseconds()
    {
        var offset = TimeSpan.FromHours(3);
        var dt = new DateTimeOffset(2026, 11, 25, 14, 30, 45, 678, offset).AddTicks(9876);

        var record = AuditRecordBuilder.Create()
            .WithOccurredAt(dt)
            .Build();

        record.OccurredAt.Year.Should().Be(2026);
        record.OccurredAt.Month.Should().Be(11);
        record.OccurredAt.Day.Should().Be(25);
        record.OccurredAt.Hour.Should().Be(14);
        record.OccurredAt.Minute.Should().Be(30);
        record.OccurredAt.Second.Should().Be(45);
        record.OccurredAt.Millisecond.Should().Be(678);
        record.OccurredAt.Offset.Should().Be(offset);
        (record.OccurredAt.Ticks % TimeSpan.TicksPerMillisecond).Should().Be(0);
    }

    [Fact]
    public void AuditRecordBuilder_WithActor_TwoParams_DisplayNameDefaultsNull()
    {
        var record = AuditRecordBuilder.Create()
            .WithActor(AuditActorType.Service, "svc-daemon")
            .Build();

        record.Actor.Type.Should().Be(AuditActorType.Service);
        record.Actor.Id.Should().Be("svc-daemon");
        record.Actor.DisplayName.Should().BeNull();
    }

    [Fact]
    public void AuditRecordBuilder_WithResource_TwoAndThreeParams_SetsExpectedProperties()
    {
        var record2 = AuditRecordBuilder.Create()
            .WithResource("Customer", "c-10")
            .Build();

        record2.Resource.Type.Should().Be("Customer");
        record2.Resource.Id.Should().Be("c-10");
        record2.Resource.AggregateType.Should().BeNull();
        record2.Resource.AggregateId.Should().BeNull();

        var record3 = AuditRecordBuilder.Create()
            .WithResource("Customer", "c-10", aggregateType: "Org")
            .Build();

        record3.Resource.AggregateType.Should().Be("Org");
        record3.Resource.AggregateId.Should().BeNull();
    }

    [Fact]
    public void AuditRecordBuilder_AddChange_And_AddRedactedChange_ConsecutiveCalls_AppendProperly()
    {
        var record = AuditRecordBuilder.Create()
            .AddChange("Email", "old@test.com", "new@test.com", false)
            .AddChange("Age", "20", "21")
            .AddRedactedChange("Password")
            .AddRedactedChange("SSN")
            .Build();

        record.Changes.Should().HaveCount(4);
        record.Changes![0].Field.Should().Be("Email");
        record.Changes[0].OldValue.Should().Be("old@test.com");
        record.Changes[0].NewValue.Should().Be("new@test.com");
        record.Changes[0].IsRedacted.Should().BeFalse();

        record.Changes[1].Field.Should().Be("Age");
        record.Changes[1].OldValue.Should().Be("20");
        record.Changes[1].NewValue.Should().Be("21");
        record.Changes[1].IsRedacted.Should().BeFalse();

        record.Changes[2].Field.Should().Be("Password");
        record.Changes[2].IsRedacted.Should().BeTrue();

        record.Changes[3].Field.Should().Be("SSN");
        record.Changes[3].IsRedacted.Should().BeTrue();
    }

    [Fact]
    public void AuditRecordBuilder_WithNullChanges_ClearsChanges()
    {
        var record = AuditRecordBuilder.Create()
            .AddChange("Field", "1", "2")
            .WithChanges(null)
            .Build();

        record.Changes.Should().BeNull();
    }

    [Fact]
    public void TestAuditIntegrityProvider_ConstructorsAndTenantOverrides()
    {
        // Default constructor
        var provider = new TestAuditIntegrityProvider();
        var defaultKey = provider.GetCurrentKey("tenant-any");
        defaultKey.ToArray().Should().Equal(TestAuditIntegrityProvider.DefaultKey);

        // Custom default key constructor
        var customKey = new byte[32];
        customKey[0] = 99;
        var customProvider = new TestAuditIntegrityProvider(customKey);
        customProvider.GetCurrentKey("tenant-x").ToArray().Should().Equal(customKey);

        // Custom constructor null check
        Action nullKey = () => _ = new TestAuditIntegrityProvider(null!);
        nullKey.Should().Throw<ArgumentNullException>();

        // Set tenant key override
        var tenantSpecificKey = new byte[32];
        tenantSpecificKey[0] = 77;
        provider.SetTenantKey("tenant-vip", tenantSpecificKey);

        provider.GetCurrentKey("tenant-vip").ToArray().Should().Equal(tenantSpecificKey);
        provider.GetCurrentKey("tenant-other").ToArray().Should().Equal(TestAuditIntegrityProvider.DefaultKey);
        provider.GetCurrentKey(string.Empty).ToArray().Should().Equal(TestAuditIntegrityProvider.DefaultKey);
    }

    [Fact]
    public void TestAuditIntegrityProvider_SetTenantKey_NullGuards()
    {
        var provider = new TestAuditIntegrityProvider();

        Action nullTenant = () => provider.SetTenantKey(null!, new byte[32]);
        nullTenant.Should().Throw<ArgumentException>();

        Action emptyTenant = () => provider.SetTenantKey("", new byte[32]);
        emptyTenant.Should().Throw<ArgumentException>();

        Action nullKey = () => provider.SetTenantKey("tenant-1", null!);
        nullKey.Should().Throw<ArgumentNullException>();
    }
}
