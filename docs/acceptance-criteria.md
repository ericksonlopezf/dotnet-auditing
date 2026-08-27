# Acceptance Criteria & Invariant Verification

> **Ecosystem:** `EricksonLopez.Mediator`  
> **Engineering Specification:** Target: .NET 10 / C# 14 · Architecture: CQRS In-Process Mediator / Native AOT  
> **Status:** 100% Verified against 510+ Automated Test Cases

---

## 1. Mediator & CQRS Acceptance Criteria

### AC-01: Zero Allocation for Compile-Time Pipeline Dispatch
- **Requirement:** Invoking `ISender.Send` or `ISender.SendCommand` through an 8-stage middleware pipeline (`IPipelineBehavior`) must allocate **0 bytes** on the managed GC heap.
- **Verification:** Unit and contract tests asserting `MediatorContractExtensions.AssertZeroAllocations` with zero allocated bytes.

### AC-02: Compile-Time Handler Exhaustiveness (Zero Runtime Reflection)
- **Requirement:** Every `ICommand<TResponse>`, `IQuery<TResponse>`, and `IStreamQuery<TResponse>` declared in the solution must resolve to a valid compile-time handler. Missing handlers must fail compilation via `ELM001` or `ELM009`.
- **Verification:** Roslyn generator tests asserting diagnostic emission for unhandled request types.

### AC-03: Single Handler Invariance for Commands and Queries
- **Requirement:** Registering multiple handlers for the same single-target command or query must be rejected at compile time with `ELM002` or `ELM003`.
- **Verification:** Roslyn generator duplicate handler tests.

### AC-04: Resilient Streaming Query Dispatch (IAsyncEnumerable)
- **Requirement:** Streaming queries (`IStreamQuery<TResponse>`) must yield elements asynchronously without buffering the complete result stream in memory.
- **Verification:** Async streaming integration tests verifying immediate yield on element consumption.

---

## 2. Infrastructure & Tooling Acceptance Criteria

### AC-05: 100% Native AOT & Trimming Compatibility
- **Requirement:** The entire library and generated dispatch tables must publish cleanly under `PublishAot=true` with zero trimming warnings (`IL2026`, `IL3050`).
- **Verification:** `EricksonLopez.Mediator.AotSmokeTest` executes under Native AOT executable runner.

### AC-06: Strict Code Analysis (TreatWarningsAsErrors)
- **Requirement:** 100% build pass rate under `TreatWarningsAsErrors=true` and `AnalysisLevel=latest-recommended`.
- **Verification:** Verified via CI/CD `repo-compliance.yml` and `scripts/verify-compliance.ps1`.
