# Level 08: Customization & Extension Points

`EricksonLopez.Mediator` offers customizable extension points to adapt to specialized enterprise architectures.

---

## 1. Segregated Interfaces (`ISender` & `IPublisher`)

Rather than injecting the full `IMediator` everywhere, adhere to the Interface Segregation Principle by injecting only what is required:
- **`ISender`**: Exposes `Send` and `CreateStream` (for commands, queries, and stream requests).
- **`IPublisher`**: Exposes `Publish` (for broadcasting notifications).

```csharp
public sealed class OrderService
{
    private readonly ISender _sender;
    private readonly IPublisher _publisher;

    public OrderService(ISender sender, IPublisher publisher)
    {
        _sender = sender;
        _publisher = publisher;
    }

    public async Task ProcessAsync(CreateOrderCommand command, CancellationToken ct)
    {
        var orderId = await _sender.Send(command, ct);
        await _publisher.Publish(new OrderProcessedNotification(orderId), ct);
    }
}
```

---

## 2. Notification Execution Strategies

Customize how domain events are dispatched by specifying the strategy directly on the `INotification` declaration:

```csharp
using EricksonLopez.Mediator;

// Parallel execution via Task.WhenAll
[PublishStrategy(PublishStrategy.Parallel)]
public sealed record CacheInvalidationNotification(string Key) : INotification;

// Sequential execution with aggregate exception collection
[PublishStrategy(PublishStrategy.SequentialAggregateExceptions)]
public sealed record AuditTrailNotification(string Action, string User) : INotification;
```

---

## 3. Streaming Requests (`IStreamRequest<T>`)

For processing large data sets or event streams without loading full collections into memory:

```csharp
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using EricksonLopez.Mediator;

public sealed record StreamSensorDataQuery(string DeviceId) : IStreamRequest<SensorReading>;

public sealed class StreamSensorDataQueryHandler : IStreamRequestHandler<StreamSensorDataQuery, SensorReading>
{
    public async IAsyncEnumerable<SensorReading> Handle(
        StreamSensorDataQuery request, 
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        for (int i = 0; i < 100; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return new SensorReading(request.DeviceId, 20.0 + i * 0.1, DateTime.UtcNow);
        }
    }
}
```
