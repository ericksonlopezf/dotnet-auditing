// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using AwesomeAssertions;
using EricksonLopez.Auditing.Testing;
using Xunit;

namespace EricksonLopez.Auditing.UnitTests;

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
        record.Context.TenantId.Should().Be("tenant-a");
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
        AuditContext.SystemTenantId.Should().Be("system");
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
        var nextCursor = Guid.NewGuid();
        var records = new List<AuditRecord> { AuditRecordBuilder.BuildDefault() };
        var result = new AuditQueryResult(records, nextCursor, true);

        result.Records.Should().HaveCount(1);
        result.NextCursorId.Should().Be(nextCursor);
        result.HasMore.Should().BeTrue();
    }
}

