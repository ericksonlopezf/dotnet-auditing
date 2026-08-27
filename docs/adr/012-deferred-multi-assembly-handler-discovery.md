# ADR-012: DEFERRED — Multi-Assembly Handler Discovery

**Status**: Deferred to v1.x (Experimental)

### Context
In large solutions, handlers may be split across multiple projects/assemblies.

### Problem
The Roslyn Incremental Generator only sees the compilation unit it's analyzing. It cannot see handlers in referenced assemblies.

### Options Considered
1. Re-run generator per assembly and combine — complex, potential conflicts
2. Explicit registration API for external handlers — breaks "generated registration"
3. Generator analyzes referenced assemblies via compilation — feasible but complex
4. **Deferred to post-MVP** — current use case is mono-project

### Decision
**DEFERRED**. Not in v1.0. Document the limitation. In v1.x, investigate `context.MetadataReferencesProvider` to discover handlers in referenced assemblies.

### Reconsideration Criteria
If multiple users report this as a blocker, implement in v1.1 with an experimental flag.

---

