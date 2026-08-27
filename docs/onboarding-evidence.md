# Developer Onboarding & Verification Evidence

## 1. Quick Onboarding Verification
To verify the local development environment and run the full test suite:

```powershell
# 1. Restore and verify repository compliance
pwsh -File ./scripts/verify-compliance.ps1

# 2. Build in Release mode with strict diagnostics
dotnet build EricksonLopez.Mediator.slnx -c Release

# 3. Execute all 510+ automated unit & integration tests
dotnet test EricksonLopez.Mediator.slnx -c Release

# 4. Pack all packages
dotnet pack EricksonLopez.Mediator.slnx -c Release -o artifacts/
```
