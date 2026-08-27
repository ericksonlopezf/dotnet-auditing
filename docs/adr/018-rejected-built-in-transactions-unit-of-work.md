# ADR-018: REJECTED — Built-in Transactions / Unit of Work

**Status**: Accepted (Rejection)

### Context
Some mediators or frameworks (Wolverine) provide automatic transaction management around handler execution.

### Decision
**REJECTED from core**. Transaction management belongs to the application layer.

### Why
- Transaction boundaries depend on the database (EF Core, Dapper, raw SQL)
- The mediator has no knowledge of the persistence layer
- A `TransactionBehavior<TRequest, TResponse>` using `IDbContextTransaction` is straightforward
- Automatic transactions can lead to unexpected long-running transactions if not carefully configured
- Coupling the mediator to a persistence abstraction violates the principle that the mediator is persistence-agnostic

### Ecosystem
This is where `EricksonLopez.Dapper.Extensions` or EF Core's `IUnitOfWork` pattern belongs. Not the mediator.

---

