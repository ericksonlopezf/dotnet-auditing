# Level 00: Introduction to EricksonLopez.Mediator

Welcome to the `EricksonLopez.Mediator` showcase. This library provides a high-performance, Native AOT-compatible implementation of the Mediator pattern for .NET applications.

## The Mediator Pattern

The Mediator pattern defines an object that encapsulates how a set of objects interact. It promotes loose coupling by keeping objects from referring to each other explicitly, allowing you to vary their interaction independently.

In the context of modern .NET applications, this usually means your controllers, minimal APIs, or background services don't depend directly on the business logic classes (handlers). Instead, they depend on an `IMediator` interface, sending a request to the mediator, which then dynamically resolves and invokes the correct handler.

## Why CQRS?

Command Query Responsibility Segregation (CQRS) is a pattern that separates read and update operations for a data store. `EricksonLopez.Mediator` is inherently designed around CQRS principles:

- **Commands:** Represent an intent to mutate state (e.g., `CreateUserCommand`). They are routed to a single handler.
- **Queries:** Represent a request to read state (e.g., `GetUserByIdQuery`). They are also routed to a single handler but return data without side effects.
- **Events (Notifications):** Represent something that has occurred (e.g., `UserCreatedEvent`). They are broadcast to zero or multiple handlers.

## Why EricksonLopez.Mediator?

While there are other mediator libraries in the .NET ecosystem, `EricksonLopez.Mediator` is built with a focus on modern .NET paradigms:

1. **Native AOT Ready:** Unlike reflection-heavy alternatives, this library relies on modern source generators or highly optimized generic constraints, making it fully compatible with Native AOT deployments without trim warnings.
2. **Zero Allocations:** The hot path for dispatching messages is heavily optimized to minimize or eliminate heap allocations, reducing garbage collection pressure in high-throughput APIs.
3. **Pipeline Behaviors:** Full support for cross-cutting concerns (middleware) via pipeline behaviors, allowing you to easily inject validation, logging, and transaction management around your handlers.

Let's move on to **Level 01** to see how easy it is to get started.
