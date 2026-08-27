# Contributing to EricksonLopez.Mediator

First off, thank you for considering contributing to EricksonLopez.Mediator!

## Code of Conduct
By participating in this project, you are expected to uphold our [Code of Conduct](CODE_OF_CONDUCT.md).

## Prerequisites
- .NET 10.0 SDK (includes runtimes for .NET 8.0, 9.0, and 10.0)

## Development Workflow

### Building the Solution
The repository uses the unified solution file `EricksonLopez.Mediator.slnx` with Central Package Management (CPM):

```bash
dotnet restore EricksonLopez.Mediator.slnx
dotnet build EricksonLopez.Mediator.slnx -c Release --no-restore
```

### Running Tests
To run the complete automated test suite across all target frameworks:

```bash
dotnet test EricksonLopez.Mediator.slnx -c Release --no-build
```

To run a specific test project:
```bash
dotnet test tests/EricksonLopez.Mediator.Tests/EricksonLopez.Mediator.Tests.csproj -c Release
```

### Native AOT Smoke Testing
Verify that all packages compile and run cleanly under Native AOT:

```bash
dotnet run -c Release --project tests/EricksonLopez.Mediator.AotTest/EricksonLopez.Mediator.AotTest.csproj
```

### Mutation Testing
Run Stryker.NET mutation testing locally:

```bash
# Full ecosystem mutation run
dotnet stryker --config-file stryker-config.json

# Fast core-only mutation run
dotnet stryker --config-file stryker-config-unit.json
```

### Benchmarks
Performance and allocation metrics are critical invariants. If you modify core dispatching, struct continuations, or generator pipelines, run the benchmarks:

```bash
dotnet run -c Release --project tests/EricksonLopez.Mediator.Benchmarks/EricksonLopez.Mediator.Benchmarks.csproj
```

### Pull Request Process
1. Ensure your code compiles cleanly (`TreatWarningsAsErrors=true`) and all tests and Native AOT smoke tests pass.
2. Update the `README.md` or the `docs/` folder with details of any changes to the public API.
3. Submit a Pull Request targeting the `main` or `develop` branch.
4. Complete the checklist provided in `.github/PULL_REQUEST_TEMPLATE.md`.

## Branch Naming Convention
We follow standard branch patterns:
- `feature/your-feature-name`
- `fix/your-fix-name`
- `docs/documentation-update`

## Commit Convention
We strictly follow [Conventional Commits](https://www.conventionalcommits.org/en/v1.0.0/) to automate SemVer releases and changelog generation via Release Please:

- `feat:` Adds a new feature or capability (triggers MINOR version bump)
- `fix:` Fixes a bug (triggers PATCH version bump)
- `feat!:` or `BREAKING CHANGE:` Breaking change in public API (triggers MAJOR version bump)
- `perf:` Performance improvements
- `docs:` Documentation updates only
- `test:` Adding or refactoring tests
- `refactor:` Code refactoring with no behavioral changes
- `chore:` Build, CI/CD, or maintenance updates

Example:
```
feat(generator): add support for multiple handlers
fix(core): resolve allocation issue in struct pipeline
docs(readme): update quick start guide
```
