// Copyright © Erickson Lopez. MIT License.
using System;
using System.Security.Cryptography;
using EricksonLopez.Auditing;
using Xunit;

namespace EricksonLopez.Auditing.Tests;

public class MemoryAllocationTests
{
    private sealed class DummyKeyProvider : IAuditIntegrityProvider
    {
        private readonly byte[] _key = new byte[32];
        public DummyKeyProvider() => RandomNumberGenerator.Fill(_key);
        public ReadOnlyMemory<byte> GetCurrentKey(TenantId tenantId) => _key;
    }

    [Fact]
    public void ComputeHash_ShouldNotAllocateOnLargeObjectHeap()
    {
        // Arrange
        var keyProvider = new DummyKeyProvider();
        var algorithm = new HmacSha256AuditHashAlgorithm();
        var service = new HmacAuditIntegrityService(keyProvider, algorithm);
        var record = new AuditRecord
        {
            Id = Guid.NewGuid(),
            OccurredAt = DateTimeOffset.UtcNow,
            Context = new AuditContext(new TenantId("tenant-a"), "TestSource"),
            Actor = new AuditActor(AuditActorType.Service, "sys", null),
            Action = new AuditAction("Test.Action"),
            Resource = new AuditResource("Resource", "123"),
            Outcome = AuditOutcome.Success
        };

        // Warm up to force static initializations
        _ = service.ComputeHash(record, null);

        // Act
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        long bytesBefore = GC.GetAllocatedBytesForCurrentThread();
        string hash = service.ComputeHash(record, null);
        long bytesAfter = GC.GetAllocatedBytesForCurrentThread();

        // Assert
        long allocations = bytesAfter - bytesBefore;

        // HmacAuditIntegrityService currently returns a string, which allocates.
        // Convert.ToHexString allocates a string of length 64 chars (~128 bytes + object overhead).
        // The array pool rents char and byte buffers, which avoids allocations if in pool.
        // We assert that total allocations are extremely small (well under LOH threshold of 85,000 bytes).
        Assert.True(allocations < 1000, $"Allocated {allocations} bytes, which is higher than expected.");
    }
}
