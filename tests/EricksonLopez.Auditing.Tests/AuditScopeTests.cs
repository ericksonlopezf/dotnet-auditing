// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AwesomeAssertions;
using Xunit;

namespace EricksonLopez.Auditing.Tests;

public sealed class AuditScopeTests
{
    [Fact]
    public void AuditScope_Begin_SetsCurrentScope()
    {
        using (var scope = AuditScope.Begin())
        {
            AuditScope.Current.Should().BeSameAs(scope);
        }
    }

    [Fact]
    public void AuditScope_Dispose_ClearsCurrentScope()
    {
        using (AuditScope.Begin()) { }
        AuditScope.Current.Should().BeNull();
    }

    [Fact]
    public void AuditScope_Nested_RestoresParentOnDispose()
    {
        using var outer = AuditScope.Begin().WithMetadata("level", "outer");
        var outerRef = AuditScope.Current;

        using (var inner = AuditScope.Begin().WithMetadata("level", "inner"))
        {
            AuditScope.Current.Should().BeSameAs(inner);
            inner.Metadata["level"].Should().Be("inner");
        }

        // Parent must be restored after inner dispose
        AuditScope.Current.Should().BeSameAs(outerRef);
        AuditScope.Current!.Metadata["level"].Should().Be("outer");
    }

    [Fact]
    public void AuditScope_WithMetadata_EmptyKey_Throws()
    {
        using var scope = AuditScope.Begin();
        Action act = () => scope.WithMetadata("", "value");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AuditScope_WithMetadata_NullKey_Throws()
    {
        using var scope = AuditScope.Begin();
        Action act = () => scope.WithMetadata(null!, "value");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AuditScope_Dispose_Idempotent_DoesNotRestoreParentAgain()
    {
        var parent = AuditScope.Begin();
        var child = AuditScope.Begin();
        child.Dispose();

        var sibling = AuditScope.Begin();
        child.Dispose(); // Should be a no-op, must not restore 'parent' again

        AuditScope.Current.Should().BeSameAs(sibling);
        sibling.Dispose();
        parent.Dispose();
    }

    [Fact]
    public void AuditScope_WithMetadata_PopulatesMetadata()
    {
        using var scope = AuditScope.Begin();
        var returnedScope = scope.WithMetadata("CorrelationId", "corr-123");
        returnedScope.Should().BeSameAs(scope);
        scope.WithMetadata("Source", "OrderService").Should().BeSameAs(scope);

        scope.Metadata["CorrelationId"].Should().Be("corr-123");
        scope.Metadata["Source"].Should().Be("OrderService");
        scope.Metadata.ContainsKey("CorrelationId").Should().BeTrue();
        scope.Metadata.ContainsKey("correlationid").Should().BeFalse();
    }

    [Fact]
    public void AuditScope_InitialMetadata_PreSeeded()
    {
        using var scope = AuditScope.Begin(new Dictionary<string, string>
        {
            ["key"] = "value"
        });

        scope.Metadata["key"].Should().Be("value");
        scope.Metadata.ContainsKey("key").Should().BeTrue();
        scope.Metadata.ContainsKey("KEY").Should().BeFalse();
    }

    [Fact]
    public void AuditScope_Dispose_IsIdempotent()
    {
        var scope = AuditScope.Begin();
        scope.Dispose();
        scope.Dispose(); // Must not throw
    }

    // ── Asynchronous & Concurrency Tests ─────────────────────────────────────

    [Fact]
    public async Task AuditScope_FlowsAcrossAsyncAwaitBoundary()
    {
        using var scope = AuditScope.Begin().WithMetadata("trace-id", "trace-abc");

        await Task.Yield();

        AuditScope.Current.Should().NotBeNull();
        AuditScope.Current!.Metadata["trace-id"].Should().Be("trace-abc");
    }

    [Fact]
    public async Task AuditScope_FlowsIntoTaskRun_AndPreservesIsolation()
    {
        using var parentScope = AuditScope.Begin().WithMetadata("tenant", "parent-tenant");

        var taskResult = await Task.Run(async () =>
        {
            AuditScope.Current.Should().NotBeNull();
            AuditScope.Current!.Metadata["tenant"].Should().Be("parent-tenant");

            // Create a child scope inside the background thread
            using (var innerScope = AuditScope.Begin().WithMetadata("tenant", "child-tenant"))
            {
                await Task.Delay(10);
                innerScope.Metadata["tenant"].Should().Be("child-tenant");
            }

            return AuditScope.Current?.Metadata["tenant"];
        });

        taskResult.Should().Be("parent-tenant");
        AuditScope.Current!.Metadata["tenant"].Should().Be("parent-tenant");
    }

    [Fact]
    public async Task AuditScope_ParallelTasks_MaintainIsolatedContexts()
    {
        var tasks = Enumerable.Range(0, 20).Select(i => Task.Run(async () =>
        {
            using var scope = AuditScope.Begin().WithMetadata("taskId", $"task-{i}");
            await Task.Delay(Random.Shared.Next(5, 25));
            AuditScope.Current.Should().NotBeNull();
            AuditScope.Current!.Metadata["taskId"].Should().Be($"task-{i}");
        }));

        await Task.WhenAll(tasks);
    }
}
