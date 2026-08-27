# ADR-024: Multi-Target net8.0, net9.0 and net10.0

**Status**: Accepted

### Context
Should EricksonLopez.Mediator target net8.0, net9.0, and/or net10.0?

### Decision
**Multi-target `net8.0`, `net9.0`, and `net10.0`** for the runtime package. **netstandard2.0** for the generator package.
Support for `net8.0` and `net9.0` will be maintained until .NET 8 reaches End of Support (November 10, 2026).

### Why
- .NET 8 (LTS) is supported until November 2026, and many enterprise environments are locked into LTS releases.
- .NET 9 (STS) is widely used, and providing a target eases the transition to .NET 10.
- While net10.0 has the most mature AOT toolchain, the API surface for the mediator is fully compatible with net8.0 and net9.0 without significant `tfm` constraints.
- The generator must remain netstandard2.0 for Roslyn host compatibility across all these versions.

### Reconsideration Criteria
When .NET 8 reaches End of Support in November 2026, drop `net8.0` and `net9.0` (which reaches EOL in May 2026) targets in the next major version (v2.0), making it exclusively `net10.0` or higher.
