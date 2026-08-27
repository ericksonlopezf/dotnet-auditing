# Level 02: Configuration & Service Lifetimes

`EricksonLopez.Mediator` relies on **compile-time attribute configuration** and **Roslyn Incremental Source Generators** to establish routing and DI registration.

## 1. Zero-Configuration Container Registration

Because handlers are discovered at compile time, container registration requires only a single line:

```csharp
using Microsoft.Extensions.DependencyInjection;
using EricksonLopez.Mediator;

var builder = WebApplication.CreateBuilder(args);

// Registers IMediator, ISender, IPublisher, and all generated handlers
builder.Services.AddEricksonLopezMediator();
```

## 2. Multi-Assembly Discovery (`[DiscoverHandlers]`)

By default, the Source Generator processes types within the current compilation. If your commands, queries, or handlers reside in separate class libraries (e.g., `MyApp.Application`, `MyApp.Domain`), use the `[DiscoverHandlers]` assembly attribute:

```csharp
using EricksonLopez.Mediator;

// Add to your main entry point (Program.cs or AssemblyInfo.cs):
[assembly: DiscoverHandlers(typeof(MyApp.Application.Users.CreateUserCommand))]
[assembly: DiscoverHandlers(typeof(MyApp.Infrastructure.DataMarker))]
```

The generator inspects the referenced assemblies and seamlessly incorporates external handlers into the generated `GeneratedMediator` dispatch table.

## 3. Configuring Service Lifetimes (`[ServiceLifetime]`)

By default, all handlers and behaviors are registered with `Transient` lifetime. You can customize the DI lifetime on any handler or behavior class using the `[ServiceLifetime]` attribute:

```csharp
using EricksonLopez.Mediator;

// 1. Scoped Handler (ideal for EF Core DbContext or Unit of Work)
[ServiceLifetime(HandlerLifetime.Scoped)]
public sealed class GetUserByIdQueryHandler : IQueryHandler<GetUserByIdQuery, UserDto?>
{
    private readonly AppDbContext _db;
    public GetUserByIdQueryHandler(AppDbContext db) => _db = db;

    public async ValueTask<UserDto?> Handle(GetUserByIdQuery query, CancellationToken ct)
    {
        var user = await _db.Users.FindAsync(new object[] { query.UserId }, ct);
        return user is null ? null : new UserDto(user.Id, user.Name);
    }
}

// 2. Singleton Handler (ideal for stateless computations, caches, in-memory catalogs)
[ServiceLifetime(HandlerLifetime.Singleton)]
public sealed class CalculateTaxCommandHandler : ICommandHandler<CalculateTaxCommand, decimal>
{
    public ValueTask<decimal> Handle(CalculateTaxCommand command, CancellationToken ct)
    {
        return new ValueTask<decimal>(command.Subtotal * 0.15m);
    }
}
```

| Lifetime | Registration | Typical Use Case |
|---|---|---|
| `HandlerLifetime.Transient` *(Default)* | `services.AddTransient<...>()` | Handlers with per-operation transient dependencies |
| `HandlerLifetime.Scoped` | `services.AddScoped<...>()` | Handlers injecting `DbContext`, tenant contexts, or scoped units of work |
| `HandlerLifetime.Singleton` | `services.AddSingleton<...>()` | Pure functions, stateless algorithms, in-memory caches |

## 4. Pipeline Behavior Configuration

Pipeline behaviors are declared using attributes:
- **Global Behaviors**: Wrap every request across the assembly:
  ```csharp
  [assembly: UseGlobalBehavior(typeof(LoggingBehavior<,>), order: 1)]
  [assembly: UseGlobalBehavior(typeof(MetricsBehavior<,>), order: 2)]
  ```
- **Per-Request Behaviors**: Wrap specific commands or queries:
  ```csharp
  [UseBehavior(typeof(ValidationBehavior<,>), order: 3)]
  public sealed record ProcessPaymentCommand(decimal Amount) : ICommand<PaymentResult>;
  ```
