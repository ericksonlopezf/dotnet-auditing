# Level 04: Events & Notifications

Commands and Queries have exactly *one* handler. Notifications (`INotification`), by contrast, represent domain events or integration triggers broadcast to zero or multiple subscribers.

---

## 1. Defining a Notification (`INotification`)

```csharp
using System;
using EricksonLopez.Mediator;

public sealed record UserRegisteredNotification(Guid UserId, string Email) : INotification;
```

---

## 2. Implementing Subscribers (`INotificationHandler<T>`)

Multiple handlers can subscribe to the same notification:

```csharp
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.Mediator;

// Subscriber 1: Send Welcome Email
public sealed class SendWelcomeEmailHandler : INotificationHandler<UserRegisteredNotification>
{
    private readonly IEmailService _emailService;
    public SendWelcomeEmailHandler(IEmailService emailService) => _emailService = emailService;

    public async ValueTask Handle(UserRegisteredNotification notification, CancellationToken cancellationToken)
    {
        await _emailService.SendWelcomeEmailAsync(notification.Email, cancellationToken).ConfigureAwait(false);
    }
}

// Subscriber 2: Audit Event Log
public sealed class AuditRegistrationHandler : INotificationHandler<UserRegisteredNotification>
{
    private readonly IAuditLog _audit;
    public AuditRegistrationHandler(IAuditLog audit) => _audit = audit;

    public async ValueTask Handle(UserRegisteredNotification notification, CancellationToken cancellationToken)
    {
        await _audit.LogAsync("UserRegistered", notification.UserId, cancellationToken).ConfigureAwait(false);
    }
}
```

---

## 3. Publishing Notifications

Publish notifications using `IPublisher` (or `IMediator`):

```csharp
public sealed class RegisterUserCommandHandler : ICommandHandler<RegisterUserCommand, Guid>
{
    private readonly IPublisher _publisher;
    public RegisterUserCommandHandler(IPublisher publisher) => _publisher = publisher;

    public async ValueTask<Guid> Handle(RegisterUserCommand command, CancellationToken ct)
    {
        var userId = Guid.NewGuid();
        
        // Publish event to all registered subscribers
        await _publisher.Publish(new UserRegisteredNotification(userId, command.Email), ct);
        
        return userId;
    }
}
```

---

## 4. Publication Strategies (`[PublishStrategy]`)

Configure the execution strategy on the notification type using the `[PublishStrategy]` attribute:

```csharp
// 1. Sequential (Default) — executes handlers one by one; fails on first exception
[PublishStrategy(PublishStrategy.Sequential)]
public sealed record OrderPlacedNotification(Guid OrderId) : INotification;

// 2. Parallel — executes handlers concurrently via Task.WhenAll
[PublishStrategy(PublishStrategy.Parallel)]
public sealed record InventoryUpdatedNotification(string Sku) : INotification;

// 3. Exception Aggregation — executes all handlers even if preceding ones throw, aggregating into NotificationHandlerAggregateException
[PublishStrategy(PublishStrategy.SequentialAggregateExceptions)]
public sealed record CriticalSystemEventNotification(string Message) : INotification;
```

| Strategy | Description | Error Handling |
|---|---|---|
| `PublishStrategy.Sequential` *(Default)* | Sequential execution | Aborts immediately on first exception |
| `PublishStrategy.Parallel` | Concurrent execution via `Task.WhenAll` | Throws `AggregateException` containing all failures |
| `PublishStrategy.SequentialAggregateExceptions` | Sequential execution to completion | Collects all exceptions into `NotificationHandlerAggregateException` |

---

## 5. Notification Pipeline Behaviors

You can intercept notification publishing using `INotificationBehavior<TNotification>`:

```csharp
public sealed class NotificationAuditBehavior<TNotification> : INotificationBehavior<TNotification>
    where TNotification : INotification
{
    public async ValueTask Handle<TNext>(TNotification notification, TNext next, CancellationToken cancellationToken)
        where TNext : struct, INext
    {
        Console.WriteLine($"[Publishing] {typeof(TNotification).Name}");
        await next.InvokeAsync().ConfigureAwait(false);
        Console.WriteLine($"[Published] {typeof(TNotification).Name}");
    }
}
```
