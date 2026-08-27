# Cross-Project Reusability Matrix

| Component | Target Use Case | Reusability Scope |
|---|---|---|
| `IPipelineBehavior<TRequest, TResponse>` | Cross-cutting request interceptors | Universal across all assemblies |
| `INotificationBehavior<TNotification>` | Domain event interceptors | Universal across all assemblies |
| `MediatorContractExtensions` | Zero-allocation contract assertions | Test project scope |
