# Level 01: Getting Started

This guide walks you through the basic installation, registration, and dispatching in `EricksonLopez.Mediator`.

## 1. Installation

Install the core package and the Roslyn Source Generator into your project:

```bash
dotnet add package EricksonLopez.Mediator
dotnet add package EricksonLopez.Mediator.Generator --output-item-type Analyzer
```

## 2. Compile-Time Registration

Register the Mediator in your dependency injection container (e.g., in `Program.cs`). The Roslyn Source Generator automatically discovers all handlers and emits the `AddEricksonLopezMediator()` extension method at compile time:

```csharp
using Microsoft.Extensions.DependencyInjection;
using EricksonLopez.Mediator;

var builder = WebApplication.CreateBuilder(args);

// Compile-time source generated registration (zero reflection, zero runtime scanning)
builder.Services.AddEricksonLopezMediator();

var app = builder.Build();
```

## 3. Creating Commands, Queries, and Handlers

In `EricksonLopez.Mediator`, Commands and Queries are strictly segregated in the type system:

```csharp
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.Mediator;

// 1. Query Definition (Read)
public sealed record GetGreetingQuery(string Name) : IQuery<string>;

// 2. Query Handler Definition
public sealed class GetGreetingQueryHandler : IQueryHandler<GetGreetingQuery, string>
{
    public ValueTask<string> Handle(GetGreetingQuery query, CancellationToken cancellationToken)
    {
        return new ValueTask<string>($"Hello, {query.Name}! Welcome to EricksonLopez.Mediator.");
    }
}

// 3. Command Definition (Write)
public sealed record RegisterUserCommand(string Username, string Email) : ICommand<Guid>;

// 4. Command Handler Definition
public sealed class RegisterUserCommandHandler : ICommandHandler<RegisterUserCommand, Guid>
{
    public ValueTask<Guid> Handle(RegisterUserCommand command, CancellationToken cancellationToken)
    {
        var newId = Guid.NewGuid();
        return new ValueTask<Guid>(newId);
    }
}
```

## 4. Dispatching Requests

Inject `IMediator` (or `ISender`) into your endpoints, controllers, or services:

```csharp
// In a Minimal API endpoint
app.MapGet("/greet/{name}", async (string name, IMediator mediator) =>
{
    var query = new GetGreetingQuery(name);
    var greeting = await mediator.Send(query);
    return Results.Ok(greeting);
});

app.MapPost("/users", async (RegisterUserCommand command, ISender sender) =>
{
    var userId = await sender.Send(command);
    return Results.Created($"/users/{userId}", new { Id = userId });
});
```

All routing is resolved via a compile-time monomorphized `switch` statement with **0 bytes allocated** on the dispatch path.
