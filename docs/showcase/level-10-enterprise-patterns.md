# Level 10: Enterprise Patterns & Domain Events

`EricksonLopez.Mediator` serves as the messaging backbone for Domain-Driven Design (DDD) and Clean Architecture.

---

## 1. Clean Architecture Boundaries

In Clean Architecture, core Domain and Application layers have zero dependencies on infrastructure (EF Core, ASP.NET Core, cloud SDKs):
- **Domain Layer:** Defines entities, aggregates, and domain events (`INotification`).
- **Application Layer:** Defines commands (`ICommand<T>`), queries (`IQuery<T>`), and handlers (`ICommandHandler`, `IQueryHandler`).
- **Presentation Layer:** Injects `ISender` or `IPublisher` into minimal endpoints or controllers.

---

## 2. Dispatching Domain Events via `IPublisher`

Aggregates collect domain events as internal state changes. When committing the Unit of Work, these events are published via `IPublisher`:

### The Aggregate Root
```csharp
using System.Collections.Generic;
using EricksonLopez.Mediator;

public abstract class AggregateRoot
{
    private readonly List<INotification> _domainEvents = new();
    public IReadOnlyCollection<INotification> DomainEvents => _domainEvents.AsReadOnly();

    protected void RaiseDomainEvent(INotification domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }

    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }
}

public sealed class Order : AggregateRoot
{
    public Guid Id { get; private set; }
    public decimal TotalAmount { get; private set; }

    public void Complete()
    {
        // Mutate aggregate state
        // ...
        RaiseDomainEvent(new OrderCompletedNotification(Id));
    }
}
```

### Publishing in the Unit of Work / DbContext
```csharp
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.Mediator;
using Microsoft.EntityFrameworkCore;

public sealed class AppDbContext : DbContext
{
    private readonly IPublisher _publisher;

    public AppDbContext(DbContextOptions<AppDbContext> options, IPublisher publisher)
        : base(options)
    {
        _publisher = publisher;
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // 1. Commit database transaction
        var result = await base.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // 2. Extract and clear all pending domain events
        var domainEntities = ChangeTracker
            .Entries<AggregateRoot>()
            .Where(x => x.Entity.DomainEvents.Any())
            .ToList();

        var events = domainEntities
            .SelectMany(x => x.Entity.DomainEvents)
            .ToList();

        domainEntities.ForEach(entity => entity.Entity.ClearDomainEvents());

        // 3. Publish domain events to subscribers
        foreach (var domainEvent in events)
        {
            await _publisher.Publish(domainEvent, cancellationToken).ConfigureAwait(false);
        }

        return result;
    }
}
```
