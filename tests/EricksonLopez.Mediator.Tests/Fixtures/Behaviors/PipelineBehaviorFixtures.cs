// Copyright © Erickson Lopez. MIT License.
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.Mediator;

namespace EricksonLopez.Mediator.Tests.Fixtures;

// ─── Pipeline Logs ─────────────────────────────────────────────────────────────

public class OrderLog
{
    public List<int> Log { get; } = new();
}

// ─── Ordered Pipeline Behaviors ────────────────────────────────────────────────

public class Behavior1 : IPipelineBehavior<OrderedCommand, int>
{
    private readonly OrderLog _log;
    public Behavior1(OrderLog log) => _log = log;

    public async ValueTask<int> Handle<TNext>(OrderedCommand request, TNext next, CancellationToken cancellationToken) where TNext : struct, INext<int>
    {
        _log.Log.Add(1);
        var res = await next.InvokeAsync();
        _log.Log.Add(-1);
        return res;
    }
}

public class Behavior2 : IPipelineBehavior<OrderedCommand, int>
{
    private readonly OrderLog _log;
    public Behavior2(OrderLog log) => _log = log;

    public async ValueTask<int> Handle<TNext>(OrderedCommand request, TNext next, CancellationToken cancellationToken) where TNext : struct, INext<int>
    {
        _log.Log.Add(2);
        var res = await next.InvokeAsync();
        _log.Log.Add(-2);
        return res;
    }
}

public class SingleOrderBehavior : IPipelineBehavior<SingleBehaviorCommand, int>
{
    private readonly OrderLog _log;
    public SingleOrderBehavior(OrderLog log) => _log = log;

    public async ValueTask<int> Handle<TNext>(SingleBehaviorCommand request, TNext next, CancellationToken cancellationToken) where TNext : struct, INext<int>
    {
        _log.Log.Add(1);
        var res = await next.InvokeAsync();
        _log.Log.Add(-1);
        return res;
    }
}

// ─── Cancellation Behaviors ───────────────────────────────────────────────────

public class CancelBehaviorForCancelCommandWithBehavior : IPipelineBehavior<CancelCommandWithBehavior, string>
{
    private readonly TestStateTracker _tracker;
    public CancelBehaviorForCancelCommandWithBehavior(TestStateTracker tracker) => _tracker = tracker;

    public async ValueTask<string> Handle<TNext>(CancelCommandWithBehavior request, TNext next, CancellationToken cancellationToken)
        where TNext : struct, INext<string>
    {
        _tracker.SetToken(nameof(CancelBehaviorForCancelCommandWithBehavior), cancellationToken);
        return await next.InvokeAsync();
    }
}

// ─── Tracked Isolation Behaviors ───────────────────────────────────────────────

public class TrackedBehavior : IPipelineBehavior<CommandWithBehavior, string>
{
    private readonly BehaviorTracker _tracker;
    public TrackedBehavior(BehaviorTracker tracker) => _tracker = tracker;

    public async ValueTask<string> Handle<TNext>(CommandWithBehavior request, TNext next, CancellationToken cancellationToken)
        where TNext : struct, INext<string>
    {
        _tracker.MarkInvoked();
        return await next.InvokeAsync();
    }
}

// ─── 5-Level Deep Pipeline Behaviors ──────────────────────────────────────────

public class B1 : IPipelineBehavior<FiveBehaviorCommand, int>
{
    private readonly OrderLog _log;
    public B1(OrderLog log) => _log = log;
    public async ValueTask<int> Handle<TNext>(FiveBehaviorCommand request, TNext next, CancellationToken cancellationToken) where TNext : struct, INext<int>
    { _log.Log.Add(1); var r = await next.InvokeAsync(); _log.Log.Add(-1); return r; }
}

public class B2 : IPipelineBehavior<FiveBehaviorCommand, int>
{
    private readonly OrderLog _log;
    public B2(OrderLog log) => _log = log;
    public async ValueTask<int> Handle<TNext>(FiveBehaviorCommand request, TNext next, CancellationToken cancellationToken) where TNext : struct, INext<int>
    { _log.Log.Add(2); var r = await next.InvokeAsync(); _log.Log.Add(-2); return r; }
}

public class B3 : IPipelineBehavior<FiveBehaviorCommand, int>
{
    private readonly OrderLog _log;
    public B3(OrderLog log) => _log = log;
    public async ValueTask<int> Handle<TNext>(FiveBehaviorCommand request, TNext next, CancellationToken cancellationToken) where TNext : struct, INext<int>
    { _log.Log.Add(3); var r = await next.InvokeAsync(); _log.Log.Add(-3); return r; }
}

public class B4 : IPipelineBehavior<FiveBehaviorCommand, int>
{
    private readonly OrderLog _log;
    public B4(OrderLog log) => _log = log;
    public async ValueTask<int> Handle<TNext>(FiveBehaviorCommand request, TNext next, CancellationToken cancellationToken) where TNext : struct, INext<int>
    { _log.Log.Add(4); var r = await next.InvokeAsync(); _log.Log.Add(-4); return r; }
}

public class B5 : IPipelineBehavior<FiveBehaviorCommand, int>
{
    private readonly OrderLog _log;
    public B5(OrderLog log) => _log = log;
    public async ValueTask<int> Handle<TNext>(FiveBehaviorCommand request, TNext next, CancellationToken cancellationToken) where TNext : struct, INext<int>
    { _log.Log.Add(5); var r = await next.InvokeAsync(); _log.Log.Add(-5); return r; }
}
