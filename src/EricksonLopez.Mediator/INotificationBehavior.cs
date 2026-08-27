// Copyright © Erickson Lopez. MIT License.
using System;
using System.Threading;
using System.Threading.Tasks;

namespace EricksonLopez.Mediator;

/// <summary>
/// Defines a cross-cutting behavior that surrounds notification handling within the mediator pipeline.
/// </summary>
/// <typeparam name="TNotification">The type of notification processed by this behavior.</typeparam>
public interface INotificationBehavior<in TNotification> where TNotification : INotification
{
    /// <summary>
    /// Intercepts the notification publishing pipeline and invokes the next step.
    /// </summary>
    /// <typeparam name="TNext">The type of the struct-based pipeline continuation delegate.</typeparam>
    /// <param name="notification">The notification instance being processed.</param>
    /// <param name="next">The pipeline continuation delegate to invoke downstream behaviors or handlers.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
    /// <returns>A value task representing the asynchronous operation.</returns>
    ValueTask Handle<TNext>(TNotification notification, TNext next, CancellationToken cancellationToken)
        where TNext : struct, INext;
}
