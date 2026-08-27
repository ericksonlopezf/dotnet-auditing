# ADR-016: REJECTED — Built-in Authorization

**Status**: Accepted (Rejection)

### Context
Some mediators (Brighter) have built-in authorization policies. ASP.NET Core has its own authorization system.

### Decision
**REJECTED**. Authorization is handled via `IPipelineBehavior` by the user, optionally integrating with `IAuthorizationService` from Microsoft.Extensions.Authorization.

### Why
- Authorization is framework-specific (ASP.NET Core, gRPC, etc.)
- A generic authorization behavior in the mediator creates coupling to a specific authorization model
- Users can write `AuthorizationBehavior<TRequest, TResponse>` in 20 lines
- Adding authorization to the mediator would require marking requests as "require authorization" (IAuthorizedRequest), which pollutes the domain model

### Competitive Impact
Neutral. Not a differentiator. The behavior pattern covers this.

---

