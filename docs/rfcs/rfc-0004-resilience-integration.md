# RFC 0004: Native Polly V8 Resilience Strategy Integration

- **Author**: Erickson Lopez
- **Date**: 2026-08-26
- **Status**: Implemented

## 1. Summary
Integrates Microsoft Polly v8 resilience pipelines directly into mediator request execution through `EricksonLopez.Mediator.Polly`, enabling zero-overhead retries, circuit breaking, timeouts, and fallbacks.

## 2. Motivation
Distributed microservices require resilient out-of-process communications. Baking resilience strategies into dedicated mediator pipeline behaviors isolates transient fault tolerance from domain business logic.
