# RFC 0002: Compile-Time Handler Discovery and Static Registration

- **Author**: Erickson Lopez
- **Date**: 2026-08-26
- **Status**: Implemented

## 1. Summary
Replaces runtime assembly reflection scanning (`Assembly.GetTypes()`) with incremental Roslyn source generation, generating direct dependency injection registrations and static dispatch tables at compile time.

## 2. Motivation
Assembly scanning adds substantial cold-start startup overhead (50ms - 300ms) and fails unpredictably under Native AOT compilation due to IL trimming.

## 3. Detailed Design
The Roslyn generator scans syntax trees for `IRequestHandler<,>`, `ICommandHandler<,>`, `IQueryHandler<,>`, and `INotificationHandler<>`, generating extension method `AddMediatorHandlers(this IServiceCollection services)`.
