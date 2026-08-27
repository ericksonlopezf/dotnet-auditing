# Cookbook — Practical Recipes for EricksonLopez.Mediator

A curated collection of battle-tested recipes solving common architectural and enterprise challenges using `EricksonLopez.Mediator`.

---

## Recipe 1: Type-Safe Result Pattern Validation Short-Circuiting

### Problem
You want validation failures to return a typed `Result<T>` failure without throwing expensive exceptions and without reflection overhead.

### Solution
Use `IResultFactory<TResponse>` inside an `IPipelineBehavior<TRequest, TResponse>`:

> **Required package:** `EricksonLopez.Result` provides the `Error` and `Result<T>` types used in this recipe.
> ```bash
> dotnet add package EricksonLopez.Mediator.Result
> dotnet add package EricksonLopez.Result
> ```

```csharp
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.Mediator;
using EricksonLopez.Mediator.Result;
using EricksonLopez.Result;        // Error, Result<T> — from EricksonLopez.Result package

public sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
{
    private readonly IResultFactory<TResponse>? _resultFactory;

    public ValidationBehavior(IResultFactory<TResponse>? resultFactory = null)
    {
        _resultFactory = resultFactory;
    }

    public ValueTask<TResponse> Handle<TNext>(TRequest request, TNext next, CancellationToken cancellationToken)
        where TNext : struct, INext<TResponse>
    {
        if (request is CreateProductCommand cmd && string.IsNullOrWhiteSpace(cmd.Sku))
        {
            if (_resultFactory is not null)
            {
                var error = Error.Validation("Product.InvalidSku", "SKU cannot be empty.");
                return new ValueTask<TResponse>(_resultFactory.CreateFailure(error));
            }
        }

        return next.InvokeAsync();
    }
}
```


---

## Recipe 2: Polly v8 Resilience Integration (Retry & Circuit Breaker)

### Problem
External API or database calls wrapped in command handlers fail intermittently due to network glitches.

### Solution
Configure a resilience pipeline and apply `[UseResiliencePipeline]`:

```csharp
// Program.cs
services.AddMediatorDefaultResiliencePipeline(builder =>
{
    builder.AddRetry(new Polly.Retry.RetryStrategyOptions
    {
        MaxRetryAttempts = 3,
        Delay = TimeSpan.FromMilliseconds(50),
        BackoffType = Polly.DelayBackoffType.Exponential
    });
});

// Command.cs
[UseResiliencePipeline("Default")]
public sealed record SyncInventoryCommand(string WarehouseId) : ICommand<bool>;
```

---

## Recipe 3: Rate Limiting Commands with Token Bucket

### Problem
Prevent service degradation under heavy load by restricting command throughput per client.

### Solution
Register `RateLimitingBehavior` with `System.Threading.RateLimiting`:

```csharp
// Service registration
services.AddSingleton<RateLimiter>(_ => new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
{
    TokenLimit = 100,
    TokensPerPeriod = 50,
    ReplenishmentPeriod = TimeSpan.FromSeconds(1),
    QueueLimit = 10
}));

services.AddMediatorRateLimiting();
```

---

## Recipe 4: OpenTelemetry Distributed Tracing & Metrics

### Problem
Export standard OpenTelemetry traces and latency histograms for all commands and queries.

### Solution
Register `AddMediatorOpenTelemetry` and configure trace exporter:

```csharp
services.AddMediatorOpenTelemetry(options =>
{
    options.ActivitySourceName = "MyEnterpriseApp.Mediator";
    options.EnrichActivity = (activity, request) =>
    {
        activity.SetTag("tenant.id", "tenant-alpha");
    };
});
```

---

## Recipe 5: Concurrency & Exception Aggregation in Domain Events

### Problem
When publishing an event to multiple subscribers, all handlers must run concurrently, and any failed handlers must have their exceptions aggregated.

### Solution
Use `[PublishStrategy(PublishStrategy.SequentialAggregateExceptions)]` or `[PublishStrategy(PublishStrategy.Parallel)]`:

```csharp
[PublishStrategy(PublishStrategy.SequentialAggregateExceptions)]
public sealed record UserDeactivatedEvent(Guid UserId) : INotification;

// Caller handling:
try
{
    await mediator.Publish(new UserDeactivatedEvent(userId), cancellationToken);
}
catch (NotificationHandlerAggregateException aggEx)
{
    foreach (var inner in aggEx.HandlerExceptions)
    {
        logger.LogError(inner, "Notification subscriber failed.");
    }
}
```

---

## Recipe 6: Clean Minimal APIs with MapCommand & MapQuery

### Problem
Expose CQRS commands and queries directly as HTTP endpoints without manual controller boilerplate.

### Solution
Use `MediatorEndpointRouteBuilderExtensions`:

```csharp
var app = builder.Build();

app.MapCommand<CreateOrderCommand, OrderResponse>("/api/orders");
app.MapQuery<GetOrderByIdQuery, OrderDto>("/api/orders/{id}");

app.Run();
```

---

## Recipe 7: Reactive Asynchronous Streaming with IStreamRequest

### Problem
Stream thousands of records in chunks from a database to an HTTP client without memory buffers.

### Solution
Implement `IStreamRequestHandler`:

```csharp
public sealed record StreamAuditLogsQuery(DateTime FromUtc) : IStreamRequest<AuditLogDto>;

public sealed class StreamAuditLogsQueryHandler : IStreamRequestHandler<StreamAuditLogsQuery, AuditLogDto>
{
    public async IAsyncEnumerable<AuditLogDto> Handle(
        StreamAuditLogsQuery request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var record in _db.GetRecordsAsync(request.FromUtc, cancellationToken))
        {
            yield return record;
        }
    }
}

// Caller:
await foreach (var item in mediator.CreateStream(new StreamAuditLogsQuery(DateTime.UtcNow.AddDays(-1))))
{
    Console.WriteLine(item);
}
```

---

## Recipe 8: Fast AOT Unit Testing with FakeMediator

### Problem
Write fast unit tests for service classes without Moq, NSubstitute, or reflection-based proxy generators.

### Solution
Use `FakeMediator`:

```csharp
[Fact]
public async Task CheckoutService_Should_Dispatch_Order()
{
    // Arrange
    var fakeMediator = new FakeMediator();
    fakeMediator.SetupCommand<PlaceOrderCommand, OrderResult>(cmd => new OrderResult("ORD-123"));

    var service = new CheckoutService(fakeMediator);

    // Act
    await service.CheckoutAsync(new CartDto());

    // Assert
    fakeMediator.ShouldHaveReceived<PlaceOrderCommand>();
    fakeMediator.ReceivedCount<PlaceOrderCommand>().Should().Be(1);
}
```

---

## Recipe 9: Zero-DI Serverless Execution with StaticMediator

### Problem
Run CQRS commands in AWS Lambda or Cloudflare Workers where DI container instantiation is avoided for sub-millisecond cold starts.

### Solution
Use `StaticMediator`:

```csharp
public static class FunctionEntrypoint
{
    static FunctionEntrypoint()
    {
        StaticMediator.RegisterCommandHandler(new ProcessServerlessOrderCommandHandler());
    }

    public static async Task<string> Run(string payload)
    {
        return await StaticMediator.SendCommand<ProcessServerlessOrderCommand, string>(
            new ProcessServerlessOrderCommand(payload));
    }
}
```

---

## Recipe 10: Kubernetes Health Checks Readiness Probes

### Problem
Ensure Kubernetes traffic is only routed when the mediator dispatch engine is ready.

### Solution
Register `AddMediatorHealthCheck`:

```csharp
services.AddHealthChecks()
    .AddCheck<MediatorHealthCheck>("mediator_dispatch");

services.AddMediatorHealthCheck();

app.MapHealthChecks("/healthz");
```
