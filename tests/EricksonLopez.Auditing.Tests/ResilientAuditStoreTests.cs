// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using EricksonLopez.Auditing.Testing;
using NSubstitute;
using Xunit;

namespace EricksonLopez.Auditing.Tests;

public sealed class ResilientAuditStoreTests
{
    private sealed class FaultyAuditStore : IAuditStore
    {
        public bool ThrowOnAppend { get; set; } = true;
        public List<AuditRecord> Appended { get; } = new();

        public ValueTask AppendAsync(AuditRecord record, CancellationToken cancellationToken = default)
        {
            if (ThrowOnAppend)
            {
                throw new InvalidOperationException("Simulated database outage");
            }
            Appended.Add(record);
            return ValueTask.CompletedTask;
        }

        public ValueTask AppendBatchAsync(IReadOnlyList<AuditRecord> records, CancellationToken cancellationToken = default)
        {
            if (ThrowOnAppend)
            {
                throw new InvalidOperationException("Simulated database outage");
            }
            Appended.AddRange(records);
            return ValueTask.CompletedTask;
        }

        public ValueTask<AuditQueryResult> QueryAsync(AuditQuery query, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new AuditQueryResult(Array.Empty<AuditRecord>(), null, false));
    }

    [Fact]
    public async Task AppendAsync_FailOpen_NonCriticalAction_SuppressesException()
    {
        var faultyStore = new FaultyAuditStore();
        var config = new AuditConfiguration
        {
            DefaultFailureBehavior = AuditFailureBehavior.FailOpen
        };
        var decorator = new ResilientAuditStoreDecorator(faultyStore, config);

        var record = AuditRecordBuilder.Create()
            .WithAction("READ")
            .Build();

        // Should not throw
        Func<Task> act = async () => await decorator.AppendAsync(record);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task AppendAsync_FailOpen_CriticalAction_RethrowsException()
    {
        var faultyStore = new FaultyAuditStore();
        var config = new AuditConfiguration
        {
            DefaultFailureBehavior = AuditFailureBehavior.FailOpen
        };
        config.CriticalActionCodes.Add("SECURITY_PRIVILEGE_ELEVATION");
        var decorator = new ResilientAuditStoreDecorator(faultyStore, config);

        var record = AuditRecordBuilder.Create()
            .WithAction("SECURITY_PRIVILEGE_ELEVATION")
            .Build();

        Func<Task> act = async () => await decorator.AppendAsync(record);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Simulated database outage");
    }

    [Fact]
    public async Task AppendAsync_FailClosed_NonCriticalAction_RethrowsException()
    {
        var faultyStore = new FaultyAuditStore();
        var config = new AuditConfiguration
        {
            DefaultFailureBehavior = AuditFailureBehavior.FailClosed
        };
        var decorator = new ResilientAuditStoreDecorator(faultyStore, config);

        var record = AuditRecordBuilder.Create()
            .WithAction("READ")
            .Build();

        Func<Task> act = async () => await decorator.AppendAsync(record);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Simulated database outage");
    }

    [Fact]
    public async Task AppendBatchAsync_FailOpen_WithOneCriticalRecord_RethrowsException()
    {
        var faultyStore = new FaultyAuditStore();
        var config = new AuditConfiguration
        {
            DefaultFailureBehavior = AuditFailureBehavior.FailOpen
        };
        config.CriticalActionCodes.Add("AUTH_REVOKE");
        var decorator = new ResilientAuditStoreDecorator(faultyStore, config);

        var records = new[]
        {
            AuditRecordBuilder.Create().WithAction("READ").Build(),
            AuditRecordBuilder.Create().WithAction("AUTH_REVOKE").Build()
        };

        Func<Task> act = async () => await decorator.AppendBatchAsync(records);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Simulated database outage");
    }

    [Fact]
    public async Task AppendAsync_PropagatesCancellationToken()
    {
        var faultyStore = new FaultyAuditStore { ThrowOnAppend = false };
        var config = new AuditConfiguration();
        var decorator = new ResilientAuditStoreDecorator(faultyStore, config);
        var record = AuditRecordBuilder.Create().WithAction("READ").Build();

        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Pre-cancel

        // Modify FaultyAuditStore to throw if token is cancelled
        var mockStore = NSubstitute.Substitute.For<IAuditStore>();
#pragma warning disable CA2012
        mockStore.AppendAsync(Arg.Any<AuditRecord>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                callInfo.Arg<CancellationToken>().ThrowIfCancellationRequested();
                return ValueTask.CompletedTask;
            });
#pragma warning restore CA2012

        var mockDecorator = new ResilientAuditStoreDecorator(mockStore, config);

        Func<Task> act = async () => await mockDecorator.AppendAsync(record, cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task AppendAsync_FailOpen_CancellationTokenCancelled_SuppressesException()
    {
        var mockStore = NSubstitute.Substitute.For<IAuditStore>();
#pragma warning disable CA2012
        mockStore.AppendAsync(Arg.Any<AuditRecord>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                callInfo.Arg<CancellationToken>().ThrowIfCancellationRequested();
                return ValueTask.CompletedTask;
            });
#pragma warning restore CA2012

        var config = new AuditConfiguration
        {
            DefaultFailureBehavior = AuditFailureBehavior.FailOpen
        };
        var decorator = new ResilientAuditStoreDecorator(mockStore, config);
        var record = AuditRecordBuilder.Create().WithAction("READ").Build();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Func<Task> act = async () => await decorator.AppendAsync(record, cts.Token);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task AppendAsync_Deferred_NonCriticalAction_SuppressesExceptionAndEnqueuesFallback()
    {
        var faultyStore = new FaultyAuditStore();
        var config = new AuditConfiguration
        {
            DefaultFailureBehavior = AuditFailureBehavior.Deferred
        };
        var decorator = new ResilientAuditStoreDecorator(faultyStore, config);

        var record = AuditRecordBuilder.Create()
            .WithAction("UPDATE")
            .Build();

        Func<Task> act = async () => await decorator.AppendAsync(record);
        await act.Should().NotThrowAsync();
    }
}



