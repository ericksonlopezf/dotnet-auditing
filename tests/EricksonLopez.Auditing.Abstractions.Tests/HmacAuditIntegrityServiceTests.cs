// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using AwesomeAssertions;
using EricksonLopez.Auditing.Testing;
using Xunit;

namespace EricksonLopez.Auditing.Abstractions.Tests;

public sealed class HmacAuditIntegrityServiceTests
{
    private readonly TestAuditIntegrityProvider _keyProvider = new();
    private readonly HmacSha256AuditHashAlgorithm _algorithm = new();
    private readonly HmacAuditIntegrityService _service;

    public HmacAuditIntegrityServiceTests()
    {
        _service = new HmacAuditIntegrityService(_keyProvider, _algorithm);
    }

    [Fact]
    public void Constructor_NullKeyProvider_ThrowsArgumentNullException()
    {
        var act = () => new HmacAuditIntegrityService(null!, _algorithm);
        act.Should().Throw<ArgumentNullException>().WithParameterName("keyProvider");
    }

    [Fact]
    public void Constructor_NullHashAlgorithm_ThrowsArgumentNullException()
    {
        var act = () => new HmacAuditIntegrityService(_keyProvider, null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("hashAlgorithm");
    }

    [Fact]
    public void ComputeHash_NullRecord_ThrowsArgumentNullException()
    {
        var act = () => _service.ComputeHash(null!, null);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ComputeHash_DefaultRecord_ReturnsValid64CharLowerHex()
    {
        var record = AuditRecordBuilder.BuildDefault();

        var hash = _service.ComputeHash(record, null);

        hash.Should().NotBeNullOrWhiteSpace();
        hash.Length.Should().Be(64);
        hash.Should().MatchRegex("^[0-9a-f]{64}$");
    }

    [Fact]
    public void ComputeHash_DifferentPreviousHash_ProducesDifferentHash()
    {
        var record = AuditRecordBuilder.BuildDefault();

        var hash1 = _service.ComputeHash(record, null);
        var hash2 = _service.ComputeHash(record, "prev-hash-1234567890");

        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void ComputeHash_WithNullAndEmptyChanges_ProducesIdenticalHash()
    {
        var baseRecord = AuditRecordBuilder.BuildDefault();
        var recordWithNull = baseRecord with { Changes = null };
        var recordWithEmpty = baseRecord with { Changes = new List<AuditChange>() };

        var hashNull = _service.ComputeHash(recordWithNull, null);
        var hashEmpty = _service.ComputeHash(recordWithEmpty, null);

        hashNull.Should().Be(hashEmpty);
    }

    [Fact]
    public void ComputeHash_WithRedactedChanges_DifferentiatesBooleanFlag()
    {
        var baseRecord = AuditRecordBuilder.BuildDefault();
        var changeUnredacted = new AuditChange("Email", "old@test.com", "new@test.com", IsRedacted: false);
        var changeRedacted = new AuditChange("Email", "old@test.com", "new@test.com", IsRedacted: true);

        var recordUnredacted = baseRecord with { Changes = new[] { changeUnredacted } };
        var recordRedacted = baseRecord with { Changes = new[] { changeRedacted } };

        var hashUnredacted = _service.ComputeHash(recordUnredacted, null);
        var hashRedacted = _service.ComputeHash(recordRedacted, null);

        hashUnredacted.Should().NotBe(hashRedacted);
    }

    [Fact]
    public void ComputeHash_UnorderedChanges_ProducesDeterministicSortedHash()
    {
        var baseRecord = AuditRecordBuilder.BuildDefault();
        var changeA = new AuditChange("Address", "Old Address", "New Address", IsRedacted: false);
        var changeB = new AuditChange("Billing", "Old Card", "New Card", IsRedacted: true);
        var changeC = new AuditChange("Customer", "Old Name", "New Name", IsRedacted: false);

        var record1 = baseRecord with { Changes = new[] { changeC, changeA, changeB } };
        var record2 = baseRecord with { Changes = new[] { changeB, changeC, changeA } };

        var hash1 = _service.ComputeHash(record1, null);
        var hash2 = _service.ComputeHash(record2, null);

        hash1.Should().Be(hash2);
    }

    [Fact]
    public void ComputeHash_ChangesWithNullValues_SerializesNullFieldProperly()
    {
        var changeNulls = new AuditChange("Status", null, null, IsRedacted: false);
        var record = AuditRecordBuilder.BuildDefault() with { Changes = new[] { changeNulls } };

        var hash = _service.ComputeHash(record, null);
        hash.Should().NotBeNullOrWhiteSpace();
        hash.Length.Should().Be(64);
    }

    [Fact]
    public void ComputeHash_LargePayload_ExercisesCharBufferExpansionAndBytePoolRental()
    {
        // Canonical string exceeding 512 chars and UTF-8 max byte count > 1024,
        // then even larger to force multiple CharBuffer reallocations returning previous rented buffer.
        string largeUserAgent = new string('A', 800);
        string largeOldValue = new string('B', 1200);
        string largeNewValue = new string('C', 1200);

        var largeChange = new AuditChange("LargeField", largeOldValue, largeNewValue, IsRedacted: false);
        var record = AuditRecordBuilder.BuildDefault() with
        {
            Context = new AuditContext("tenant-a", "Source")
            {
                UserAgent = largeUserAgent,
                IpAddress = "192.168.1.100",
                CorrelationId = "corr-12345"
            },
            Changes = new[] { largeChange }
        };

        var hash = _service.ComputeHash(record, "prev-large-hash-value-123");

        hash.Should().NotBeNullOrWhiteSpace();
        hash.Length.Should().Be(64);
        hash.Should().MatchRegex("^[0-9a-f]{64}$");
    }

    [Fact]
    public void ComputeHash_ExactBoundaryLength_TriggersBytePoolRental()
    {
        // 341 chars * 3 + 3 = 1026 > 1024, which triggers ArrayPool<byte>.Shared.Rent
        string boundaryString = new string('X', 280);
        var record = AuditRecordBuilder.BuildDefault() with
        {
            Context = new AuditContext("tenant-a", "Source")
            {
                UserAgent = boundaryString
            }
        };

        var hash = _service.ComputeHash(record, null);
        hash.Length.Should().Be(64);
    }

    [Fact]
    public void Verify_NullIntegrityHash_ReturnsFalse()
    {
        var record = AuditRecordBuilder.BuildDefault() with { IntegrityHash = null };

        var result = _service.Verify(record);

        result.Should().BeFalse();
    }

    [Fact]
    public void Verify_InvalidLengthIntegrityHash_ReturnsFalse()
    {
        var record = AuditRecordBuilder.BuildDefault() with { IntegrityHash = "invalid_short_hash" };

        var result = _service.Verify(record);

        result.Should().BeFalse();
    }

    [Fact]
    public void Verify_ValidHash_ReturnsTrue()
    {
        var record = AuditRecordBuilder.BuildDefault();
        var computedHash = _service.ComputeHash(record, record.PreviousHash);
        var recordWithHash = record with { IntegrityHash = computedHash };

        var result = _service.Verify(recordWithHash);

        result.Should().BeTrue();
    }

    [Fact]
    public void Verify_TamperedIntegrityHashWithSameLength_ReturnsFalse()
    {
        var record = AuditRecordBuilder.BuildDefault();
        var computedHash = _service.ComputeHash(record, record.PreviousHash);
        // Flip one character in the 64-char hash
        char flipped = computedHash[0] == 'a' ? 'b' : 'a';
        string tamperedHash = flipped + computedHash.Substring(1);

        var recordWithHash = record with { IntegrityHash = tamperedHash };

        var result = _service.Verify(recordWithHash);

        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("ActorId")]
    [InlineData("ActorName")]
    [InlineData("Action")]
    [InlineData("ResourceType")]
    [InlineData("ResourceId")]
    [InlineData("AggregateType")]
    [InlineData("AggregateId")]
    [InlineData("Outcome")]
    [InlineData("ErrorCode")]
    [InlineData("CorrelationId")]
    [InlineData("CausationId")]
    [InlineData("RequestId")]
    [InlineData("IpAddress")]
    [InlineData("UserAgent")]
    [InlineData("PreviousHash")]
    public void Verify_TamperedRecordField_ReturnsFalse(string fieldToTamper)
    {
        var baseRecord = new AuditRecord
        {
            Id = Guid.NewGuid(),
            OccurredAt = DateTimeOffset.UtcNow,
            Actor = new AuditActor(AuditActorType.User, "user-1", "Original User"),
            Action = AuditAction.Update,
            Resource = new AuditResource("Document", "doc-1", "Folder", "folder-1"),
            Outcome = AuditOutcome.Success,
            ErrorCode = "NONE",
            Context = new AuditContext("tenant-a", "WebApp")
            {
                CorrelationId = "corr-1",
                CausationId = "cause-1",
                RequestId = "req-1",
                IpAddress = "10.0.0.1",
                UserAgent = "Browser/1.0"
            },
            PreviousHash = "prev-hash-999"
        };

        var validHash = _service.ComputeHash(baseRecord, baseRecord.PreviousHash);
        var recordWithHash = baseRecord with { IntegrityHash = validHash };

        AuditRecord tamperedRecord = fieldToTamper switch
        {
            "ActorId" => recordWithHash with { Actor = recordWithHash.Actor with { Id = "tampered-user" } },
            "ActorName" => recordWithHash with { Actor = recordWithHash.Actor with { DisplayName = "Tampered Name" } },
            "Action" => recordWithHash with { Action = AuditAction.Delete },
            "ResourceType" => recordWithHash with { Resource = recordWithHash.Resource with { Type = "TamperedType" } },
            "ResourceId" => recordWithHash with { Resource = recordWithHash.Resource with { Id = "tampered-id" } },
            "AggregateType" => recordWithHash with { Resource = recordWithHash.Resource with { AggregateType = "TamperedAgg" } },
            "AggregateId" => recordWithHash with { Resource = recordWithHash.Resource with { AggregateId = "tampered-agg-id" } },
            "Outcome" => recordWithHash with { Outcome = AuditOutcome.Failure },
            "ErrorCode" => recordWithHash with { ErrorCode = "ERR_TAMPERED" },
            "CorrelationId" => recordWithHash with { Context = recordWithHash.Context with { CorrelationId = "tampered-corr" } },
            "CausationId" => recordWithHash with { Context = recordWithHash.Context with { CausationId = "tampered-cause" } },
            "RequestId" => recordWithHash with { Context = recordWithHash.Context with { RequestId = "tampered-req" } },
            "IpAddress" => recordWithHash with { Context = recordWithHash.Context with { IpAddress = "192.168.0.1" } },
            "UserAgent" => recordWithHash with { Context = recordWithHash.Context with { UserAgent = "TamperedAgent" } },
            "PreviousHash" => recordWithHash with { PreviousHash = "tampered-prev-hash" },
            _ => throw new ArgumentOutOfRangeException(nameof(fieldToTamper))
        };

        var result = _service.Verify(tamperedRecord);

        result.Should().BeFalse();
    }
}
