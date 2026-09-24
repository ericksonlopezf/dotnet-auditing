// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using EricksonLopez.Auditing;
using EricksonLopez.Auditing.Outbox;
using EricksonLopez.Auditing.Testing;
using NSubstitute;
using Xunit;

namespace EricksonLopez.Auditing.Tests;

public sealed class OutboxAuditStoreTests
{
    private readonly IOutboxMessageService _outboxService = Substitute.For<IOutboxMessageService>();

    [Fact]
    public void Constructor_NullOutboxService_ThrowsArgumentNullException()
    {
        Action act = () => _ = new OutboxAuditStore(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task AppendAsync_ValidRecord_InvokesOutboxWithSerializedPayload()
    {
        // Arrange
        var store = new OutboxAuditStore(_outboxService);
        var record = AuditRecordBuilder.Create()
            .WithAction("UPDATE")
            .WithResource("Order", "12345")
            .Build();

        // Act
        await store.AppendAsync(record);

        // Assert
        await _outboxService.Received(1).AppendMessageAsync(
            "AuditRecordCreated",
            Arg.Is<string>(payload => payload.Contains("12345")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AppendBatchAsync_ValidRecords_InvokesOutboxWithSerializedBatch()
    {
        // Arrange
        var store = new OutboxAuditStore(_outboxService);
        var records = new List<AuditRecord>
        {
            AuditRecordBuilder.Create().WithAction("CREATE").Build(),
            AuditRecordBuilder.Create().WithAction("DELETE").Build()
        };

        // Act
        await store.AppendBatchAsync(records);

        // Assert
        await _outboxService.Received(1).AppendMessageAsync(
            "AuditRecordBatchCreated",
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task QueryAsync_ThrowsNotSupportedException()
    {
        // Arrange
        var store = new OutboxAuditStore(_outboxService);
        var query = new AuditQuery { TenantId = "tenant-1" };

        // Act
        Func<Task> act = async () => await store.QueryAsync(query);

        // Assert
        await act.Should().ThrowAsync<NotSupportedException>();
    }
}
