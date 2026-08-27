# Level 03: Strict CQRS (Commands & Queries)

In `EricksonLopez.Mediator`, Command Query Responsibility Segregation (CQRS) is strictly enforced in the type system at compile time (ADR-003).

Unlike legacy mediator libraries that use a single generic `IRequest<TResponse>` interface, `EricksonLopez.Mediator` distinguishes between mutating writes (`ICommand<T>`) and side-effect-free reads (`IQuery<T>`).

---

## 1. Defining Queries (`IQuery<TResponse>`)

Queries represent an intention to read data. They must be idempotent and produce no side effects.

```csharp
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.Mediator;

// 1. The Query Contract (Read)
public sealed record GetUserByIdQuery(Guid UserId) : IQuery<UserDto?>;

// 2. The DTO Record
public sealed record UserDto(Guid Id, string Username, string Email);

// 3. The Query Handler
[ServiceLifetime(HandlerLifetime.Scoped)]
public sealed class GetUserByIdQueryHandler : IQueryHandler<GetUserByIdQuery, UserDto?>
{
    private readonly ISqlConnectionFactory _db;

    public GetUserByIdQueryHandler(ISqlConnectionFactory db) => _db = db;

    public async ValueTask<UserDto?> Handle(GetUserByIdQuery query, CancellationToken cancellationToken)
    {
        // 1. Fetch data directly into DTO without side effects
        return await _db.QuerySingleOrDefaultAsync<UserDto>(
            "SELECT Id, Username, Email FROM Users WHERE Id = @UserId", 
            new { query.UserId }, 
            cancellationToken);
    }
}
```

---

## 2. Defining Commands (`ICommand<TResponse>`)

Commands represent an explicit intent to mutate application state.

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.Mediator;

// 1. The Command Contract (Write)
public sealed record CreateUserCommand(string Username, string Email) : ICommand<Guid>;

// 2. The Command Handler
[ServiceLifetime(HandlerLifetime.Scoped)]
public sealed class CreateUserCommandHandler : ICommandHandler<CreateUserCommand, Guid>
{
    private readonly IUserRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateUserCommandHandler(IUserRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async ValueTask<Guid> Handle(CreateUserCommand command, CancellationToken cancellationToken)
    {
        // 1. Instantiate aggregate root and mutate state
        var user = new User(Guid.NewGuid(), command.Username, command.Email);
        await _repository.AddAsync(user, cancellationToken).ConfigureAwait(false);
        
        // 2. Commit transaction
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        
        // 3. Return generated aggregate ID
        return user.Id;
    }
}
```

---

## 3. Compile-Time CQRS Safety

The Roslyn analyzer enforces strict single-handler CQRS invariants:
- **`ELM001`**: Compilation error if a command or query has no handler.
- **`ELM002`**: Compilation error if multiple handlers exist for the same `ICommand<T>`.
- **`ELM003`**: Compilation error if multiple handlers exist for the same `IQuery<T>`.

This prevents runtime routing ambiguities and ensures that your architecture is verified during build.
