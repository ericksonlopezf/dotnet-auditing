// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AwesomeAssertions;
using Xunit;

namespace EricksonLopez.Auditing.Tests;

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
    public async Task GlobalDenylist_SuppressesSensitiveFields(string sensitiveField)
    {
        var pipeline = BuildPipeline();
        var changes = new List<AuditChange>
        {
            new(sensitiveField, "old-value", "new-value")
        };

        var result = await pipeline.ApplyAsync(changes, "tenant-a");

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
    public async Task GlobalDenylist_IsCaseInsensitive_SuppressesVariants(string fieldVariant)
    {
        var pipeline = BuildPipeline();
        var changes = new List<AuditChange>
        {
            new(fieldVariant, "old-secret", "new-secret")
        };

        var result = await pipeline.ApplyAsync(changes, "tenant-a");

        result.Should().BeNull(
            $"field variant '{fieldVariant}' must be excluded case-insensitively and return null");
    }

    [Fact]
    public async Task Pipeline_NonSensitiveField_IsPassedThrough()
    {
        var pipeline = BuildPipeline();
        var changes = new List<AuditChange>
        {
            new("Status", "Pending", "Approved")
        };

        var result = await pipeline.ApplyAsync(changes, "tenant-a");

        result.Should().BeSameAs(changes, "when no changes are filtered or redacted, the original list reference is returned directly");
        result.Should().HaveCount(1);
        result![0].Field.Should().Be("Status");
        result[0].OldValue.Should().Be("Pending");
        result[0].NewValue.Should().Be("Approved");
    }

    [Fact]
    public async Task Pipeline_FirstNormalSecondDenylisted_PreservesFirstItem()
    {
        var pipeline = BuildPipeline();
        var changes = new List<AuditChange>
        {
            new("Status", "Pending", "Approved"),
            new("Password", "secret-old", "secret-new")
        };

        var result = await pipeline.ApplyAsync(changes, "tenant-a");

        result.Should().NotBeNull();
        result.Should().NotBeSameAs(changes);
        result!.Count.Should().Be(1);
        result[0].Field.Should().Be("Status");
    }

    [Fact]
    public async Task Pipeline_MixedFields_FiltersSensitiveOnly()
    {
        var pipeline = BuildPipeline();
        var changes = new List<AuditChange>
        {
            new("Status", "Pending", "Approved"),
            new("Password", "old", "new"),         // sensitive — excluded
            new("TotalAmount", "100", "150")
        };

        var result = await pipeline.ApplyAsync(changes, "tenant-a");

        result.Should().NotBeNull();
        result!.Select(c => c.Field).Should().NotContain("Password");
        result.Select(c => c.Field).Should().Contain("Status");
        result.Select(c => c.Field).Should().Contain("TotalAmount");
    }

    [Fact]
    public async Task Pipeline_AlreadyRedactedChange_IsPreservedAsRedacted()
    {
        var pipeline = BuildPipeline();
        var changes = new List<AuditChange>
        {
            AuditChange.Redacted("CardLastFour")
        };

        var result = await pipeline.ApplyAsync(changes, "tenant-a");

        result.Should().HaveCount(1);
        result![0].IsRedacted.Should().BeTrue();
        result[0].OldValue.Should().BeNull();
        result[0].NewValue.Should().BeNull();
    }

    [Fact]
    public async Task Pipeline_FirstNormalSecondRedacted_BuildsResultCorrectly()
    {
        var pipeline = BuildPipeline();
        var changes = new List<AuditChange>
        {
            new("NormalField", "old", "new"),
            new("RedactedField", "secret-old", "secret-new", IsRedacted: true),
            new("AnotherNormal", "v1", "v2")
        };

        var result = await pipeline.ApplyAsync(changes, "tenant-a");

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
    public async Task Pipeline_MultipleDenylistedFieldsInterleaved_ExcludesAllDenylistedWithoutReintroducingPriorItems()
    {
        var pipeline = BuildPipeline();
        var changes = new List<AuditChange>
        {
            new("Username", "old-user", "new-user"),
            new("Password", "secret1", "secret2"),
            new("Email", "old@mail.com", "new@mail.com"),
            new("ApiKey", "key1", "key2"),
            new("Role", "User", "Admin")
        };

        var result = await pipeline.ApplyAsync(changes, "tenant-a");

        result.Should().NotBeNull();
        result!.Count.Should().Be(3);
        result[0].Field.Should().Be("Username");
        result[1].Field.Should().Be("Email");
        result[2].Field.Should().Be("Role");
        result.Select(c => c.Field).Should().NotContain("Password");
        result.Select(c => c.Field).Should().NotContain("ApiKey");
    }

    [Fact]
    public async Task Pipeline_MultipleRedactedFieldsInterleaved_RedactsAllWithoutReintroducingUnredactedPriorItems()
    {
        var pipeline = BuildPipeline();
        var changes = new List<AuditChange>
        {
            new("Username", "old-user", "new-user"),
            new("CreditCard", "1111-2222", "3333-4444", IsRedacted: true),
            new("Email", "old@mail.com", "new@mail.com"),
            new("PinCode", "1234", "5678", IsRedacted: true),
            new("Role", "User", "Admin")
        };

        var result = await pipeline.ApplyAsync(changes, "tenant-a");

        result.Should().NotBeNull();
        result!.Count.Should().Be(5);
        result[0].Field.Should().Be("Username");
        result[0].OldValue.Should().Be("old-user");
        result[1].Field.Should().Be("CreditCard");
        result[1].IsRedacted.Should().BeTrue();
        result[1].OldValue.Should().BeNull();
        result[2].Field.Should().Be("Email");
        result[3].Field.Should().Be("PinCode");
        result[3].IsRedacted.Should().BeTrue();
        result[3].OldValue.Should().BeNull();
        result[4].Field.Should().Be("Role");
    }

    [Fact]
    public async Task Pipeline_MixedSensitiveAndRedactedInterleaved_ProcessesAllCorrectly()
    {
        var pipeline = BuildPipeline();
        var changes = new List<AuditChange>
        {
            new("Password", "secret", "secret2"),
            new("Username", "user1", "user2"),
            new("CreditCard", "1111", "2222", IsRedacted: true),
            new("ApiKey", "k1", "k2"),
            new("Status", "Active", "Suspended")
        };

        var result = await pipeline.ApplyAsync(changes, "tenant-a");

        result.Should().NotBeNull();
        result!.Count.Should().Be(3);
        result[0].Field.Should().Be("Username");
        result[1].Field.Should().Be("CreditCard");
        result[1].IsRedacted.Should().BeTrue();
        result[1].OldValue.Should().BeNull();
        result[2].Field.Should().Be("Status");
        result.Select(c => c.Field).Should().NotContain("Password");
        result.Select(c => c.Field).Should().NotContain("ApiKey");
    }

    [Fact]
    public async Task Pipeline_NullChanges_ReturnsNull()
    {
        var pipeline = BuildPipeline();
        var result = await pipeline.ApplyAsync(null, "tenant-a");
        result.Should().BeNull();
    }

    [Fact]
    public async Task Pipeline_EmptyChanges_ReturnsEmpty()
    {
        var pipeline = BuildPipeline();
        var empty = Array.Empty<AuditChange>();
        var result = await pipeline.ApplyAsync(empty, "tenant-a");
        result.Should().BeSameAs(empty);
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Pipeline_CustomDenylistField_IsExcluded()
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

        var result = await pipeline.ApplyAsync(changes, "tenant-a");

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

    private static readonly string[] _expectedCriticalActionCodes =
    [
        AuditAction.Login.Code,
        AuditAction.Delete.Code,
        AuditAction.GrantPermission.Code,
        AuditAction.RevokePermission.Code
    ];

    private static readonly string[] _expectedGlobalDenylist =
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

        config.CriticalActionCodes.Should().BeEquivalentTo(_expectedCriticalActionCodes);
        config.CriticalActionCodes.Contains("login").Should().BeTrue();
        config.CriticalActionCodes.Contains("LOGIN").Should().BeTrue();
        config.CriticalActionCodes.Contains("delete").Should().BeTrue();
        config.CriticalActionCodes.Contains("DELETE").Should().BeTrue();

        config.GlobalFieldDenylist.Should().BeEquivalentTo(_expectedGlobalDenylist);
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

    [Fact]
    public async Task SanitizeAsync_NullRecord_ThrowsArgumentNullException()
    {
        var pipeline = BuildPipeline();
        Func<Task> act = async () => await pipeline.SanitizeAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("record");
    }

    [Fact]
    public async Task SanitizeAsync_NullOrEmptyChanges_ReturnsSameRecordInstance()
    {
        var pipeline = BuildPipeline();
        var recordNullChanges = AuditRecordBuilder.Create().WithChanges(null).Build();
        var result1 = await pipeline.SanitizeAsync(recordNullChanges);
        result1.Should().BeSameAs(recordNullChanges);

        var recordEmptyChanges = AuditRecordBuilder.Create().WithChanges(Array.Empty<AuditChange>()).Build();
        var result2 = await pipeline.SanitizeAsync(recordEmptyChanges);
        result2.Should().BeSameAs(recordEmptyChanges);
    }

    [Fact]
    public async Task SanitizeAsync_UnmodifiedChanges_ReturnsSameRecordInstance()
    {
        var pipeline = BuildPipeline();
        var originalChanges = new List<AuditChange> { new("NormalField", "OldVal", "NewVal") };
        var record = AuditRecordBuilder.Create().WithChanges(originalChanges).Build();

        var sanitized = await pipeline.SanitizeAsync(record);
        sanitized.Should().BeSameAs(record, "when no changes are filtered or modified, the original record reference is returned");
    }

    [Fact]
    public async Task SanitizeAsync_ModifiedChanges_ReturnsRecordWithSanitizedChanges()
    {
        var pipeline = BuildPipeline();
        var originalChanges = new List<AuditChange>
        {
            new("NormalField", "OldVal", "NewVal"),
            new("Password", "secret", "newSecret")
        };
        var record = AuditRecordBuilder.Create().WithChanges(originalChanges).Build();

        var sanitized = await pipeline.SanitizeAsync(record);
        sanitized.Should().NotBeSameAs(record);
        sanitized.Changes.Should().NotBeNull();
        sanitized.Changes!.Count.Should().Be(1);
        sanitized.Changes[0].Field.Should().Be("NormalField");
    }

    [Fact]
    public async Task ApplyAsync_StringLengthExactBoundary_IsNotTruncated()
    {
        var pipeline = BuildPipeline(cfg => cfg.MaxStringLength = 10);
        var exact10 = "1234567890";
        var changes = new List<AuditChange>
        {
            new("Field1", exact10, exact10)
        };

        var result = await pipeline.ApplyAsync(changes, "tenant-a");

        result.Should().BeSameAs(changes, "a string whose length is exactly equal to MaxStringLength must NOT be truncated");
        result![0].OldValue.Should().Be(exact10);
        result[0].NewValue.Should().Be(exact10);
    }

    [Fact]
    public async Task ApplyAsync_OnlyOldValueExceedsLimit_TruncatesOldValueOnly()
    {
        var pipeline = BuildPipeline(cfg => cfg.MaxStringLength = 5);
        var changes = new List<AuditChange>
        {
            new("Field1", "123456", "123")
        };

        var result = await pipeline.ApplyAsync(changes, "tenant-a");

        result.Should().NotBeNull();
        result!.Count.Should().Be(1);
        result[0].OldValue.Should().Be("12345[TRUNCATED]");
        result[0].NewValue.Should().Be("123");
        result[0].IsRedacted.Should().BeFalse();
    }

    [Fact]
    public async Task ApplyAsync_OnlyNewValueExceedsLimit_TruncatesNewValueOnly()
    {
        var pipeline = BuildPipeline(cfg => cfg.MaxStringLength = 5);
        var changes = new List<AuditChange>
        {
            new("NormalField", "1", "2"),
            new("Field2", "123", "123456")
        };

        var result = await pipeline.ApplyAsync(changes, "tenant-a");

        result.Should().NotBeNull();
        result!.Count.Should().Be(2);
        result[0].Field.Should().Be("NormalField");
        result[1].OldValue.Should().Be("123");
        result[1].NewValue.Should().Be("12345[TRUNCATED]");
    }

    [Fact]
    public void HashValue_WithSalt_ComputesCorrectDigest()
    {
        const string val = "mySecretPassword";
        const string salt = "tenantSalt123";

        var hash = AuditSensitivityPipeline.HashValue(val, salt);

        // Verify matches SHA256 of "tenantSalt123:mySecretPassword"
        var expectedBytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"{salt}:{val}"));
        var expectedHex = Convert.ToHexString(expectedBytes).ToLowerInvariant();

        hash.Should().Be(expectedHex);
    }

    [Fact]
    public void KeyProvider_Property_ReturnsConfiguredInstance()
    {
        var keyProvider = NSubstitute.Substitute.For<IAuditCryptoKeyProvider>();
        var pipeline = new AuditSensitivityPipeline(new AuditConfiguration(), keyProvider);
        pipeline.KeyProvider.Should().BeSameAs(keyProvider);

        var pipelineDefault = new AuditSensitivityPipeline(new AuditConfiguration());
        pipelineDefault.KeyProvider.Should().BeNull();
    }

    [Fact]
    public async Task ApplyAsync_RedactedChangeFollowedByTruncatedChange_PreservesBothChanges()
    {
        var config = new AuditConfiguration
        {
            MaxStringLength = 10
        };
        var pipeline = new AuditSensitivityPipeline(config);

        var changes = new List<AuditChange>
        {
            AuditChange.Redacted("CreditCard"),
            new AuditChange("Bio", null, new string('A', 50), false)
        };

        var result = await pipeline.ApplyAsync(changes, "tenant-1");
        result.Should().NotBeNull();
        result!.Count.Should().Be(2);
        result[0].Field.Should().Be("CreditCard");
        result[0].IsRedacted.Should().BeTrue();
        result[0].NewValue.Should().BeNull();
        result[1].Field.Should().Be("Bio");
        result[1].NewValue.Should().Be("AAAAAAAAAA[TRUNCATED]");
    }

    [Fact]
    public async Task ApplyAsync_RedactedChangeFollowedByUnmodifiedChange_PreservesBothChanges()
    {
        var pipeline = new AuditSensitivityPipeline(new AuditConfiguration());
        var changes = new List<AuditChange>
        {
            AuditChange.Redacted("CreditCard"),
            new AuditChange("RegularField", "Old", "New")
        };

        var result = await pipeline.ApplyAsync(changes, "tenant-1");
        result.Should().NotBeNull();
        result!.Count.Should().Be(2);
        result[0].Field.Should().Be("CreditCard");
        result[0].IsRedacted.Should().BeTrue();
        result[1].Field.Should().Be("RegularField");
        result[1].NewValue.Should().Be("New");
    }

    [Fact]
    public async Task ApplyAsync_MultipleTruncatedChanges_PreservesFirstTruncatedChange()
    {
        var config = new AuditConfiguration { MaxStringLength = 5 };
        var pipeline = new AuditSensitivityPipeline(config);
        var changes = new List<AuditChange>
        {
            new AuditChange("F1", null, "1234567890"),
            new AuditChange("F2", null, "abcdefghij")
        };

        var result = await pipeline.ApplyAsync(changes, "tenant-1");
        result.Should().NotBeNull();
        result!.Count.Should().Be(2);
        result[0].Field.Should().Be("F1");
        result[0].NewValue.Should().Be("12345[TRUNCATED]");
        result[1].Field.Should().Be("F2");
        result[1].NewValue.Should().Be("abcde[TRUNCATED]");
    }

    [Fact]
    public async Task ApplyAsync_DenylistedChangeFollowedByTruncatedChange_ExcludesDenylistedField()
    {
        var config = new AuditConfiguration { MaxStringLength = 5 };
        var pipeline = new AuditSensitivityPipeline(config);
        var changes = new List<AuditChange>
        {
            new AuditChange("Password", "oldSecret", "newSecret"),
            new AuditChange("F2", null, "abcdefghij")
        };

        var result = await pipeline.ApplyAsync(changes, "tenant-1");
        result.Should().NotBeNull();
        result!.Count.Should().Be(1);
        result[0].Field.Should().Be("F2");
        result[0].NewValue.Should().Be("abcde[TRUNCATED]");
    }
}




