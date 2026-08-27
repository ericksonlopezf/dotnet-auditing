# Best Practices — EricksonLopez.Mediator

A reference guide for writing correct, performant, and maintainable code with `EricksonLopez.Mediator`.

---

## 1. CQRS Contract Design

### DO: Use strict ICommand / IQuery segregation

```csharp
// Correct: Explicit intent
public sealed record CreateOrderCommand(string CustomerId, decimal Total) : ICommand<OrderResult>;
public sealed record GetOrderByIdQuery(Guid OrderId) : IQuery<OrderDto?>;
```

### DON'T: Use ICommand for read operations

```csharp
// Wrong: Commands must mutate state, not read it
public sealed record GetUserCommand(Guid UserId) : ICommand<UserDto>; // BAD
```

---

## 2. Handler Design

### DO: Keep handlers focused on a single responsibility

```csharp
// Correct: Handler delegates cross-cutting concerns to behaviors
public sealed class CreateOrderCommandHandler : ICommandHandler<CreateOrderCommand, OrderResult>
{
    private readonly IOrderRepository _repo;

    public CreateOrderCommandHandler(IOrderRepository repo) => _repo = repo;

    public async ValueTask<OrderResult> Handle(CreateOrderCommand command, CancellationToken ct)
    {
        var order = Order.Create(command.CustomerId, command.Total);
        await _repo.AddAsync(order, ct);
        return new OrderResult(order.Id, "Created");
    }
}
```

### DON'T: Embed validation, logging, or retry logic inside handlers

Cross-cutting concerns belong in IPipelineBehavior<TRequest, TResponse> — not in the handler itself.

---

## 3. Pipeline Behavior Order

### DO: Assign explicit, unique order values

```csharp
[assembly: UseGlobalBehavior(typeof(TracingBehavior<,>), order: 0)]    // Outermost
[assembly: UseGlobalBehavior(typeof(LoggingBehavior<,>), order: 1)]    // Second
[assembly: UseGlobalBehavior(typeof(ValidationBehavior<,>), order: 2)] // Inner
```

Order semantics: **lower = outermost** (first to receive, last to return).

### DON'T: Duplicate order values

Two behaviors with the same order produce ELM008 and nondeterministic execution.

---

## 4. Struct Pipeline Constraints

### DO: Always constrain the TNext generic parameter

```csharp
public sealed class MyBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
{
    public async ValueTask<TResponse> Handle<TNext>(
        TRequest request, TNext next, CancellationToken cancellationToken)
        where TNext : struct, INext<TResponse> // <- REQUIRED for zero-allocation
    {
        // ...
        return await next.InvokeAsync().ConfigureAwait(false);
    }
}
```

### DON'T: Store TNext as an interface variable

```csharp
// Wrong: boxes the struct onto the heap
INext<TResponse> boxed = next; // BAD — defeats zero-allocation design
return await boxed.InvokeAsync();
```

---

## 5. Handler Lifetimes

### DO: Use Singleton for stateless handlers

```csharp
[ServiceLifetime(HandlerLifetime.Singleton)]
public sealed class StatelessCalculationHandler : ICommandHandler<CalculateCommand, int>
{
    // No instance state — safe for Singleton
    public ValueTask<int> Handle(CalculateCommand cmd, CancellationToken ct)
        => new(cmd.X + cmd.Y);
}
```

### DO: Use Scoped for handlers with DbContext dependencies

```csharp
[ServiceLifetime(HandlerLifetime.Scoped)]
public sealed class CreateProductHandler : ICommandHandler<CreateProductCommand, Guid>
{
    private readonly AppDbContext _db; // Scoped — correct per-request lifetime
    public CreateProductHandler(AppDbContext db) => _db = db;
    //...
}
```

---

## 6. Notification Design

### DO: Choose PublishStrategy deliberately

```csharp
// High-throughput fan-out: all handlers run concurrently
[PublishStrategy(PublishStrategy.Parallel)]
public sealed record OrderShippedEvent(Guid OrderId) : INotification;

// Critical path: collect all failures, never short-circuit
[PublishStrategy(PublishStrategy.SequentialAggregateExceptions)]
public sealed record PaymentFailedEvent(Guid OrderId) : INotification;
```

### DO: Handle NotificationHandlerAggregateException when using SequentialAggregateExceptions

```csharp
try
{
    await publisher.Publish(new PaymentFailedEvent(orderId), ct);
}
catch (NotificationHandlerAggregateException aggEx)
{
    foreach (var ex in aggEx.HandlerExceptions)
        logger.LogError(ex, "Notification handler failed.");
}
```

---

## 7. Validation

### DO: Apply [ValidateRequest] early, close to the request definition

```csharp
[ValidateRequest]
public sealed record CreateAccountCommand(
    [property: ValidateNotEmpty] string Username,
    [property: ValidateRegex(".+@.+")] string Email,
    [property: ValidateLength(8, 128)] string Password) : ICommand<bool>;
```

### DON'T: Mix [ValidateRequest] with IResultFactory<T> without null checks

If IResultFactory<TResponse> is not registered in DI, the behavior falls back to throwing MediatorValidationException. Always ensure the factory is registered or guard accordingly.

---

## 8. Testing

### DO: Use FakeMediator for service-level unit tests

```csharp
var fake = new FakeMediator();
fake.SetupCommand<PlaceOrderCommand, OrderResult>(cmd =>
    new OrderResult(Guid.NewGuid(), "Confirmed"));

var service = new CheckoutService(fake);
await service.CheckoutAsync(new CartDto());

fake.ShouldHaveReceived<PlaceOrderCommand>(c => c.CustomerId == "CUST-001");
```

### DO: Use DelegateNext<T> for isolated behavior unit tests

```csharp
var behavior = new ValidationBehavior<CreateUserCommand, bool>();
var next = new DelegateNext<bool>(true); // constant result stub
var request = new CreateUserCommand("alice", "alice@example.com");

var result = await behavior.Handle(request, next, CancellationToken.None);
Assert.True(result);
```

---

## 9. StaticMediator

### DO: Call Reset() between test cases when using StaticMediator

```csharp
[Fact]
public async Task Test_Serverless_Handler()
{
    StaticMediator.Reset();
    StaticMediator.RegisterCommandHandler(new MyHandler());
    var result = await StaticMediator.SendCommand<MyCommand, MyResult>(new MyCommand());
    Assert.NotNull(result);
}
```

### DON'T: Use StaticMediator in DI-enabled environments

StaticMediator bypasses scoped lifetime, DbContext management, and IServiceProvider. For DI environments, inject IMediator, ISender, or IPublisher.

---

## 10. Native AOT

### DO: Publish with PublishAot=true and test before deployment

```xml
<PropertyGroup>
  <PublishAot>true</PublishAot>
</PropertyGroup>
```

### DON'T: Add any runtime reflection inside handlers or behaviors

Any call to Type.GetType(), Assembly.GetTypes(), Activator.CreateInstance(), or MakeGenericMethod() inside a handler or behavior will fail under Native AOT trimming.
