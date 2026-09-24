// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using NSubstitute;
using Xunit;

namespace EricksonLopez.Auditing.Tests;

public sealed class AuditLoggerTests
{
    private sealed class TestCategory { }

    private sealed class SimpleAuditStore : IAuditStore
    {
        public List<AuditRecord> Records { get; } = new();
        public CancellationToken LastCancellationToken { get; private set; }

        public ValueTask AppendAsync(AuditRecord record, CancellationToken cancellationToken = default)
        {
            Records.Add(record);
            LastCancellationToken = cancellationToken;
            return ValueTask.CompletedTask;
        }

        public ValueTask AppendBatchAsync(IReadOnlyList<AuditRecord> records, CancellationToken cancellationToken = default)
        {
            Records.AddRange(records);
            return ValueTask.CompletedTask;
        }

        public ValueTask<AuditQueryResult> QueryAsync(AuditQuery query, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new AuditQueryResult(Array.Empty<AuditRecord>(), null, false));
    }

    [Fact]
    public void Constructor_NullStore_ThrowsArgumentNullException()
    {
        var actorProvider = Substitute.For<IAuditActorProvider>();
        Action act = () => _ = new AuditLogger<TestCategory>(null!, actorProvider);
        act.Should().Throw<ArgumentNullException>().WithParameterName("store");
    }

    [Fact]
    public void Constructor_NullActorProvider_ThrowsArgumentNullException()
    {
        var store = new SimpleAuditStore();
        Action act = () => _ = new AuditLogger<TestCategory>(store, null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("actorProvider");
    }

    [Fact]
    public async Task LogAsync_WithoutContextProvider_UsesDefaultSystemContext()
    {
        var store = new SimpleAuditStore();
        var actorProvider = Substitute.For<IAuditActorProvider>();
        var actor = new AuditActor(AuditActorType.User, "user-99", "Admin");
        actorProvider.GetCurrentActor().Returns(actor);

        var logger = new AuditLogger<TestCategory>(store, actorProvider, contextProvider: null);

        var action = AuditAction.Create;
        var resource = new AuditResource("User", "u-1");
        var outcome = AuditOutcome.Success;

        await logger.LogAsync(action, resource, outcome);

        store.Records.Should().HaveCount(1);
        var record = store.Records[0];

        record.Id.Should().NotBeEmpty();
        record.OccurredAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
        record.Actor.Should().Be(actor);
        record.Action.Should().Be(action);
        record.Resource.Should().Be(resource);
        record.Outcome.Should().Be(outcome);

        // Kills mutants: fallback must use SystemTenantId ("system") and "Unknown"
        record.Context.TenantId.Value.Should().Be("system");
        record.Context.Source.Should().Be("Unknown");
    }

    [Fact]
    public async Task LogAsync_WithContextProvider_UsesProvidedContext()
    {
        var store = new SimpleAuditStore();
        var actorProvider = Substitute.For<IAuditActorProvider>();
        actorProvider.GetCurrentActor().Returns(AuditActor.Anonymous);

        var contextProvider = Substitute.For<IAuditContextProvider>();
        var customContext = new AuditContext("tenant-custom", "CustomSource", "corr-id");
        contextProvider.GetCurrentContext().Returns(customContext);

        var logger = new AuditLogger<TestCategory>(store, actorProvider, contextProvider);

        await logger.LogAsync(AuditAction.Delete, new AuditResource("Invoice", "inv-1"), AuditOutcome.Failure);

        store.Records.Should().HaveCount(1);
        var record = store.Records[0];

        record.Context.Should().Be(customContext);
        record.Context.TenantId.Value.Should().Be("tenant-custom");
        record.Context.Source.Should().Be("CustomSource");
        record.Context.CorrelationId.Should().Be("corr-id");
    }

    [Fact]
    public async Task LogAsync_PropagatesCancellationToken()
    {
        var store = new SimpleAuditStore();
        var actorProvider = Substitute.For<IAuditActorProvider>();
        actorProvider.GetCurrentActor().Returns(AuditActor.Anonymous);

        var logger = new AuditLogger<TestCategory>(store, actorProvider);

        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        await logger.LogAsync(AuditAction.Update, new AuditResource("Item", "1"), AuditOutcome.Success, token);

        store.LastCancellationToken.Should().Be(token);
    }
}
