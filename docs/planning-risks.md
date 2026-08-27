# Planning Risks & Architectural Trade-Offs

## 1. Identified Risks & Architectural Mitigations
- **Risk:** Multi-assembly handler discovery requires explicit configuration in large enterprise solutions.
  - **Mitigation:** Compile-time `[assembly: DiscoverHandlers(typeof(Marker))]` attribute triggers Roslyn external syntax tree analysis.
- **Risk:** Missing compile-time handlers could cause developer friction if error messages are unclear.
  - **Mitigation:** Clear Roslyn diagnostic codes (`ELM001` - `ELM011`) with automated IDE code actions and rich documentation.
