// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Linq;
using AwesomeAssertions;
using Xunit;

namespace EricksonLopez.Auditing.UnitTests;

public sealed class AuditSensitivityPipelineTests
{
    private static AuditSensitivityPipeline BuildPipeline(Action<AuditConfiguration>? configure = null)
    {
        var config = new AuditConfiguration();
        configure?.Invoke(config);
        return new AuditSensitivityPipeline(config);
    }

    [Theory]
    [InlineData("Password")]
    [InlineData("PasswordHash")]
    [InlineData("Token")]
    [InlineData("AccessToken")]
    [InlineData("RefreshToken")]
    [InlineData("Secret")]
    [InlineData("ApiKey")]
    [InlineData("ClientSecret")]
    [InlineData("PrivateKey")]
    [InlineData("CreditCardNumber")]
    [InlineData("Cvv")]
    [InlineData("Ssn")]
    [InlineData("Pin")]
    [InlineData("PasswordSalt")]
    [InlineData("ApiSecret")]
    [InlineData("Certificate")]
    [InlineData("SecurityAnswer")]
    public void GlobalDenylist_SuppressesSensitiveFields(string sensitiveField)
    {
        var pipeline = BuildPipeline();
        var changes = new List<AuditChange>
        {
            new(sensitiveField, "old-value", "new-value")
        };

        var result = pipeline.Apply(changes);

        result.Should().BeNull(
            $"field '{sensitiveField}' must be excluded by the global denylist and result in null when all changes are excluded");
    }

    [Theory]
    [InlineData("password")]
    [InlineData("PASSWORD")]
    [InlineData("pAsSwOrD")]
    [InlineData("apikey")]
    [InlineData("APIKEY")]
    [InlineData("secret")]
    [InlineData("SECRET")]
    [InlineData("creditcardnumber")]
    [InlineData("CREDITCARDNUMBER")]
    public void GlobalDenylist_IsCaseInsensitive_SuppressesVariants(string fieldVariant)
    {
        var pipeline = BuildPipeline();
        var changes = new List<AuditChange>
        {
            new(fieldVariant, "old-secret", "new-secret")
        };

        var result = pipeline.Apply(changes);

        result.Should().BeNull(
            $"field variant '{fieldVariant}' must be excluded case-insensitively and return null");
    }

    [Fact]
    public void Pipeline_NonSensitiveField_IsPassedThrough()
    {
        var pipeline = BuildPipeline();
        var changes = new List<AuditChange>
        {
            new("Status", "Pending", "Approved")
        };

        var result = pipeline.Apply(changes);

        result.Should().BeSameAs(changes, "when no changes are filtered or redacted, the original list reference is returned directly");
        result.Should().HaveCount(1);
        result![0].Field.Should().Be("Status");
        result[0].OldValue.Should().Be("Pending");
        result[0].NewValue.Should().Be("Approved");
    }

    [Fact]
    public void Pipeline_FirstNormalSecondDenylisted_PreservesFirstItem()
    {
        var pipeline = BuildPipeline();
        var changes = new List<AuditChange>
        {
            new("Status", "Pending", "Approved"),
            new("Password", "secret-old", "secret-new")
        };

        var result = pipeline.Apply(changes);

        result.Should().NotBeNull();
        result.Should().NotBeSameAs(changes);
        result!.Count.Should().Be(1);
        result[0].Field.Should().Be("Status");
    }

    [Fact]
    public void Pipeline_MixedFields_FiltersSensitiveOnly()
    {
        var pipeline = BuildPipeline();
        var changes = new List<AuditChange>
        {
            new("Status", "Pending", "Approved"),
            new("Password", "old", "new"),         // sensitive — excluded
            new("TotalAmount", "100", "150")
        };

        var result = pipeline.Apply(changes);

        result.Should().NotBeNull();
        result!.Select(c => c.Field).Should().NotContain("Password");
        result.Select(c => c.Field).Should().Contain("Status");
        result.Select(c => c.Field).Should().Contain("TotalAmount");
    }

    [Fact]
    public void Pipeline_AlreadyRedactedChange_IsPreservedAsRedacted()
    {
        var pipeline = BuildPipeline();
        var changes = new List<AuditChange>
        {
            AuditChange.Redacted("CardLastFour")
        };

        var result = pipeline.Apply(changes);

        result.Should().HaveCount(1);
        result![0].IsRedacted.Should().BeTrue();
        result[0].OldValue.Should().BeNull();
        result[0].NewValue.Should().BeNull();
    }

    [Fact]
    public void Pipeline_FirstNormalSecondRedacted_BuildsResultCorrectly()
    {
        var pipeline = BuildPipeline();
        var changes = new List<AuditChange>
        {
            new("NormalField", "old", "new"),
            new("RedactedField", "secret-old", "secret-new", IsRedacted: true),
            new("AnotherNormal", "v1", "v2")
        };

        var result = pipeline.Apply(changes);

        result.Should().NotBeNull();
        result!.Count.Should().Be(3);
        result[0].Field.Should().Be("NormalField");
        result[0].OldValue.Should().Be("old");
        result[1].Field.Should().Be("RedactedField");
        result[1].IsRedacted.Should().BeTrue();
        result[1].OldValue.Should().BeNull();
        result[2].Field.Should().Be("AnotherNormal");
    }

    [Fact]
    public void Pipeline_NullChanges_ReturnsNull()
    {
        var pipeline = BuildPipeline();
        pipeline.Apply(null).Should().BeNull();
    }

    [Fact]
    public void Pipeline_EmptyChanges_ReturnsEmpty()
    {
        var pipeline = BuildPipeline();
        var empty = Array.Empty<AuditChange>();
        var result = pipeline.Apply(empty);
        result.Should().BeSameAs(empty);
        result.Should().BeEmpty();
    }

    [Fact]
    public void Pipeline_CustomDenylistField_IsExcluded()
    {
        var pipeline = BuildPipeline(cfg =>
        {
            cfg.GlobalFieldDenylist.Add("InternalTaxId");
        });

        var changes = new List<AuditChange>
        {
            new("InternalTaxId", "old-tax", "new-tax"),
            new("Name", "old-name", "new-name")
        };

        var result = pipeline.Apply(changes);

        result.Should().HaveCount(1);
        result![0].Field.Should().Be("Name");
    }

    [Fact]
    public void HashValue_ProducesDeterministicOutput()
    {
        var hash1 = AuditSensitivityPipeline.HashValue("secret-value");
        var hash2 = AuditSensitivityPipeline.HashValue("secret-value");

        hash1.Should().Be(hash2);
        hash1.Should().HaveLength(64, "SHA-256 produces 256 bits = 64 hex chars");
        // Exact lower-case hex SHA-256 of "test"
        AuditSensitivityPipeline.HashValue("test")
            .Should().Be("9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08");
    }

    [Fact]
    public void HashValue_DifferentInputs_ProduceDifferentHashes()
    {
        var hash1 = AuditSensitivityPipeline.HashValue("value-a");
        var hash2 = AuditSensitivityPipeline.HashValue("value-b");

        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void HashValue_NullValue_Throws()
    {
        Action act = () => AuditSensitivityPipeline.HashValue(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("value");
    }

    [Fact]
    public void Constructor_NullConfig_Throws()
    {
        Action act = () => _ = new AuditSensitivityPipeline(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("config");
    }

    private static readonly string[] s_expectedCriticalActionCodes =
    [
        AuditAction.Login.Code,
        AuditAction.Delete.Code,
        AuditAction.GrantPermission.Code,
        AuditAction.RevokePermission.Code
    ];

    private static readonly string[] s_expectedGlobalDenylist =
    [
        "Password",
        "PasswordHash",
        "PasswordSalt",
        "Token",
        "AccessToken",
        "RefreshToken",
        "Secret",
        "ApiKey",
        "ApiSecret",
        "ClientSecret",
        "PrivateKey",
        "Certificate",
        "CreditCardNumber",
        "Cvv",
        "Ssn",
        "Pin",
        "SecurityAnswer"
    ];

    [Fact]
    public void AuditConfiguration_DefaultValues_AreExpected()
    {
        var config = new AuditConfiguration();
        config.DefaultFailureBehavior.Should().Be(AuditFailureBehavior.FailClosed);
        config.EnableIntegrityChain.Should().BeFalse();
        config.BatchChannelCapacity.Should().Be(1000);
        config.BatchSize.Should().Be(100);
        config.BatchFlushInterval.Should().Be(TimeSpan.FromSeconds(5));

        config.CriticalActionCodes.Should().BeEquivalentTo(s_expectedCriticalActionCodes);
        config.CriticalActionCodes.Contains("login").Should().BeTrue();
        config.CriticalActionCodes.Contains("LOGIN").Should().BeTrue();
        config.CriticalActionCodes.Contains("delete").Should().BeTrue();
        config.CriticalActionCodes.Contains("DELETE").Should().BeTrue();

        config.GlobalFieldDenylist.Should().BeEquivalentTo(s_expectedGlobalDenylist);
        config.GlobalFieldDenylist.Contains("password").Should().BeTrue();
        config.GlobalFieldDenylist.Contains("PASSWORD").Should().BeTrue();
        config.GlobalFieldDenylist.Contains("token").Should().BeTrue();
        config.GlobalFieldDenylist.Contains("TOKEN").Should().BeTrue();

        // Verify property setters/getters
        config.BatchChannelCapacity = 2000;
        config.BatchChannelCapacity.Should().Be(2000);
        config.BatchSize = 250;
        config.BatchSize.Should().Be(250);
        config.BatchFlushInterval = TimeSpan.FromSeconds(10);
        config.BatchFlushInterval.Should().Be(TimeSpan.FromSeconds(10));
        config.DefaultFailureBehavior = AuditFailureBehavior.FailOpen;
        config.DefaultFailureBehavior.Should().Be(AuditFailureBehavior.FailOpen);

        // Verify Enum values
        ((int)AuditFailureBehavior.FailClosed).Should().Be(1);
        ((int)AuditFailureBehavior.FailOpen).Should().Be(2);
        ((int)AuditFailureBehavior.Deferred).Should().Be(3);

        ((int)AuditFieldSensitivity.Include).Should().Be(0);
        ((int)AuditFieldSensitivity.Exclude).Should().Be(1);
        ((int)AuditFieldSensitivity.Redact).Should().Be(2);
        ((int)AuditFieldSensitivity.Hash).Should().Be(3);
    }
}
