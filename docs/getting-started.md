# Getting Started — EricksonLopez.Mediator

A step-by-step guide to set up and run your first commands, queries, and notifications.

---

## Prerequisites

- .NET 8.0 SDK or higher
- Visual Studio 2022 / Rider / VS Code with C# extension

---

## Step 1: Install Packages

Add the core library and the Roslyn Source Generator to your project:

```bash
dotnet add package EricksonLopez.Mediator
dotnet add package EricksonLopez.Mediator.Generator --version <same> 
```

In your .csproj, mark the Generator as an Analyzer:

```xml
<ItemGroup>
  <PackageReference Include="EricksonLopez.Mediator" Version="1.0.0" />
  <PackageReference Include="EricksonLopez.Mediator.Generator" Version="1.0.0"
                    OutputItemType="Analyzer"
                    ReferenceOutputAssembly="false" />
</ItemGroup>
```

---

## Step 2: Register the Mediator in DI

The Source Generator produces a `AddEricksonLopezMediator()` extension method that registers all handlers and behaviors automatically:

```csharp
using EricksonLopez.Mediator;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddEricksonLopezMediator(); // Generated at compile time
var provider = services.BuildServiceProvider();
```

---

## Step 3: Define Your First Command

A command represents a state-mutating write operation:

```csharp
using EricksonLopez.Mediator;
using System.Threading;
using System.Threading.Tasks;

// Contract
public sealed record CreateUserCommand(string Username, string Email) : ICommand<string>;

// Handler
public sealed class CreateUserCommandHandler : ICommandHandler<CreateUserCommand, string>
{
    public ValueTask<string> Handle(CreateUserCommand command, CancellationToken cancellationToken)
    {
        var userId = Guid.NewGuid().ToString();
        Console.WriteLine($"User '{command.Username}' created with ID: {userId}");
        return ValueTask.FromResult(userId);
    }
}
```

---

## Step 4: Dispatch the Command

```csharp
var mediator = provider.GetRequiredService<IMediator>();

// Option A: Polymorphic dispatch (resolves handler at compile time from type-switch)
var userId = await mediator.Send(new CreateUserCommand("alice", "alice@example.com"));

// Option B: Strongly typed dispatch (no boxing)
var userId2 = await mediator.SendCommand<CreateUserCommand, string>(
    new CreateUserCommand("bob", "bob@example.com"));

Console.WriteLine($"Created: {userId}, {userId2}");
```

---

## Step 5: Define Your First Query

A query represents a pure, idempotent read:

```csharp
// Contract
public sealed record GetUserByIdQuery(Guid UserId) : IQuery<UserDto?>;
public sealed record UserDto(Guid Id, string Username, string Email);

// Handler
public sealed class GetUserByIdQueryHandler : IQueryHandler<GetUserByIdQuery, UserDto?>
{
    public ValueTask<UserDto?> Handle(GetUserByIdQuery query, CancellationToken cancellationToken)
    {
        // In real code: return await _db.Users.FindAsync(query.UserId);
        var user = new UserDto(query.UserId, "alice", "alice@example.com");
        return ValueTask.FromResult<UserDto?>(user);
    }
}
```

---

## Step 6: Dispatch the Query

```csharp
ISender sender = mediator; // ISender is the segregated interface for commands + queries

var user = await sender.SendQuery<GetUserByIdQuery, UserDto?>(
    new GetUserByIdQuery(Guid.NewGuid()));

Console.WriteLine($"User: {user?.Username}");
```

---

## Step 7: Define and Publish a Notification

Notifications (domain events) fan out to multiple handlers:

```csharp
// Notification contract
public sealed record UserRegisteredEvent(Guid UserId, string Email) : INotification;

// Handler 1
public sealed class SendWelcomeEmailHandler : INotificationHandler<UserRegisteredEvent>
{
    public ValueTask Handle(UserRegisteredEvent evt, CancellationToken ct)
    {
        Console.WriteLine($"Sending welcome email to {evt.Email}");
        return ValueTask.CompletedTask;
    }
}

// Handler 2
public sealed class CreateUserAuditHandler : INotificationHandler<UserRegisteredEvent>
{
    public ValueTask Handle(UserRegisteredEvent evt, CancellationToken ct)
    {
        Console.WriteLine($"Audit: User {evt.UserId} registered");
        return ValueTask.CompletedTask;
    }
}
```

Publish:

```csharp
IPublisher publisher = mediator;
await publisher.Publish(new UserRegisteredEvent(Guid.NewGuid(), "alice@example.com"));
```

---

## Step 8: Add a Pipeline Behavior

Behaviors are middleware that intercept all (or specific) requests:

```csharp
using EricksonLopez.Mediator;

[assembly: UseGlobalBehavior(typeof(LoggingBehavior<,>), order: 1)]

public sealed class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
{
    public async ValueTask<TResponse> Handle<TNext>(
        TRequest request, TNext next, CancellationToken cancellationToken)
        where TNext : struct, INext<TResponse>
    {
        Console.WriteLine($"[Before] {typeof(TRequest).Name}");
        var response = await next.InvokeAsync().ConfigureAwait(false);
        Console.WriteLine($"[After] {typeof(TRequest).Name}");
        return response;
    }
}
```

---

## Step 9: Add Declarative Validation

Annotate request properties to enable compile-time generated validation:

```csharp
[ValidateRequest]
public sealed record CreateUserCommand(
    [property: ValidateNotEmpty] string Username,
    [property: ValidateRegex(".+@.+")] string Email) : ICommand<string>;
```

If validation fails, MediatorValidationException is thrown automatically before the handler executes.

---

## Step 10: Run the Official Showcase

The `samples/Sample` project demonstrates all API features from Level 0 (conceptual) to Level 11 (testing):

```bash
cd samples/Sample
dotnet run
```

Expected output:

```
================================================================================
  LEVEL 0: CONCEPTUAL FOUNDATIONS OF ERICKSONLOPEZ.MEDIATOR
================================================================================
...
================================================================================
  ✓ ALL SHOWCASE LEVELS EXECUTED SUCCESSFULLY (Levels 0-11)
================================================================================
```

---

## Next Steps

- Read the [API Reference](api-reference.md) for full method signatures
- Study the [Architecture Guide](architecture.md) to understand compile-time dispatch
- Follow the [Cookbook](cookbook.md) for production-ready recipes
- Review [Best Practices](best-practices.md) to avoid common mistakes
- Use the [Migration Guide](migration-guide.md) if migrating from MediatR
