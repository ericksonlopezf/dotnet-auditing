// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using AwesomeAssertions;
using EricksonLopez.Auditing.Testing;
using Xunit;

namespace EricksonLopez.Auditing.Abstractions.Tests;

public sealed class AuditRecordModelTests
{
    [Fact]
    public void AuditRecord_RequiredFields_MustBeSet()
    {
        var record = AuditRecordBuilder.BuildDefault();

        record.Id.Should().NotBe(Guid.Empty);
        record.OccurredAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
        record.Actor.Should().NotBeNull();
        record.Action.Code.Should().NotBeNullOrEmpty();
        record.Resource.Should().NotBeNull();
        record.Context.TenantId.Value.Should().Be("tenant-a");
    }

    [Fact]
    public void AuditAction_PredefinedActions_HaveCorrectCodes()
    {
        AuditAction.Create.Code.Should().Be("Create");
        AuditAction.Update.Code.Should().Be("Update");
        AuditAction.Delete.Code.Should().Be("Delete");
        AuditAction.Read.Code.Should().Be("Read");
        AuditAction.Approve.Code.Should().Be("Approve");
        AuditAction.Reject.Code.Should().Be("Reject");
        AuditAction.Login.Code.Should().Be("Login");
        AuditAction.Logout.Code.Should().Be("Logout");
        AuditAction.Export.Code.Should().Be("Export");
        AuditAction.Download.Code.Should().Be("Download");
        AuditAction.Send.Code.Should().Be("Send");
        AuditAction.Cancel.Code.Should().Be("Cancel");
        AuditAction.Restore.Code.Should().Be("Restore");
        AuditAction.GrantPermission.Code.Should().Be("GrantPermission");
        AuditAction.RevokePermission.Code.Should().Be("RevokePermission");
    }

    [Fact]
    public void AuditAction_CustomAction_IsSupported()
    {
        var custom = new AuditAction("ProcessPayment");
        custom.Code.Should().Be("ProcessPayment");
        custom.ToString().Should().Be("ProcessPayment");
    }

    [Fact]
    public void AuditAction_Equality_BasedOnCode()
    {
        var action1 = new AuditAction("CUSTOM");
        var action2 = new AuditAction("CUSTOM");
        var action3 = new AuditAction("OTHER");

        (action1 == action2).Should().BeTrue();
        (action1 == action3).Should().BeFalse();
        action1.Equals(action2).Should().BeTrue();
    }

    [Fact]
    public void AuditActor_PredefinedActors_AreConfigured()
    {
        AuditActor.Anonymous.Type.Should().Be(AuditActorType.Anonymous);
        AuditActor.Anonymous.Id.Should().Be("anonymous");

        AuditActor.System.Type.Should().Be(AuditActorType.SystemProcess);
        AuditActor.System.Id.Should().Be("system");
    }

    [Fact]
    public void AuditActor_WithDisplayName_PopulatesCorrectly()
    {
        var actor = new AuditActor(AuditActorType.User, "usr-42", "Alice Smith");
        actor.Type.Should().Be(AuditActorType.User);
        actor.Id.Should().Be("usr-42");
        actor.DisplayName.Should().Be("Alice Smith");
    }

    [Fact]
    public void AuditResource_WithAggregateRoot_PopulatesCorrectly()
    {
        var res = new AuditResource("InvoiceLine", "line-99", "Invoice", "inv-100");
        res.Type.Should().Be("InvoiceLine");
        res.Id.Should().Be("line-99");
        res.AggregateType.Should().Be("Invoice");
        res.AggregateId.Should().Be("inv-100");
    }

    [Fact]
    public void AuditChange_Redacted_SetsIsRedactedTrueAndValuesNull()
    {
        var change = AuditChange.Redacted("CreditCardNumber");
        change.Field.Should().Be("CreditCardNumber");
        change.OldValue.Should().BeNull();
        change.NewValue.Should().BeNull();
        change.IsRedacted.Should().BeTrue();
    }

    [Fact]
    public void AuditChange_Normal_StoresBeforeAndAfterValues()
    {
        var change = new AuditChange("Status", "Pending", "Active");
        change.Field.Should().Be("Status");
        change.OldValue.Should().Be("Pending");
        change.NewValue.Should().Be("Active");
        change.IsRedacted.Should().BeFalse();
    }

    [Fact]
    public void AuditContext_SystemTenant_HasReservedConstant()
    {
        AuditContext.SystemTenantId.Value.Should().Be("system");
    }

    [Fact]
    public void SystemAuditActorProvider_ReturnsSystemActor()
    {
        var provider = SystemAuditActorProvider.Instance;
        provider.Should().NotBeNull();
        var actor = provider.GetCurrentActor();
        actor.Should().Be(AuditActor.System);
        actor.Type.Should().Be(AuditActorType.SystemProcess);
        actor.Id.Should().Be("system");
    }

    [Fact]
    public void AuditIntegrityVerificationResult_PropertiesAreCorrect()
    {
        var failId = Guid.NewGuid();
        var result = new AuditIntegrityVerificationResult(false, 10, failId, "Tampered");
        result.IsValid.Should().BeFalse();
        result.VerifiedCount.Should().Be(10);
        result.FirstFailedRecordId.Should().Be(failId);
        result.FailureReason.Should().Be("Tampered");

        var success = new AuditIntegrityVerificationResult(true, 5);
        success.IsValid.Should().BeTrue();
        success.VerifiedCount.Should().Be(5);
        success.FirstFailedRecordId.Should().BeNull();
        success.FailureReason.Should().BeNull();
    }

    [Fact]
    public void AuditQueryResult_PropertiesAreCorrect()
    {
        var id = Guid.NewGuid();
        var nextCursor = AuditCursorToken.Create(DateTimeOffset.UtcNow, id);
        var records = new List<AuditRecord> { AuditRecordBuilder.BuildDefault() };
        var result = new AuditQueryResult(records, nextCursor, true);

        result.Records.Should().HaveCount(1);
        result.NextPageToken.Should().NotBeNull(); EricksonLopez.Auditing.AuditCursorToken.TryParse(result.NextPageToken, out _, out var parsedId).Should().BeTrue(); parsedId.Should().Be(id);
        result.HasMore.Should().BeTrue();
    }

    [Fact]
    public void TenantId_Constructor_NullOrWhitespace_Throws()
    {
        Action actNull = () => _ = new TenantId(null!);
        actNull.Should().Throw<ArgumentException>();

        Action actEmpty = () => _ = new TenantId("");
        actEmpty.Should().Throw<ArgumentException>();

        Action actWhitespace = () => _ = new TenantId("   ");
        actWhitespace.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void TenantId_ToString_And_ImplicitConversions()
    {
        TenantId t = "tenant-1";
        string s = t;
        s.Should().Be("tenant-1");
        t.ToString().Should().Be("tenant-1");
        t.Value.Should().Be("tenant-1");

        TenantId t2 = new TenantId("tenant-1");
        (t == t2).Should().BeTrue();
        (t != new TenantId("other")).Should().BeTrue();
    }

    [Fact]
    public void AuditCursorToken_TryParse_NullOrWhitespace_ReturnsFalse()
    {
        AuditCursorToken.TryParse(null, out var d1, out var id1).Should().BeFalse();
        d1.Should().Be(default);
        id1.Should().Be(Guid.Empty);

        AuditCursorToken.TryParse("", out var d2, out var id2).Should().BeFalse();
        d2.Should().Be(default);
        id2.Should().Be(Guid.Empty);

        AuditCursorToken.TryParse("   ", out var d3, out var id3).Should().BeFalse();
        d3.Should().Be(default);
        id3.Should().Be(Guid.Empty);
    }

    [Fact]
    public void AuditCursorToken_TryParse_InvalidBase64_ReturnsFalse()
    {
        AuditCursorToken.TryParse("not-base64!!", out _, out _).Should().BeFalse();
    }

    [Fact]
    public void AuditCursorToken_TryParse_InvalidPartsCount_ReturnsFalse()
    {
        var singlePart = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("onlyonepart"));
        AuditCursorToken.TryParse(singlePart, out _, out _).Should().BeFalse();

        var threeParts = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("123:456:789"));
        AuditCursorToken.TryParse(threeParts, out _, out _).Should().BeFalse();
    }

    [Fact]
    public void AuditCursorToken_TryParse_InvalidTimestampOrGuid_ReturnsFalse()
    {
        var invalidMs = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"notanumber:{Guid.NewGuid():N}"));
        AuditCursorToken.TryParse(invalidMs, out _, out _).Should().BeFalse();

        var invalidGuid = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("123456789:notaguid"));
        AuditCursorToken.TryParse(invalidGuid, out _, out _).Should().BeFalse();
    }

    [Fact]
    public void AuditContext_Constructor_NullOrWhitespaceSource_Throws()
    {
        Action actNull = () => _ = new AuditContext(new TenantId("t"), null!);
        actNull.Should().Throw<ArgumentException>()
            .WithMessage("*Source cannot be null or whitespace.*")
            .WithParameterName("Source");

        Action actEmpty = () => _ = new AuditContext(new TenantId("t"), "");
        actEmpty.Should().Throw<ArgumentException>()
            .WithMessage("*Source cannot be null or whitespace.*")
            .WithParameterName("Source");

        Action actWhitespace = () => _ = new AuditContext(new TenantId("t"), "   ");
        actWhitespace.Should().Throw<ArgumentException>()
            .WithMessage("*Source cannot be null or whitespace.*")
            .WithParameterName("Source");
    }

    [Fact]
    public void AuditContext_InitSource_NullOrWhitespace_Throws()
    {
        var ctx = new AuditContext(new TenantId("t"), "valid-source");
        Action actNull = () => _ = ctx with { Source = null! };
        actNull.Should().Throw<ArgumentException>();

        Action actEmpty = () => _ = ctx with { Source = "" };
        actEmpty.Should().Throw<ArgumentException>();

        Action actWhitespace = () => _ = ctx with { Source = "   " };
        actWhitespace.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AuditContext_AnonymizeIp_Behaviors()
    {
        AuditContext.AnonymizeIp(null).Should().BeNull();
        AuditContext.AnonymizeIp("").Should().BeNull();
        AuditContext.AnonymizeIp("   ").Should().BeNull();
        AuditContext.AnonymizeIp("not-an-ip").Should().BeNull();

        AuditContext.AnonymizeIp("192.168.1.155").Should().Be("192.168.1.0");
        AuditContext.AnonymizeIp("10.0.5.23").Should().Be("10.0.5.0");

        var v6 = "2001:0db8:85a3:0000:0000:8a2e:0370:7334";
        var anonymizedV6 = AuditContext.AnonymizeIp(v6);
        anonymizedV6.Should().NotBeNull();
        anonymizedV6.Should().Be("2001:db8:85a3::");
    }

    [Fact]
    public void AuditResource_TwoParametersConstructor_LeavesAggregateNull()
    {
        var res = new AuditResource("User", "usr-1");
        res.Type.Should().Be("User");
        res.Id.Should().Be("usr-1");
        res.AggregateType.Should().BeNull();
        res.AggregateId.Should().BeNull();
    }

    [Fact]
    public void AuditQuery_Defaults_AreConfigured()
    {
        var query = new AuditQuery { TenantId = "tenant-query" };
        query.PageSize.Should().Be(50);
        query.TenantId.Value.Should().Be("tenant-query");
        query.From.Should().BeNull();
        query.To.Should().BeNull();
        query.ActorId.Should().BeNull();
        query.ActionCode.Should().BeNull();
        query.ResourceType.Should().BeNull();
        query.ResourceId.Should().BeNull();
        query.Outcome.Should().BeNull();
        query.CorrelationId.Should().BeNull();
        query.ContinuationToken.Should().BeNull();
    }

    [Fact]
    public void HmacSha256AuditHashAlgorithm_ComputesExpectedHash()
    {
        var algo = new HmacSha256AuditHashAlgorithm();
        algo.HashLengthInBytes.Should().Be(32);
        algo.AlgorithmId.Should().Be("HMACSHA256");

        var key = new byte[32];
        var data = System.Text.Encoding.UTF8.GetBytes("test-data");
        Span<byte> destination = stackalloc byte[32];

        var bytesWritten = algo.ComputeHash(data, key, destination);
        bytesWritten.Should().Be(32);
        destination.ToArray().Should().NotEqual(new byte[32]);
    }

    [Fact]
    public void AuditActor_Validation_NullOrWhitespace_Throws()
    {
        Action actNull = () => _ = new AuditActor(AuditActorType.User, null!);
        actNull.Should().Throw<ArgumentException>()
            .WithMessage("*Actor Id cannot be null or whitespace.*")
            .WithParameterName("Id");

        Action actEmpty = () => _ = new AuditActor(AuditActorType.User, "");
        actEmpty.Should().Throw<ArgumentException>()
            .WithMessage("*Actor Id cannot be null or whitespace.*")
            .WithParameterName("Id");

        Action actWs = () => _ = new AuditActor(AuditActorType.User, "   ");
        actWs.Should().Throw<ArgumentException>()
            .WithMessage("*Actor Id cannot be null or whitespace.*")
            .WithParameterName("Id");

        var actor = new AuditActor(AuditActorType.User, "valid-id");
        Action initNull = () => _ = actor with { Id = null! };
        initNull.Should().Throw<ArgumentException>();

        Action initEmpty = () => _ = actor with { Id = "" };
        initEmpty.Should().Throw<ArgumentException>();

        Action initWs = () => _ = actor with { Id = "   " };
        initWs.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AuditResource_Validation_NullOrWhitespace_Throws()
    {
        Action actNullType = () => _ = new AuditResource(null!, "id");
        actNullType.Should().Throw<ArgumentException>()
            .WithMessage("*Resource Type cannot be null or whitespace.*")
            .WithParameterName("Type");

        Action actEmptyType = () => _ = new AuditResource("", "id");
        actEmptyType.Should().Throw<ArgumentException>()
            .WithMessage("*Resource Type cannot be null or whitespace.*")
            .WithParameterName("Type");

        Action actWsType = () => _ = new AuditResource("   ", "id");
        actWsType.Should().Throw<ArgumentException>()
            .WithMessage("*Resource Type cannot be null or whitespace.*")
            .WithParameterName("Type");

        Action actNullId = () => _ = new AuditResource("type", null!);
        actNullId.Should().Throw<ArgumentException>()
            .WithMessage("*Resource Id cannot be null or whitespace.*")
            .WithParameterName("Id");

        Action actEmptyId = () => _ = new AuditResource("type", "");
        actEmptyId.Should().Throw<ArgumentException>()
            .WithMessage("*Resource Id cannot be null or whitespace.*")
            .WithParameterName("Id");

        Action actWsId = () => _ = new AuditResource("type", "   ");
        actWsId.Should().Throw<ArgumentException>()
            .WithMessage("*Resource Id cannot be null or whitespace.*")
            .WithParameterName("Id");

        var res = new AuditResource("type", "id");
        Action initNullType = () => _ = res with { Type = null! };
        initNullType.Should().Throw<ArgumentException>();
        Action initEmptyType = () => _ = res with { Type = "" };
        initEmptyType.Should().Throw<ArgumentException>();

        Action initNullId = () => _ = res with { Id = null! };
        initNullId.Should().Throw<ArgumentException>();
        Action initEmptyId = () => _ = res with { Id = "" };
        initEmptyId.Should().Throw<ArgumentException>();
    }
}




