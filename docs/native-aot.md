# Native AOT Compilation & Trimming Safety

## 1. Zero Reflection & Zero Trimming Warnings
`EricksonLopez.Mediator` is engineered for zero-reflection Native AOT execution:
- **Static Dispatch Generation**: Replaces `ActivatorUtilities` and dynamic `MethodInfo.Invoke` with compile-time generated switch/call statements.
- **Trimming Safe**: Preserves all handler types through direct Roslyn syntax tree references.
- **Zero Unreferenced Code Annotations**: Emits 0 `[RequiresUnreferencedCode]` warnings.
- **Verified Smoke Test**: Verified via `dotnet publish -p:PublishAot=true` in `EricksonLopez.Mediator.AotSmokeTest`.
