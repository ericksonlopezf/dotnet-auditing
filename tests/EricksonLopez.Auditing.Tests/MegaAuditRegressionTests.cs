// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using EricksonLopez.Auditing.Testing;
using Xunit;

namespace EricksonLopez.Auditing.Tests;

/// <summary>
/// Regression test suite covering all security, concurrency, and architecture findings
/// from the Mega-Audit.
/// </summary>
public sealed class MegaAuditRegressionTests
{
    private static HmacAuditIntegrityService BuildHmacService() =>
        new(new TestAuditIntegrityProvider(new byte[32]), new HmacSha256AuditHashAlgorithm());

    // ── SEC-01: Cryptographic Integrity of Missing Forensic Fields ───────────

    [Fact]
    public void SEC01_TamperingSource_InvalidatesHmac()
    {
        var svc = BuildHmacService();
        var record = AuditRecordBuilder.BuildDefault();
        var hash = svc.ComputeHash(record, null);
        var sealedRecord = record with { IntegrityHash = hash };

        svc.Verify(sealedRecord).Should().BeTrue();

        var tampered = sealedRecord with
        {
            Context = sealedRecord.Context with { Source = "MaliciousSource" }
        };

        svc.Verify(tampered).Should().BeFalse();
    }

    [Fact]
    public void SEC01_TamperingActorDisplayName_InvalidatesHmac()
    {
        var svc = BuildHmacService();
        var record = AuditRecordBuilder.BuildDefault();
        var hash = svc.ComputeHash(record, null);
        var sealedRecord = record with { IntegrityHash = hash };

        svc.Verify(sealedRecord).Should().BeTrue();

        var tampered = sealedRecord with
        {
            Actor = sealedRecord.Actor with { DisplayName = "ImpersonatedAdmin" }
        };

        svc.Verify(tampered).Should().BeFalse();
    }

    [Fact]
    public void SEC01_TamperingIpAddress_InvalidatesHmac()
    {
        var svc = BuildHmacService();
        var record = AuditRecordBuilder.BuildDefault();
        var hash = svc.ComputeHash(record, null);
        var sealedRecord = record with { IntegrityHash = hash };

        svc.Verify(sealedRecord).Should().BeTrue();

        var tampered = sealedRecord with
        {
            Context = sealedRecord.Context with { IpAddress = "10.0.0.99" }
        };

        svc.Verify(tampered).Should().BeFalse();
    }

    [Fact]
    public void SEC01_TamperingAggregateInfo_InvalidatesHmac()
    {
        var svc = BuildHmacService();
        var record = AuditRecordBuilder.BuildDefault();
        var hash = svc.ComputeHash(record, null);
        var sealedRecord = record with { IntegrityHash = hash };

        svc.Verify(sealedRecord).Should().BeTrue();

        var tampered = sealedRecord with
        {
            Resource = sealedRecord.Resource with { AggregateId = "Agg999" }
        };

        svc.Verify(tampered).Should().BeFalse();
    }

    // ── CON-02: Thread-Safe AuditScope Metadata Under High Concurrency ───────

    [Fact]
    public async Task CON02_ConcurrentWithMetadata_DoesNotCorruptStateOrThrow()
    {
        using var scope = AuditScope.Begin();

        const int concurrency = 50;
        var tasks = new Task[concurrency];

        for (int i = 0; i < concurrency; i++)
        {
            int index = i;
            tasks[i] = Task.Run(() =>
            {
                scope.WithMetadata($"Key_{index}", $"Value_{index}");
            });
        }

        await Task.WhenAll(tasks);

        scope.Metadata.Count.Should().Be(concurrency);
        for (int i = 0; i < concurrency; i++)
        {
            scope.Metadata.Should().ContainKey($"Key_{i}");
            scope.Metadata[$"Key_{i}"].Should().Be($"Value_{i}");
        }
    }

    // ── SEC-05: SQL Identifier Injection Rejection ───────────────────────────

    [Theory]
    [InlineData("audit; DROP TABLE audit_records;--")]
    [InlineData("records' OR '1'='1")]
    [InlineData("schema with spaces")]
    [InlineData("table-with-dashes")]
    [InlineData("123starts_with_number")]
    [InlineData("")]
    [InlineData("   ")]
    public void SEC05_InvalidSqlIdentifiers_AreRejectedByRegex(string malformedIdentifier)
    {
        var regex = new Regex("^[a-zA-Z_][a-zA-Z0-9_]*$");
        regex.IsMatch(malformedIdentifier).Should().BeFalse();
    }

    [Theory]
    [InlineData("valid_table")]
    [InlineData("AuditRecords")]
    [InlineData("_hidden_table")]
    [InlineData("table_123")]
    public void SEC05_ValidSqlIdentifiers_AreAcceptedByRegex(string validIdentifier)
    {
        var regex = new Regex("^[a-zA-Z_][a-zA-Z0-9_]*$");
        regex.IsMatch(validIdentifier).Should().BeTrue();
    }

    // ── PRV-01: Privacy IP Anonymization ─────────────────────────────────────

    [Fact]
    public void PRV01_AnonymizeIp_MasksIPv4LastOctet()
    {
        AuditContext.AnonymizeIp("192.168.1.150").Should().Be("192.168.1.0");
        AuditContext.AnonymizeIp("10.200.5.254").Should().Be("10.200.5.0");
    }

    [Fact]
    public void PRV01_AnonymizeIp_MasksIPv6ToPrefix48()
    {
        var anonymized = AuditContext.AnonymizeIp("2001:0db8:85a3:0000:0000:8a2e:0370:7334");
        anonymized.Should().NotBeNull();
        anonymized.Should().StartWith("2001:db8:85a3::");
    }

    [Fact]
    public void PRV01_AnonymizeIp_HandlesNullAndInvalidGracefully()
    {
        AuditContext.AnonymizeIp(null).Should().BeNull();
        AuditContext.AnonymizeIp("").Should().BeNull();
        AuditContext.AnonymizeIp("not-an-ip").Should().BeNull();
    }

    // ── DOC-01: High-Throughput Buffered Channel Decorator ────────────────────

    [Fact]
    public async Task DOC01_BufferedAuditStoreDecorator_BatchesAndDrainsCorrectly()
    {
        var mockStore = new InMemoryAuditStore();
        var options = new BufferedAuditStoreOptions
        {
            Capacity = 1000,
            BatchSize = 10,
            FlushInterval = TimeSpan.FromMilliseconds(50)
        };

        await using (var buffered = new BufferedAuditStoreDecorator(mockStore, options))
        {
            for (int i = 0; i < 25; i++)
            {
                var record = AuditRecordBuilder.Create()
                    .WithAction($"Action_{i}")
                    .WithTenant("tenant-a")
                    .Build();

                await buffered.AppendAsync(record);
            }

            // Buffer drains automatically upon DisposeAsync
        }

        mockStore.GetAllRecords().Count.Should().Be(25);
    }

    // ── SEC-06: Salted Hashing Support ─────────────────────────────────────────

    [Fact]
    public void SEC06_HashValue_WithSalt_ProducesDifferentDigestThanUnsalted()
    {
        const string plaintext = "secret123";
        var unsalted = AuditSensitivityPipeline.HashValue(plaintext);
        var salted = AuditSensitivityPipeline.HashValue(plaintext, "random-salt-abc");

        unsalted.Should().NotBeNullOrWhiteSpace();
        salted.Should().NotBeNullOrWhiteSpace();
        salted.Should().NotBe(unsalted);
    }

    [Fact]
    public void SEC06_HashValue_SameSalt_IsDeterministic()
    {
        const string plaintext = "secret123";
        const string salt = "tenant-salt-99";
        var hash1 = AuditSensitivityPipeline.HashValue(plaintext, salt);
        var hash2 = AuditSensitivityPipeline.HashValue(plaintext, salt);

        hash1.Should().Be(hash2);
    }

    // ── InMemory store helper for tests ──────────────────────────────────────

    private sealed class InMemoryAuditStore : IAuditStore
    {
        private readonly List<AuditRecord> _records = new();
        private readonly object _lock = new();

        public List<AuditRecord> GetAllRecords()
        {
            lock (_lock)
            {
                return new List<AuditRecord>(_records);
            }
        }

        public ValueTask AppendAsync(AuditRecord record, CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                _records.Add(record);
            }
            return ValueTask.CompletedTask;
        }

        public ValueTask AppendBatchAsync(IReadOnlyList<AuditRecord> records, CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                _records.AddRange(records);
            }
            return ValueTask.CompletedTask;
        }

        public ValueTask<AuditQueryResult> QueryAsync(AuditQuery query, CancellationToken cancellationToken = default)
        {
            lock (_lock)
            {
                var list = _records.Where(r => r.Context.TenantId == query.TenantId).ToList();
                return ValueTask.FromResult(new AuditQueryResult(list, null, false));
            }
        }
    }
}




