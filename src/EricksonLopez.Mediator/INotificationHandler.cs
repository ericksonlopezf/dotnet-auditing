// Copyright © Erickson Lopez. MIT License.
using System;
using System.Threading;
using System.Threading.Tasks;

namespace EricksonLopez.Mediator;

/// <summary>
/// Defines a handler for processing notifications of type <typeparamref name="TNotification"/>.
/// </summary>
/// <remarks>
/// Multiple handlers can be registered for the same notification type, and are executed according to the configured publishing strategy.
/// </remarks>
/// <typeparam name="TNotification">The type of notification to process.</typeparam>
public interface INotificationHandler<in TNotification> where TNotification : INotification
{
    /// <summary>
    /// Processes the specified notification.
    /// </summary>
    /// <param name="notification">The notification instance to process.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
    /// <returns>A value task representing the asynchronous operation.</returns>
    ValueTask Handle(TNotification notification, CancellationToken cancellationToken);
}
