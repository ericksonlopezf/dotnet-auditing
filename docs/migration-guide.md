# Migration Guide: MediatR → EricksonLopez.Mediator

This guide helps you migrate an existing codebase from `MediatR` to `EricksonLopez.Mediator`.

---

## 1. Package References

Replace `MediatR` and `MediatR.Contracts` packages:

```xml
<!-- Before -->
<PackageReference Include="MediatR" Version="12.4.1" />

<!-- After -->
<PackageReference Include="EricksonLopez.Mediator" Version="1.0.0-rc1" />
<PackageReference Include="EricksonLopez.Mediator.Generator" Version="1.0.0-rc1" OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
```

---

## 2. Request Interfaces

In MediatR, requests implement `IRequest<TResponse>`. In `EricksonLopez.Mediator`, use explicit CQRS semantics:

| MediatR | EricksonLopez.Mediator |
|---|---|
| `IRequest<TResponse>` (Command) | `ICommand<TResponse>` |
| `IRequest<TResponse>` (Query) | `IQuery<TResponse>` |
| `IRequest` (Void Command) | `ICommand<Unit>` or `ICommand<Result>` |
| `INotification` | `INotification` |

---

## 3. Handler Signatures

Change `Task<TResponse> Handle(..., CancellationToken)` to return `ValueTask<TResponse>`:

```csharp
// Before (MediatR)
public class PingHandler : IRequestHandler<PingCommand, string>
{
    public Task<string> Handle(PingCommand request, CancellationToken cancellationToken)
    {
        return Task.FromResult("Pong");
    }
}

// After (EricksonLopez.Mediator)
public class PingHandler : ICommandHandler<PingCommand, string>
{
    public ValueTask<string> Handle(PingCommand command, CancellationToken cancellationToken)
    {
        return ValueTask.FromResult("Pong");
    }
}
```

---

## 4. Pipeline Behaviors

Replace `RequestHandlerDelegate<TResponse>` with the generic struct `TNext` constraint:

```csharp
// Before (MediatR)
public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        return await next();
    }
}

// After (EricksonLopez.Mediator)
public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
{
    public async ValueTask<TResponse> Handle<TNext>(TRequest request, TNext next, CancellationToken ct)
        where TNext : struct, INext<TResponse>
    {
        return await next.InvokeAsync().ConfigureAwait(false);
    }
}
```

---

## 5. Dependency Injection Registration

Replace `services.AddMediatR(cfg => ...)` with the source-generated extension:

```csharp
// Before (MediatR)
services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

// After (EricksonLopez.Mediator)
services.AddEricksonLopezMediator();
```
