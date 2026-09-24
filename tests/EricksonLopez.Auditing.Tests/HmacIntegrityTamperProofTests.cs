// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using AwesomeAssertions;
using EricksonLopez.Auditing.Testing;
using Xunit;

namespace EricksonLopez.Auditing.Tests;

public sealed class HmacIntegrityTamperProofTests
{
    private static HmacAuditIntegrityService BuildService() =>
        new(new TestAuditIntegrityProvider(new byte[32]), new HmacSha256AuditHashAlgorithm());

    [Fact]
    public void Verify_ValidRecord_ReturnsTrue()
    {
        var svc = BuildService();
        var record = AuditRecordBuilder.Create()
            .WithTenant("tenant-1")
            .WithActor(AuditActorType.User, "user-1", "Alice")
            .WithAction("UPDATE")
            .WithResource("Account", "acc-1")
            .WithOutcome(AuditOutcome.Success)
            .WithCorrelationId("corr-123")
            .WithCausationId("cause-456")
            .WithRequestId("req-789")
            .AddChange("Balance", "100", "200")
            .Build();

        var hash = svc.ComputeHash(record, null);
        var recordWithHash = record with { IntegrityHash = hash };

        svc.Verify(recordWithHash).Should().BeTrue();
    }

    [Fact]
    public void Verify_TamperedChangesNewValue_ReturnsFalse()
    {
        var svc = BuildService();
        var record = AuditRecordBuilder.Create()
            .WithTenant("tenant-1")
            .WithActor(AuditActorType.User, "user-1", "Alice")
            .WithAction("UPDATE")
            .WithResource("Account", "acc-1")
            .WithOutcome(AuditOutcome.Success)
            .AddChange("Balance", "100", "200")
            .Build();

        var hash = svc.ComputeHash(record, null);
        var recordWithHash = record with { IntegrityHash = hash };

        // Tamper: alter change newValue
        var tamperedChanges = new List<AuditChange>
        {
            new("Balance", "100", "999999")
        };
        var tamperedRecord = recordWithHash with { Changes = tamperedChanges };

        svc.Verify(tamperedRecord).Should().BeFalse();
    }

    [Fact]
    public void Verify_TamperedChangesOldValue_ReturnsFalse()
    {
        var svc = BuildService();
        var record = AuditRecordBuilder.Create()
            .WithTenant("tenant-1")
            .WithActor(AuditActorType.User, "user-1", "Alice")
            .WithAction("UPDATE")
            .WithResource("Account", "acc-1")
            .WithOutcome(AuditOutcome.Success)
            .AddChange("Balance", "100", "200")
            .Build();

        var hash = svc.ComputeHash(record, null);
        var recordWithHash = record with { IntegrityHash = hash };

        var tamperedChanges = new List<AuditChange>
        {
            new("Balance", "0", "200")
        };
        var tamperedRecord = recordWithHash with { Changes = tamperedChanges };

        svc.Verify(tamperedRecord).Should().BeFalse();
    }

    [Fact]
    public void Verify_TamperedChangesAddedField_ReturnsFalse()
    {
        var svc = BuildService();
        var record = AuditRecordBuilder.Create()
            .WithTenant("tenant-1")
            .WithActor(AuditActorType.User, "user-1", "Alice")
            .WithAction("UPDATE")
            .WithResource("Account", "acc-1")
            .WithOutcome(AuditOutcome.Success)
            .AddChange("Balance", "100", "200")
            .Build();

        var hash = svc.ComputeHash(record, null);
        var recordWithHash = record with { IntegrityHash = hash };

        var tamperedChanges = new List<AuditChange>
        {
            new("Balance", "100", "200"),
            new("Role", null, "Admin")
        };
        var tamperedRecord = recordWithHash with { Changes = tamperedChanges };

        svc.Verify(tamperedRecord).Should().BeFalse();
    }

    [Fact]
    public void Verify_TamperedErrorCode_ReturnsFalse()
    {
        var svc = BuildService();
        var record = AuditRecordBuilder.Create()
            .WithTenant("tenant-1")
            .WithActor(AuditActorType.User, "user-1", "Alice")
            .WithAction("LOGIN")
            .WithResource("Auth", "session-1")
            .WithOutcome(AuditOutcome.Failure)
            .WithErrorCode("ERR_INVALID_PASSWORD")
            .Build();

        var hash = svc.ComputeHash(record, null);
        var recordWithHash = record with { IntegrityHash = hash };

        var tamperedRecord = recordWithHash with { ErrorCode = "ERR_ACCOUNT_LOCKED" };

        svc.Verify(tamperedRecord).Should().BeFalse();
    }

    [Fact]
    public void Verify_TamperedCorrelationId_ReturnsFalse()
    {
        var svc = BuildService();
        var record = AuditRecordBuilder.Create()
            .WithTenant("tenant-1")
            .WithActor(AuditActorType.User, "user-1", "Alice")
            .WithAction("TRANSFER")
            .WithResource("Transfer", "tx-1")
            .WithOutcome(AuditOutcome.Success)
            .WithCorrelationId("corr-original")
            .Build();

        var hash = svc.ComputeHash(record, null);
        var recordWithHash = record with { IntegrityHash = hash };

        var tamperedRecord = recordWithHash with
        {
            Context = recordWithHash.Context with { CorrelationId = "corr-forged" }
        };

        svc.Verify(tamperedRecord).Should().BeFalse();
    }

    [Fact]
    public void Verify_TamperedCausationId_ReturnsFalse()
    {
        var svc = BuildService();
        var record = AuditRecordBuilder.Create()
            .WithTenant("tenant-1")
            .WithActor(AuditActorType.User, "user-1", "Alice")
            .WithAction("TRANSFER")
            .WithResource("Transfer", "tx-1")
            .WithOutcome(AuditOutcome.Success)
            .WithCausationId("cause-original")
            .Build();

        var hash = svc.ComputeHash(record, null);
        var recordWithHash = record with { IntegrityHash = hash };

        var tamperedRecord = recordWithHash with
        {
            Context = recordWithHash.Context with { CausationId = "cause-forged" }
        };

        svc.Verify(tamperedRecord).Should().BeFalse();
    }

    [Fact]
    public void Verify_TamperedRequestId_ReturnsFalse()
    {
        var svc = BuildService();
        var record = AuditRecordBuilder.Create()
            .WithTenant("tenant-1")
            .WithActor(AuditActorType.User, "user-1", "Alice")
            .WithAction("TRANSFER")
            .WithResource("Transfer", "tx-1")
            .WithOutcome(AuditOutcome.Success)
            .WithRequestId("req-original")
            .Build();

        var hash = svc.ComputeHash(record, null);
        var recordWithHash = record with { IntegrityHash = hash };

        var tamperedRecord = recordWithHash with
        {
            Context = recordWithHash.Context with { RequestId = "req-forged" }
        };

        svc.Verify(tamperedRecord).Should().BeFalse();
    }

    [Fact]
    public void ComputeHash_ChangeOrderIndependence_ProducesDeterministicHash()
    {
        var svc = BuildService();
        var id = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var record1 = AuditRecordBuilder.Create()
            .WithId(id)
            .WithOccurredAt(now)
            .WithTenant("tenant-1")
            .WithActor(AuditActorType.User, "user-1", "Alice")
            .WithAction("UPDATE")
            .WithResource("Order", "1")
            .AddChange("Alpha", "1", "2")
            .AddChange("Beta", "3", "4")
            .Build();

        var record2 = AuditRecordBuilder.Create()
            .WithId(id)
            .WithOccurredAt(now)
            .WithTenant("tenant-1")
            .WithActor(AuditActorType.User, "user-1", "Alice")
            .WithAction("UPDATE")
            .WithResource("Order", "1")
            .AddChange("Beta", "3", "4")
            .AddChange("Alpha", "1", "2")
            .Build();

        var hash1 = svc.ComputeHash(record1, null);
        var hash2 = svc.ComputeHash(record2, null);

        hash1.Should().Be(hash2, "HMAC canonicalization sorts changes by Field ordinal to ensure canonical determinism");
    }
}




