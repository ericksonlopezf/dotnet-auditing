# Rejected Features & Architectural Trade-Offs

## 1. Discarded Features
1. **Dynamic Reflection Dispatch**: Discarded in favor of compile-time static dispatch to ensure 100% Native AOT compatibility.
2. **Built-in Distributed Message Broker Transport**: Discarded to keep mediator strictly in-process and lightweight (< 60 KB).
3. **Implicit Exception Catching**: Discarded to enforce functional error propagation via `IResultFactory<TResponse>`.
