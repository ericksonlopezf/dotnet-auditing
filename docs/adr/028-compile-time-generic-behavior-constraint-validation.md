# ADR-028: Compile-Time Generic Behavior Constraint Validation

**Status**: Accepted (Implemented in v1.0)

### Context
When developers create open generic pipeline behaviors with constraints (e.g. `ValidationBehavior<TRequest, TResponse> where TRequest : IValidatable`), closing the generic type on requests that do not satisfy the constraint can result in compiler errors or incorrect registration.

### Decision
`MediatorModelBuilder` evaluates Roslyn symbol constraints (`ConstraintTypes`) against the target `TRequest` and `TNotification` types during model construction. If a request does not satisfy the constraint, the behavior is automatically skipped for that request, ensuring clean compile-time dispatch without invalid type construction.

### Consequences
- Behaviors with type constraints only apply to compatible requests.
- Eliminates runtime casting errors and compilation failures.
