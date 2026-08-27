# Level 11: Dependency Injection & Lifetimes

`EricksonLopez.Mediator` integrates directly with `Microsoft.Extensions.DependencyInjection` through compile-time code generation.

---

## 1. Compile-Time Registration Mechanism

When `builder.Services.AddEricksonLopezMediator()` is called:
1. `GeneratedMediator` is registered as a **Singleton** (`IMediator`, `ISender`, `IPublisher`).
2. The `IServiceProvider` is captured by `GeneratedMediator` to resolve scoped and transient handlers on demand.
3. Every discovered handler and behavior is registered in DI with its configured `[ServiceLifetime]`.

Because the Source Generator writes direct calls to `serviceProvider.GetRequiredService<THandler>()` inside `GeneratedMediator`, **zero reflection or runtime type scanning** occurs.

---

## 2. Lifetimes Overview

| Lifetime | Attribute | Behavior | Recommendation |
|---|---|---|---|
| **Transient** | `[ServiceLifetime(HandlerLifetime.Transient)]` *(Default)* | Created on every dispatch | Default choice for stateless CQRS handlers |
| **Scoped** | `[ServiceLifetime(HandlerLifetime.Scoped)]` | One instance per HTTP request scope | When injecting `DbContext` or scoped unit of work |
| **Singleton** | `[ServiceLifetime(HandlerLifetime.Singleton)]` | Created once at startup | Pure calculations, in-memory caches, stateless services |

---

## 3. Scoped Dispatching in Background Services

When dispatching requests inside long-running singleton services (such as `BackgroundService` or `IHostedService`), always create an explicit `IServiceScope` to avoid captive dependencies on scoped handlers or `DbContext`:

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.Mediator;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

public sealed class QueueWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;

    public QueueWorker(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // 1. Create an explicit scope per message batch:
            using (var scope = _serviceProvider.CreateScope())
            {
                var sender = scope.ServiceProvider.GetRequiredService<ISender>();
                
                // 2. Dispatch command (scoped handlers resolve safely within this scope)
                await sender.Send(new ProcessQueueItemCommand(), stoppingToken).ConfigureAwait(false);
            }

            await Task.Delay(1000, stoppingToken).ConfigureAwait(false);
        }
    }
}
```
