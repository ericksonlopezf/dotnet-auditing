# Strategic Differentiation & Architecture Positioning

## 1. Core Differentiators

`EricksonLopez.Mediator` is built on four core architectural pillars:

### 1. Compile-Time Static Dispatch (Zero Reflection)
Eliminates runtime assembly scanning and reflection `Invoke`, compiling all point-to-point dispatch routes and generic behavior pipelines directly into static C# method tables.

### 2. Native AOT & Trimming-First Design
Every interface, handler, and pipeline continuation satisfies strict Native AOT trimming invariants with zero warnings.

### 3. Struct-Based Pipeline Engine (Zero Allocations)
Replaces allocating closure delegates (`Func<Task<T>>`) with value-type `struct INext<TResponse>` continuations, reducing pipeline memory overhead from 480 B to **0 B**.

### 4. Explicit CQRS Contracts
Segregates mutating commands (`ICommand<T>`) from side-effect-free queries (`IQuery<T>`) and reactive streams (`IStreamQuery<T>`).
