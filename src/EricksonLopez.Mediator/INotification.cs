// Copyright © Erickson Lopez. MIT License.
namespace EricksonLopez.Mediator;

/// <summary>
/// Defines a message that represents an event to be published to zero or more handlers.
/// </summary>
/// <remarks>
/// Notifications support multicast dispatching via <see cref="IPublisher"/> and can have zero or multiple registered <see cref="INotificationHandler{TNotification}"/> instances.
/// </remarks>
public interface INotification
{
}
