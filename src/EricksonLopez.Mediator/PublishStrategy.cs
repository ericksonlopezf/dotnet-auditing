// Copyright © Erickson Lopez. MIT License.

namespace EricksonLopez.Mediator;

/// <summary>
/// Specifies the dispatching strategy used when publishing notifications to multiple handlers.
/// </summary>
public enum PublishStrategy
{
    /// <summary>
    /// Executes all registered notification handlers sequentially in registration order.
    /// </summary>
    Sequential,

    /// <summary>
    /// Executes all registered notification handlers concurrently using <see cref="System.Threading.Tasks.Task.WhenAll(System.Threading.Tasks.Task[])"/>.
    /// </summary>
    Parallel,

    /// <summary>
    /// Executes all handlers sequentially, catching failures and aggregating them into a <see cref="NotificationHandlerAggregateException"/>.
    /// </summary>
    SequentialAggregateExceptions
}
