# ADR-007: Attribute-Based Behavior Configuration

**Status**: Accepted

### Context
MediatR configures behaviors exclusively through DI registration order, which is invisible at the call site and can be surprising. Wolverine uses convention scanning.

### Decision
Behaviors are declared using attributes:
- `[assembly: UseGlobalBehavior(typeof(ValidationBehavior<,>))]` — applies to all requests
- `[UseBehavior(typeof(AuditBehavior<,>))]` on a specific command/query class

The source generator reads these attributes and generates the pipeline composition for each handler.

### Why
- Behavior configuration is visible in code (at the assembly level and at the type level)
- Easier to audit: grep for `[UseBehavior]` to see all behavior assignments
- Generator can validate behavior types at compile time (ELM004, ELM007)
- Deterministic ordering without DI registration order dependency

### Consequences
+ Behaviors are visible in code, not hidden in DI setup
+ Generator can validate behavior types
+ Per-request behavior assignment is explicit
- `[UseGlobalBehavior]` ordering between multiple attributes needs explicit Order property (v1.1)
- More attributes in code (minor verbosity)

---

