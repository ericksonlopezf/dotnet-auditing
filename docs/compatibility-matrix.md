# Compatibility Matrix — EricksonLopez.Mediator

This document outlines the compatibility between `EricksonLopez.Mediator` packages and supported Microsoft .NET Target Framework Monikers (TFMs).

---

## 1. Supported Framework Lifecycle

`EricksonLopez.Mediator` targets officially supported Microsoft .NET versions:

| Framework | Type | Support Lifecycle | Native AOT Trimming Support (Core) |
|---|---|---|---|
| **.NET 8.0** | LTS | Active | Full (Zero Reflection in Core, 0 Trim Warnings) |
| **.NET 9.0** | STS | Active | Full (Zero Reflection in Core, 0 Trim Warnings) |
| **.NET 10.0** | LTS | Active | Full (Zero Reflection in Core, 0 Trim Warnings) |

---

## 2. Package Target Frameworks & AOT Readiness

| Package | .NET 8.0 | .NET 9.0 | .NET 10.0 | .NET Standard 2.0 | Native AOT Ready |
|---|:---:|:---:|:---:|:---:|:---:|
| `EricksonLopez.Mediator` | ✅ | ✅ | ✅ | — | ✅ 100% |
| `EricksonLopez.Mediator.Generator` | — | — | — | ✅ | N/A (Roslyn Analyzer) |
| `EricksonLopez.Mediator.AspNetCore` | ✅ | ✅ | ✅ | — | ⚠️ Partial — `[RequiresUnreferencedCode]` on all public methods |
| `EricksonLopez.Mediator.OpenTelemetry` | ✅ | ✅ | ✅ | — | ✅ 100% |
| `EricksonLopez.Mediator.Polly` | ✅ | ✅ | ✅ | — | ⚠️ Generally compatible; preserve attribute metadata under trimming |
| `EricksonLopez.Mediator.RateLimiting` | ✅ | ✅ | ✅ | — | ✅ 100% |
| `EricksonLopez.Mediator.Result` | ✅ | ✅ | ✅ | — | ✅ 100% |
| `EricksonLopez.Mediator.Testing` | ✅ | ✅ | ✅ | — | Test-only Double |
| `EricksonLopez.Mediator.FluentValidation` | ✅ | ✅ | ✅ | — | ⚠️ Behavior AOT-safe; assembly scanning uses `[RequiresUnreferencedCode]` |
| `EricksonLopez.Mediator.Validation` | ✅ | ✅ | ✅ | — | ❌ DEPRECATED (ADR-033) — Not AOT compatible |

---

## 3. Tooling & Platform Compatibility

| Tooling / Platform | Supported Version | Notes |
|---|---|---|
| **Roslyn Compiler** | C# 12 / C# 13 / C# Latest | Generator uses `Microsoft.CodeAnalysis.CSharp 4.8.0` |
| **Operating Systems** | Windows, Linux, macOS | Tested via GitHub Actions runners |
| **AOT Cross-Compilation** | `linux-x64`, `win-x64`, `osx-arm64` | Fully validated with `PublishAot=true` |
| **Dependency Injection** | `Microsoft.Extensions.DependencyInjection 8.0+` | Monomorphized at compile time |
