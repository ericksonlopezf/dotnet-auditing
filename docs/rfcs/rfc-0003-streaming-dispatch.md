# RFC 0003: High-Throughput IAsyncEnumerable Streaming Dispatch

- **Author**: Erickson Lopez
- **Date**: 2026-08-26
- **Status**: Implemented

## 1. Summary
Provides first-class streaming query dispatch via `IStreamQuery<TResponse>` and `IStreamQueryHandler<TQuery, TResponse>`, yielding asynchronous response sequences without buffering complete datasets in memory.

## 2. Motivation
Large queries (e.g., historical telemetry, database cursor feeds) require reactive streaming to minimize memory footprint and enable backpressure handling.

## 3. Detailed Design
```csharp
public interface IStreamQuery<out TResponse> { }

public interface IStreamQueryHandler<in TQuery, out TResponse>
    where TQuery : IStreamQuery<TResponse>
{
    IAsyncEnumerable<TResponse> Handle(TQuery query, CancellationToken ct);
}
```
