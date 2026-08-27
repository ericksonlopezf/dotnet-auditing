// Copyright © Erickson Lopez. MIT License.
using System;
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.Mediator;

namespace EricksonLopez.Mediator.Tests.Fixtures;

// ─── Sequential Notifications ──────────────────────────────────────────────────

public class MyNotification : INotification { }

public class NotificationHandler1 : INotificationHandler<MyNotification>
{
    private readonly TestStateTracker _tracker;
    public NotificationHandler1(TestStateTracker tracker) => _tracker = tracker;

    public ValueTask Handle(MyNotification notification, CancellationToken cancellationToken)
    {
        _tracker.MarkInvoked(nameof(NotificationHandler1));
        return default;
    }
}

public class NotificationHandler2 : INotificationHandler<MyNotification>
{
    private readonly TestStateTracker _tracker;
    public NotificationHandler2(TestStateTracker tracker) => _tracker = tracker;

    public ValueTask Handle(MyNotification notification, CancellationToken cancellationToken)
    {
        _tracker.MarkInvoked(nameof(NotificationHandler2));
        return default;
    }
}

public class PreCancelledNotification : INotification { }

public class PreCancelledNotificationHandler : INotificationHandler<PreCancelledNotification>
{
    private readonly TestStateTracker _tracker;
    public PreCancelledNotificationHandler(TestStateTracker tracker) => _tracker = tracker;

    public ValueTask Handle(PreCancelledNotification notification, CancellationToken cancellationToken)
    {
        _tracker.MarkInvoked(nameof(PreCancelledNotificationHandler));
        return default;
    }
}


// ─── Parallel Notifications ────────────────────────────────────────────────────

[PublishStrategy(PublishStrategy.Parallel)]
public class ParallelNotification : INotification { }

public class ParallelNotificationHandler1 : INotificationHandler<ParallelNotification>
{
    private readonly TestStateTracker _tracker;
    public ParallelNotificationHandler1(TestStateTracker tracker) => _tracker = tracker;

    public async ValueTask Handle(ParallelNotification notification, CancellationToken cancellationToken)
    {
        await Task.Delay(10, cancellationToken);
        _tracker.MarkInvoked(nameof(ParallelNotificationHandler1));
    }
}

public class ParallelNotificationHandler2 : INotificationHandler<ParallelNotification>
{
    private readonly TestStateTracker _tracker;
    public ParallelNotificationHandler2(TestStateTracker tracker) => _tracker = tracker;

    public async ValueTask Handle(ParallelNotification notification, CancellationToken cancellationToken)
    {
        await Task.Delay(10, cancellationToken);
        _tracker.MarkInvoked(nameof(ParallelNotificationHandler2));
    }
}

// ─── Aggregated Exception Notifications ────────────────────────────────────────

[PublishStrategy(PublishStrategy.SequentialAggregateExceptions)]
public class AggregatedNotification : INotification { }

public class AggregatedNotificationHandler1 : INotificationHandler<AggregatedNotification>
{
    public ValueTask Handle(AggregatedNotification notification, CancellationToken cancellationToken)
        => throw new InvalidOperationException("First aggregated error");
}

public class AggregatedNotificationHandler2 : INotificationHandler<AggregatedNotification>
{
    public ValueTask Handle(AggregatedNotification notification, CancellationToken cancellationToken)
        => throw new ArgumentException("Second aggregated error");
}
