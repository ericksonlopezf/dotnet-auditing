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

        var expectedFile = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"audit-deferred-{record.Id:N}.json");
        System.IO.File.Exists(expectedFile).Should().BeTrue();
        try { System.IO.File.Delete(expectedFile); } catch { }
    }

    [Fact]
    public void Constructor_NullArguments_ThrowArgumentNullException()
    {
        var store = new FaultyAuditStore();
        var config = new AuditConfiguration();

        Action act1 = () => _ = new ResilientAuditStoreDecorator(null!, config);
        act1.Should().Throw<ArgumentNullException>().WithParameterName("innerStore");

        Action act2 = () => _ = new ResilientAuditStoreDecorator(store, null!);
        act2.Should().Throw<ArgumentNullException>().WithParameterName("configuration");
    }

    [Fact]
    public async Task AppendAsync_NullRecord_ThrowsArgumentNullException()
    {
        var decorator = new ResilientAuditStoreDecorator(new FaultyAuditStore(), new AuditConfiguration());
        Func<Task> act = async () => await decorator.AppendAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("record");
    }

    [Fact]
    public async Task AppendBatchAsync_NullRecords_ThrowsArgumentNullException()
    {
        var decorator = new ResilientAuditStoreDecorator(new FaultyAuditStore(), new AuditConfiguration());
        Func<Task> act = async () => await decorator.AppendBatchAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("records");
    }

    [Fact]
    public async Task AppendBatchAsync_EmptyRecords_ReturnsImmediately()
    {
        var faultyStore = new FaultyAuditStore { ThrowOnAppend = true };
        var config = new AuditConfiguration { DefaultFailureBehavior = AuditFailureBehavior.FailClosed };
        var decorator = new ResilientAuditStoreDecorator(faultyStore, config);

        // If empty, should not call innerStore and not throw even if FailClosed
        Func<Task> act = async () => await decorator.AppendBatchAsync(Array.Empty<AuditRecord>());
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task QueryAsync_DelegatesToInnerStore()
    {
        var mockStore = NSubstitute.Substitute.For<IAuditStore>();
        var query = new AuditQuery { TenantId = "tenant-1" };
        var expectedResult = new AuditQueryResult(Array.Empty<AuditRecord>(), null, false);
#pragma warning disable CA2012
        mockStore.QueryAsync(query, Arg.Any<CancellationToken>()).Returns(ValueTask.FromResult(expectedResult));
#pragma warning restore CA2012

        var decorator = new ResilientAuditStoreDecorator(mockStore, new AuditConfiguration());
        var result = await decorator.QueryAsync(query);
        result.Should().BeSameAs(expectedResult);
    }

    [Fact]
    public async Task AppendAsync_FailOpen_WithLogger_LogsWarning()
    {
        var faultyStore = new FaultyAuditStore();
        var config = new AuditConfiguration { DefaultFailureBehavior = AuditFailureBehavior.FailOpen };
        var logger = NSubstitute.Substitute.For<Microsoft.Extensions.Logging.ILogger<ResilientAuditStoreDecorator>>();
        logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Warning).Returns(true);

        var decorator = new ResilientAuditStoreDecorator(faultyStore, config, logger);
        var record = AuditRecordBuilder.Create().WithAction("READ").Build();

        await decorator.AppendAsync(record);

        // Verify warning was logged
        logger.ReceivedWithAnyArgs().Log(
            Microsoft.Extensions.Logging.LogLevel.Warning,
            Arg.Any<Microsoft.Extensions.Logging.EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task AppendBatchAsync_FailOpen_NonCritical_SuppressesExceptionAndLogsWarning()
    {
        var faultyStore = new FaultyAuditStore();
        var config = new AuditConfiguration { DefaultFailureBehavior = AuditFailureBehavior.FailOpen };
        var logger = NSubstitute.Substitute.For<Microsoft.Extensions.Logging.ILogger<ResilientAuditStoreDecorator>>();
        logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Warning).Returns(true);

        var decorator = new ResilientAuditStoreDecorator(faultyStore, config, logger);
        var records = new[] { AuditRecordBuilder.Create().WithAction("READ").Build() };

        Func<Task> act = async () => await decorator.AppendBatchAsync(records);
        await act.Should().NotThrowAsync();

        logger.ReceivedWithAnyArgs().Log(
            Microsoft.Extensions.Logging.LogLevel.Warning,
            Arg.Any<Microsoft.Extensions.Logging.EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task AppendBatchAsync_FailClosed_NonCritical_ThrowsException()
    {
        var faultyStore = new FaultyAuditStore();
        var config = new AuditConfiguration { DefaultFailureBehavior = AuditFailureBehavior.FailClosed };
        var decorator = new ResilientAuditStoreDecorator(faultyStore, config);
        var records = new[] { AuditRecordBuilder.Create().WithAction("READ").Build() };

        Func<Task> act = async () => await decorator.AppendBatchAsync(records);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Simulated database outage");
    }

    [Fact]
    public async Task AppendBatchAsync_Deferred_NonCritical_SuppressesExceptionAndEnqueuesFallback()
    {
        var faultyStore = new FaultyAuditStore();
        var config = new AuditConfiguration { DefaultFailureBehavior = AuditFailureBehavior.Deferred };
        var decorator = new ResilientAuditStoreDecorator(faultyStore, config);
        var records = new[] { AuditRecordBuilder.Create().WithAction("READ").Build() };

        Func<Task> act = async () => await decorator.AppendBatchAsync(records);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ShouldPropagateBatch_IteratesAllRecordsToFindCriticalActionAtEnd()
    {
        var faultyStore = new FaultyAuditStore();
        var config = new AuditConfiguration { DefaultFailureBehavior = AuditFailureBehavior.FailOpen };
        config.CriticalActionCodes.Add("LAST_CRITICAL");
        var decorator = new ResilientAuditStoreDecorator(faultyStore, config);

        var records = new[]
        {
            AuditRecordBuilder.Create().WithAction("READ").Build(),
            AuditRecordBuilder.Create().WithAction("UPDATE").Build(),
            AuditRecordBuilder.Create().WithAction("LAST_CRITICAL").Build()
        };

        Func<Task> act = async () => await decorator.AppendBatchAsync(records);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Simulated database outage");
    }
}



