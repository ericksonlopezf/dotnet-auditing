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
    public void ComputeHash_GoldenVector_FullRecord_MatchesDeterministicHash()
    {
        var record = new AuditRecord
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            OccurredAt = DateTimeOffset.FromUnixTimeMilliseconds(1700000000000),
            Actor = new AuditActor(AuditActorType.User, "user-1", "John Doe"),
            Action = AuditAction.Create,
            Resource = new AuditResource("Order", "ord-123", "Customer", "cust-456"),
            Outcome = AuditOutcome.Success,
            ErrorCode = "NONE",
            Context = new AuditContext(new TenantId("tenant-1"), "ServiceA", "corr-1", "cause-1", "req-1", "127.0.0.1", "TestAgent/1.0", "idem-1"),
            PreviousHash = "prev-hash-fixed",
            Changes = new[]
            {
                new AuditChange("Amount", "100", "200", IsRedacted: false),
                new AuditChange("Status", "Pending", "Approved", IsRedacted: true)
            }
        };

        var hash = _service.ComputeHash(record, record.PreviousHash);
        hash.Should().Be("8b2fd39f9671a7b078e5869ad7015d5faf4624440a2135c31b7f0d25e9861133");
    }

    [Fact]
    public void ComputeHash_GoldenVector_NoChanges_MatchesDeterministicHash()
    {
        var record = new AuditRecord
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            OccurredAt = DateTimeOffset.FromUnixTimeMilliseconds(1700000000000),
            Actor = new AuditActor(AuditActorType.User, "user-1", "John Doe"),
            Action = AuditAction.Create,
            Resource = new AuditResource("Order", "ord-123", "Customer", "cust-456"),
            Outcome = AuditOutcome.Success,
            ErrorCode = "NONE",
            Context = new AuditContext(new TenantId("tenant-1"), "ServiceA", "corr-1", "cause-1", "req-1", "127.0.0.1", "TestAgent/1.0", "idem-1"),
            PreviousHash = "prev-hash-fixed",
            Changes = null
        };

        var hash = _service.ComputeHash(record, record.PreviousHash);
        hash.Should().Be("f6b8967ce9a0646ac862f9417e3bd1a2a98ebc5749a8951133ce690708ca9d5e");
    }

    [Fact]
    public void ComputeHash_GoldenVector_NullChangeValues_MatchesDeterministicHash()
    {
        var record = new AuditRecord
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            OccurredAt = DateTimeOffset.FromUnixTimeMilliseconds(1700000000000),
            Actor = new AuditActor(AuditActorType.User, "user-1", "John Doe"),
            Action = AuditAction.Create,
            Resource = new AuditResource("Order", "ord-123", "Customer", "cust-456"),
            Outcome = AuditOutcome.Success,
            ErrorCode = "NONE",
            Context = new AuditContext(new TenantId("tenant-1"), "ServiceA", "corr-1", "cause-1", "req-1", "127.0.0.1", "TestAgent/1.0", "idem-1"),
            PreviousHash = "prev-hash-fixed",
            Changes = new[] { new AuditChange("Note", null, null, IsRedacted: false) }
        };

        var hash = _service.ComputeHash(record, record.PreviousHash);
        hash.Should().Be("c5dd9916ed4d9af20b52401065fa6314e885cfa59099ce1acb103979915e1371");
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

    [Fact]
    public void ComputeHash_LargePayloadExceedingStackalloc_CalculatesCorrectHashAndResizesBuffer()
    {
        var largeValue1 = new string('A', 1500);
        var largeValue2 = new string('B', 1500);
        var record = new AuditRecord
        {
            Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            OccurredAt = DateTimeOffset.FromUnixTimeMilliseconds(1700000000000),
            Actor = new AuditActor(AuditActorType.User, "user-large", "Large User"),
            Action = AuditAction.Update,
            Resource = new AuditResource("LargeResource", "res-1"),
            Outcome = AuditOutcome.Success,
            Context = new AuditContext(new TenantId("tenant-large"), "BigService"),
            PreviousHash = "prev-hash-fixed",
            Changes = new[]
            {
                new AuditChange("LargeField1", largeValue1, largeValue2, IsRedacted: false)
            }
        };

        var hash = _service.ComputeHash(record, record.PreviousHash);

        hash.Should().NotBeNullOrWhiteSpace();
        hash.Should().Be("53578ce8b5f84b16d71c151ccc3f8baf48686c5e9564483bc8bb4e9041d76468");

        var hash2 = _service.ComputeHash(record, record.PreviousHash);
        hash2.Should().Be(hash);

        var verifiedRecord = record with { IntegrityHash = hash };
        _service.Verify(verifiedRecord).Should().BeTrue();
    }

    [Fact]
    public void ComputeHash_MultipleGrowthExceedingIntermediateBuffer_CopiesAndComputesCorrectly()
    {
        var changes = new List<AuditChange>();
        for (int i = 0; i < 5; i++)
        {
            changes.Add(new AuditChange($"Field_{i:D2}", new string((char)('A' + i), 600), new string((char)('a' + i), 600), false));
        }

        var record = new AuditRecord
        {
            Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
            OccurredAt = DateTimeOffset.FromUnixTimeMilliseconds(1700000000000),
            Actor = new AuditActor(AuditActorType.User, "user-multi", "Multi User"),
            Action = AuditAction.Update,
            Resource = new AuditResource("MultiResource", "res-multi"),
            Outcome = AuditOutcome.Success,
            Context = new AuditContext(new TenantId("tenant-multi"), "MultiService"),
            PreviousHash = "prev-hash-multi",
            Changes = changes
        };

        var hash = _service.ComputeHash(record, record.PreviousHash);
        hash.Should().Be("43a22b830565aa366eb72b2e94324a25f37d356dee9415db17bc30e1b4372ced");

        var verifiedRecord = record with { IntegrityHash = hash };
        _service.Verify(verifiedRecord).Should().BeTrue();
    }

    [Fact]
    public void ComputeHash_BoundaryCapacitySweep_EnsuresCapacityKillsMutants()
    {
        for (int len = 1; len <= 550; len++)
        {
            var baseRecord = new AuditRecord
            {
                Id = Guid.Parse("44444444-4444-4444-4444-444444444444"),
                OccurredAt = DateTimeOffset.FromUnixTimeMilliseconds(1700000000000),
                Actor = new AuditActor(AuditActorType.User, "user-sweep", new string('U', len % 50)),
                Action = AuditAction.Read,
                Resource = new AuditResource("SweepType", new string('X', len)),
                Outcome = AuditOutcome.Success,
                Context = new AuditContext(new TenantId("tenant-sweep"), "SweepService"),
                PreviousHash = len % 2 == 0 ? "prev-hash" : null,
                Changes = len % 3 == 0 ? new[] { new AuditChange("F", new string('V', len % 15), null, len % 4 == 0) } : null
            };

            var hash = _service.ComputeHash(baseRecord, baseRecord.PreviousHash);
            hash.Length.Should().Be(64);
        }
    }
}
