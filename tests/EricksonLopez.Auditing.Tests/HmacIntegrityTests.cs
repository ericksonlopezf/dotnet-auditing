// Copyright © Erickson Lopez. MIT License.
using System;
using AwesomeAssertions;
using EricksonLopez.Auditing.Testing;
using FsCheck;
using FsCheck.Xunit;
using Xunit;

namespace EricksonLopez.Auditing.Tests;

public sealed class HmacIntegrityTests
{
    private static HmacAuditIntegrityService BuildService() =>
        new(new TestAuditIntegrityProvider(new byte[32])); // 256-bit zero key for tests

    [Fact]
    public void Constructor_NullKeyProvider_Throws()
    {
        Action act = () => _ = new HmacAuditIntegrityService(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ComputeHash_IsDeterministic()
    {
        var svc = BuildService();
        var record = AuditRecordBuilder.BuildDefault();

        var hash1 = svc.ComputeHash(record, null);
        var hash2 = svc.ComputeHash(record, null);

        hash1.Should().Be(hash2);
    }

    [Fact]
    public void ComputeHash_DifferentRecords_ProduceDifferentHashes()
    {
        var svc = BuildService();
        var r1 = AuditRecordBuilder.BuildDefault(resourceId: "res-1");
        var r2 = AuditRecordBuilder.BuildDefault(resourceId: "res-2");

        svc.ComputeHash(r1, null).Should().NotBe(svc.ComputeHash(r2, null));
    }

    [Fact]
    public void ComputeHash_DifferentPreviousHash_ProducesDifferentHash()
    {
        var svc = BuildService();
        var record = AuditRecordBuilder.BuildDefault();

        var hashA = svc.ComputeHash(record, "prev-a");
        var hashB = svc.ComputeHash(record, "prev-b");

        hashA.Should().NotBe(hashB, "previous hash is part of the chain computation");
    }

    [Fact]
    public void Verify_ValidRecord_ReturnsTrue()
    {
        var svc = BuildService();
        var record = AuditRecordBuilder.BuildDefault();
        var hash = svc.ComputeHash(record, null);

        var signedRecord = record with { IntegrityHash = hash, PreviousHash = null };

        svc.Verify(signedRecord).Should().BeTrue();
    }

    [Fact]
    public void Verify_TamperedActionCode_ReturnsFalse()
    {
        var svc = BuildService();
        var record = AuditRecordBuilder.BuildDefault();
        var hash = svc.ComputeHash(record, null);

        // Tamper: change the action code after hash was computed
        var tampered = record with
        {
            Action = AuditAction.Delete,       // original was Create
            IntegrityHash = hash,
            PreviousHash = null
        };

        svc.Verify(tampered).Should().BeFalse("tampering must be detected");
    }

    [Fact]
    public void Verify_MissingIntegrityHash_ReturnsFalse()
    {
        var svc = BuildService();
        var record = AuditRecordBuilder.BuildDefault();

        svc.Verify(record).Should().BeFalse("record without hash is unverifiable");
    }

    [Fact]
    public void ComputeHash_NullRecord_Throws()
    {
        var svc = BuildService();
        Action act = () => svc.ComputeHash(null!, null);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ComputeHash_FormatString_PreventsCollision()
    {
        var svc = BuildService();

        // Simulating mutation of pipe delimiters: |
        var r1 = new AuditRecord
        {
            Id = AuditId.NewId(),
            OccurredAt = DateTimeOffset.UtcNow,
            Actor = new AuditActor(AuditActorType.User, "b", "Alice"),
            Action = new AuditAction("Create"),
            Resource = new AuditResource("Order", "1"),
            Outcome = AuditOutcome.Success,
            Context = new AuditContext("tenant-a", "src", null)
        };

        var r2 = new AuditRecord
        {
            Id = r1.Id,
            OccurredAt = r1.OccurredAt,
            Actor = new AuditActor(AuditActorType.User, "", "Alice"),
            Action = new AuditAction("Creat"),
            Resource = new AuditResource("eOrder", "1"),
            Outcome = AuditOutcome.Success,
            Context = new AuditContext("tenant-ab", "src", null)
        };

        // Without | delimiters, both might concatenate to the same string.
        var hash1 = svc.ComputeHash(r1, null);
        var hash2 = svc.ComputeHash(r2, null);

        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void ComputeHash_ProducesExactHash()
    {
        var svc = BuildService();
        var record = new AuditRecord
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000000"),
            OccurredAt = DateTimeOffset.FromUnixTimeMilliseconds(1600000000000),
            Actor = new AuditActor(AuditActorType.User, "b", null),
            Action = new AuditAction("Create"),
            Resource = new AuditResource("Order", "1"),
            Outcome = AuditOutcome.Success,
            Context = new AuditContext("tenant-a", "src", null)
        };
        var hashWithPrev = svc.ComputeHash(record, "prev");
        hashWithPrev.Should().Be("5c9b353fb2e7a78949d1f05aad998323f17cf4aaf163ad5c7595580f3a94c9f8");

        var hashFirst = svc.ComputeHash(record, null);
        hashFirst.Should().Be("f55ec3b9d8fb3cd6b530ba3e3dfe91d5e2717442deaadd1bebb9f8719f6b4efa");
    }

    [Property(MaxTest = 100)]
    public bool HmacVerification_SucceedsForUntamperedRecords_AndFailsOnTamperedPayload(NonNull<string> tenant)
    {
        if (string.IsNullOrEmpty(tenant.Get)) return true;

        var svc = BuildService();
        var record = AuditRecordBuilder.Create().WithTenant(tenant.Get).Build();
        var hash = svc.ComputeHash(record, null);

        var valid = record with { IntegrityHash = hash, PreviousHash = null };
        var isVerifiable = svc.Verify(valid);

        var tampered = valid with { Action = new AuditAction(valid.Action.Code + "_tampered") };
        var isTamperDetected = !svc.Verify(tampered);

        return isVerifiable && isTamperDetected;
    }
}
