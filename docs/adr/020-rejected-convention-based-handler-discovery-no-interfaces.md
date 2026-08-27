# ADR-020: REJECTED — Convention-Based Handler Discovery (No Interfaces)

**Status**: Accepted (Rejection)

### Context
Wolverine discovers handlers by convention: any public class with a `Handle(MyMessage)` method is a handler. No interfaces required.

### Decision
**REJECTED**. Interface-based handler discovery is mandatory.

### Why
- Convention-based discovery is "magic". The developer cannot know if a class is a handler without knowing the convention.
- "Explicit over magic" is a core philosophy of EricksonLopez.Mediator.
- Interface-based handlers are self-documenting: `ICommandHandler<CreateOrder, Result<OrderId>>` tells you exactly what the class does.
- The source generator works on interface detection, not naming conventions.
- Testing is simpler with interfaces (mock `ICommandHandler<,>` directly).

### Competitive Impact
This is a deliberate differentiation from Wolverine. We target developers who value explicitness over convenience.

---

