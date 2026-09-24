// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using EricksonLopez.Auditing.Testing;
using NSubstitute;
using Xunit;

namespace EricksonLopez.Auditing.Tests;

public sealed class IntegrityAuditStoreDecoratorTests
{
    private static HmacAuditIntegrityService CreateIntegrityService()
    {
        var keyProvider = new TestAuditIntegrityProvider(new byte[32]);
        var algorithm = new HmacSha256AuditHashAlgorithm();
        return new HmacAuditIntegrityService(keyProvider, algorithm);
    }

    private sealed class TrackingAuditStore : IAuditStore
    {
        public List<AuditRecord> AppendedRecords { get; } = new();
        public List<AuditRecord> QueryRecords { get; } = new();
        public int AppendCallCount { get; private set; }
        public int FailAppendTimes { get; set; }

        public ValueTask AppendAsync(AuditRecord record, CancellationToken cancellationToken = default)
        {
            AppendCallCount++;
            if (AppendCallCount <= FailAppendTimes)
            {
                throw new InvalidOperationException("Transient store failure");
            }
            AppendedRecords.Add(record);
            return ValueTask.CompletedTask;
        }

        public ValueTask AppendBatchAsync(IReadOnlyList<AuditRecord> records, CancellationToken cancellationToken = default)
        {
            AppendCallCount++;
            if (AppendCallCount <= FailAppendTimes)
            {
                throw new InvalidOperationException("Transient store failure");
            }
            AppendedRecords.AddRange(records);
            return ValueTask.CompletedTask;
        }

        public ValueTask<AuditQueryResult> QueryAsync(AuditQuery query, CancellationToken cancellationToken = default)
        {
            var matches = QueryRecords.Where(r => r.Context.TenantId == query.TenantId).Take(query.PageSize).ToList();
            return ValueTask.FromResult(new AuditQueryResult(matches, null, false));
        }
    }

    [Fact]
    public void Constructor_NullArguments_ThrowsArgumentNullException()
    {
        var integrityService = CreateIntegrityService();
        var store = new TrackingAuditStore();

        Action act1 = () => _ = new IntegrityAuditStoreDecorator(null!, integrityService);
        act1.Should().Throw<ArgumentNullException>().WithParameterName("innerStore");

        Action act2 = () => _ = new IntegrityAuditStoreDecorator(store, null!);
        act2.Should().Throw<ArgumentNullException>().WithParameterName("integrityService");
    }

    [Fact]
    public async Task AppendAsync_NullRecord_ThrowsArgumentNullException()
    {
        var decorator = new IntegrityAuditStoreDecorator(new TrackingAuditStore(), CreateIntegrityService());
        Func<Task> act = async () => await decorator.AppendAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("record");
    }

    [Fact]
    public async Task AppendAsync_WhenNoPreviousRecordsExist_ChainsWithNullPreviousHash()
    {
        var store = new TrackingAuditStore();
        var service = CreateIntegrityService();
        var decorator = new IntegrityAuditStoreDecorator(store, service);

        var record = AuditRecordBuilder.Create()
            .WithTenant("tenant-new")
            .WithAction("CREATE")
            .Build();

        await decorator.AppendAsync(record);

        store.AppendedRecords.Should().HaveCount(1);
        var secure = store.AppendedRecords[0];

        secure.PreviousHash.Should().BeNull();
        secure.IntegrityHash.Should().NotBeNullOrWhiteSpace();
        service.Verify(secure).Should().BeTrue();
    }

    [Fact]
    public async Task AppendAsync_WhenPreviousRecordExists_ChainsWithPreviousHash()
    {
        var store = new TrackingAuditStore();
        var service = CreateIntegrityService();
        var decorator = new IntegrityAuditStoreDecorator(store, service);

        // Populate previous record for tenant
        var prevRecord = AuditRecordBuilder.Create()
            .WithTenant("tenant-a")
            .WithAction("LOGIN")
            .WithIntegrityHash("hash-predecessor-999")
            .Build();
        store.QueryRecords.Add(prevRecord);

        var nextRecord = AuditRecordBuilder.Create()
            .WithTenant("tenant-a")
            .WithAction("UPDATE")
            .Build();

        await decorator.AppendAsync(nextRecord);

        store.AppendedRecords.Should().HaveCount(1);
        var secure = store.AppendedRecords[0];

        secure.PreviousHash.Should().Be("hash-predecessor-999");
        secure.IntegrityHash.Should().NotBeNullOrWhiteSpace();
        service.Verify(secure).Should().BeTrue();
    }

    [Fact]
    public async Task AppendAsync_TransientException_RetriesAndSucceeds()
    {
        var store = new TrackingAuditStore { FailAppendTimes = 1 };
        var service = CreateIntegrityService();
        var decorator = new IntegrityAuditStoreDecorator(store, service);

        var record = AuditRecordBuilder.Create().WithTenant("tenant-t").Build();
        await decorator.AppendAsync(record);

        store.AppendCallCount.Should().Be(2);
        store.AppendedRecords.Should().HaveCount(1);
    }

    [Fact]
    public async Task AppendBatchAsync_NullRecords_ThrowsArgumentNullException()
    {
        var decorator = new IntegrityAuditStoreDecorator(new TrackingAuditStore(), CreateIntegrityService());
        Func<Task> act = async () => await decorator.AppendBatchAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("records");
    }

    [Fact]
    public async Task AppendBatchAsync_EmptyRecords_ReturnsImmediately()
    {
        var store = new TrackingAuditStore { FailAppendTimes = 99 };
        var decorator = new IntegrityAuditStoreDecorator(store, CreateIntegrityService());

        Func<Task> act = async () => await decorator.AppendBatchAsync(Array.Empty<AuditRecord>());
        await act.Should().NotThrowAsync();
        store.AppendCallCount.Should().Be(0);
    }

    [Fact]
    public async Task AppendBatchAsync_SingleTenant_ChainsRecordsLocallyWithinBatch()
    {
        var store = new TrackingAuditStore();
        var service = CreateIntegrityService();
        var decorator = new IntegrityAuditStoreDecorator(store, service);

        // Pre-populate an initial record
        var existing = AuditRecordBuilder.Create()
            .WithTenant("tenant-batch")
            .WithIntegrityHash("genesis-hash")
            .Build();
        store.QueryRecords.Add(existing);

        var r1 = AuditRecordBuilder.Create().WithTenant("tenant-batch").WithAction("ACT_1").Build();
        var r2 = AuditRecordBuilder.Create().WithTenant("tenant-batch").WithAction("ACT_2").Build();
        var r3 = AuditRecordBuilder.Create().WithTenant("tenant-batch").WithAction("ACT_3").Build();

        await decorator.AppendBatchAsync(new[] { r1, r2, r3 });

        store.AppendedRecords.Should().HaveCount(3);
        var rec1 = store.AppendedRecords[0];
        var rec2 = store.AppendedRecords[1];
        var rec3 = store.AppendedRecords[2];

        rec1.PreviousHash.Should().Be("genesis-hash");
        rec1.IntegrityHash.Should().NotBeNullOrWhiteSpace();

        rec2.PreviousHash.Should().Be(rec1.IntegrityHash);
        rec2.IntegrityHash.Should().NotBeNullOrWhiteSpace();

        rec3.PreviousHash.Should().Be(rec2.IntegrityHash);
        rec3.IntegrityHash.Should().NotBeNullOrWhiteSpace();

        service.Verify(rec1).Should().BeTrue();
        service.Verify(rec2).Should().BeTrue();
        service.Verify(rec3).Should().BeTrue();
    }

    [Fact]
    public async Task AppendBatchAsync_MultiTenant_GroupsAndChainsPerTenant()
    {
        var store = new TrackingAuditStore();
        var service = CreateIntegrityService();
        var decorator = new IntegrityAuditStoreDecorator(store, service);

        var rA1 = AuditRecordBuilder.Create().WithTenant("tenant-alpha").WithAction("A1").Build();
        var rB1 = AuditRecordBuilder.Create().WithTenant("tenant-beta").WithAction("B1").Build();
        var rA2 = AuditRecordBuilder.Create().WithTenant("tenant-alpha").WithAction("A2").Build();

        await decorator.AppendBatchAsync(new[] { rA1, rB1, rA2 });

        store.AppendedRecords.Should().HaveCount(3);

        var alphaRecords = store.AppendedRecords.Where(r => r.Context.TenantId == "tenant-alpha").ToList();
        var betaRecords = store.AppendedRecords.Where(r => r.Context.TenantId == "tenant-beta").ToList();

        alphaRecords.Should().HaveCount(2);
        alphaRecords[0].PreviousHash.Should().BeNull();
        alphaRecords[1].PreviousHash.Should().Be(alphaRecords[0].IntegrityHash);

        betaRecords.Should().HaveCount(1);
        betaRecords[0].PreviousHash.Should().BeNull();
    }

    [Fact]
    public async Task QueryAsync_DelegatesToInnerStore()
    {
        var mockStore = Substitute.For<IAuditStore>();
        var query = new AuditQuery { TenantId = "tenant-1" };
        var expectedResult = new AuditQueryResult(Array.Empty<AuditRecord>(), null, false);
#pragma warning disable CA2012
        mockStore.QueryAsync(query, Arg.Any<CancellationToken>()).Returns(ValueTask.FromResult(expectedResult));
#pragma warning restore CA2012

        var decorator = new IntegrityAuditStoreDecorator(mockStore, CreateIntegrityService());
        var result = await decorator.QueryAsync(query);

        result.Should().BeSameAs(expectedResult);
    }
}
