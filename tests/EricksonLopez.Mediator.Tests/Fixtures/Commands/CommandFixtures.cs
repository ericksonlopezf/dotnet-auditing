// Copyright © Erickson Lopez. MIT License.
using System;
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.Mediator;

namespace EricksonLopez.Mediator.Tests.Fixtures;

// ─── Basic Commands & Queries ──────────────────────────────────────────────────

public class PingCommand : ICommand<string> { }

public class PingCommandHandler : ICommandHandler<PingCommand, string>
{
    public ValueTask<string> Handle(PingCommand command, CancellationToken cancellationToken) => new("Pong");
}

public class MyQuery : IQuery<int> { }

public class MyQueryHandler : IQueryHandler<MyQuery, int>
{
    public ValueTask<int> Handle(MyQuery query, CancellationToken cancellationToken) => new(42);
}

// ─── Ordered Commands ──────────────────────────────────────────────────────────

[UseBehavior(typeof(Behavior2), 2)]
[UseBehavior(typeof(Behavior1), 1)]
public class OrderedCommand : ICommand<int> { }

public class OrderedCommandHandler : ICommandHandler<OrderedCommand, int>
{
    public ValueTask<int> Handle(OrderedCommand command, CancellationToken cancellationToken) => new(100);
}

[UseBehavior(typeof(SingleOrderBehavior))]
public class SingleBehaviorCommand : ICommand<int> { }

public class SingleBehaviorCommandHandler : ICommandHandler<SingleBehaviorCommand, int>
{
    public ValueTask<int> Handle(SingleBehaviorCommand command, CancellationToken cancellationToken) => new(50);
}

// ─── CancellationToken propagation fixtures ───────────────────────────────────

public class CancelCommand : ICommand<string> { }

public class CancelCommandHandler : ICommandHandler<CancelCommand, string>
{
    private readonly TestStateTracker _tracker;
    public CancelCommandHandler(TestStateTracker tracker) => _tracker = tracker;

    public ValueTask<string> Handle(CancelCommand command, CancellationToken cancellationToken)
    {
        _tracker.SetToken(nameof(CancelCommandHandler), cancellationToken);
        return new("ok");
    }
}

[UseBehavior(typeof(CancelBehaviorForCancelCommandWithBehavior))]
public class CancelCommandWithBehavior : ICommand<string> { }

public class CancelCommandWithBehaviorHandler : ICommandHandler<CancelCommandWithBehavior, string>
{
    private readonly TestStateTracker _tracker;
    public CancelCommandWithBehaviorHandler(TestStateTracker tracker) => _tracker = tracker;

    public ValueTask<string> Handle(CancelCommandWithBehavior command, CancellationToken cancellationToken)
    {
        _tracker.SetToken(nameof(CancelCommandWithBehaviorHandler), cancellationToken);
        return new("ok");
    }
}

public class PreCancelledCommand : ICommand<string> { }

public class PreCancelledCommandHandler : ICommandHandler<PreCancelledCommand, string>
{
    private readonly TestStateTracker _tracker;
    public PreCancelledCommandHandler(TestStateTracker tracker) => _tracker = tracker;

    public ValueTask<string> Handle(PreCancelledCommand command, CancellationToken cancellationToken)
    {
        _tracker.MarkInvoked(nameof(PreCancelledCommandHandler));
        return new("should-not-run");
    }
}

public class PreCancelledQuery : IQuery<int> { }

public class PreCancelledQueryHandler : IQueryHandler<PreCancelledQuery, int>
{
    private readonly TestStateTracker _tracker;
    public PreCancelledQueryHandler(TestStateTracker tracker) => _tracker = tracker;

    public ValueTask<int> Handle(PreCancelledQuery query, CancellationToken cancellationToken)
    {
        _tracker.MarkInvoked(nameof(PreCancelledQueryHandler));
        return new(-1);
    }
}



// ─── Exception propagation fixtures ──────────────────────────────────────────

public class ThrowCommand : ICommand<string> { }

public class ThrowCommandHandler : ICommandHandler<ThrowCommand, string>
{
    public ValueTask<string> Handle(ThrowCommand command, CancellationToken cancellationToken)
        => throw new InvalidOperationException("handler-exception");
}

// ─── Nested dispatch fixtures ─────────────────────────────────────────────────

public class OuterCommand : ICommand<string> { }

public class OuterCommandHandler : ICommandHandler<OuterCommand, string>
{
    private readonly IMediator _mediator;
    public OuterCommandHandler(IMediator mediator) => _mediator = mediator;

    public async ValueTask<string> Handle(OuterCommand command, CancellationToken cancellationToken)
    {
        var inner = await _mediator.Send(new PingCommand(), cancellationToken);
        return $"outer:{inner}";
    }
}

// ─── ServiceLifetime fixtures ─────────────────────────────────────────────────

public class SingletonCommand : ICommand<Guid> { }

[ServiceLifetime(HandlerLifetime.Singleton)]
public class SingletonCommandHandler : ICommandHandler<SingletonCommand, Guid>
{
    private readonly Guid _id = Guid.NewGuid();
    public ValueTask<Guid> Handle(SingletonCommand command, CancellationToken cancellationToken) => new(_id);
}

public class ScopedCommand : ICommand<Guid> { }

[ServiceLifetime(HandlerLifetime.Scoped)]
public class ScopedCommandHandler : ICommandHandler<ScopedCommand, Guid>
{
    private readonly Guid _id = Guid.NewGuid();
    public ValueTask<Guid> Handle(ScopedCommand command, CancellationToken cancellationToken) => new(_id);
}

// ─── UseBehavior isolation fixtures ──────────────────────────────────────────

[UseBehavior(typeof(TrackedBehavior))]
public class CommandWithBehavior : ICommand<string> { }

public class CommandWithBehaviorHandler : ICommandHandler<CommandWithBehavior, string>
{
    public ValueTask<string> Handle(CommandWithBehavior command, CancellationToken cancellationToken) => new("with-behavior");
}

public class CommandWithoutBehavior : ICommand<string> { }

public class CommandWithoutBehaviorHandler : ICommandHandler<CommandWithoutBehavior, string>
{
    public ValueTask<string> Handle(CommandWithoutBehavior command, CancellationToken cancellationToken) => new("without-behavior");
}

// ─── 5-behavior pipeline fixtures ────────────────────────────────────────────

[UseBehavior(typeof(B5), 5)]
[UseBehavior(typeof(B4), 4)]
[UseBehavior(typeof(B3), 3)]
[UseBehavior(typeof(B2), 2)]
[UseBehavior(typeof(B1), 1)]
public class FiveBehaviorCommand : ICommand<int> { }

public class FiveBehaviorCommandHandler : ICommandHandler<FiveBehaviorCommand, int>
{
    public ValueTask<int> Handle(FiveBehaviorCommand command, CancellationToken cancellationToken) => new(0);
}
