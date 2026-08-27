# QuickStart Guide: 5 Minutes to High-Performance Mediation

## 1. Install Package
```bash
dotnet add package EricksonLopez.Mediator
```

## 2. Define Command and Handler
```csharp
using EricksonLopez.Mediator;

public readonly record struct CreateOrderCommand(Guid OrderId, decimal Total) : ICommand<Guid>;

public sealed class CreateOrderCommandHandler : ICommandHandler<CreateOrderCommand, Guid>
{
    public ValueTask<Guid> Handle(CreateOrderCommand command, CancellationToken cancellationToken)
    {
        // Business logic execution
        return ValueTask.FromResult(command.OrderId);
    }
}
```

## 3. Register and Dispatch
```csharp
var services = new ServiceCollection();
services.AddMediator(); // Compile-time generated registration

var provider = services.BuildServiceProvider();
var sender = provider.GetRequiredService<ISender>();

var orderId = await sender.SendCommand(new CreateOrderCommand(Guid.NewGuid(), 99.99m));
```
