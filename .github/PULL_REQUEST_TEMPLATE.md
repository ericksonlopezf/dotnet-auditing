<!-- Copyright © Erickson Lopez. MIT License. -->
## Description

Please provide a clear and concise summary of the changes introduced in this pull request and the motivation behind them.

Fixes #(issue)

---

## Type of Change

- [ ] `feat`: New feature or public API addition
- [ ] `fix`: Bug fix
- [ ] `docs`: Documentation updates or additions
- [ ] `perf`: Performance optimization
- [ ] `refactor`: Code change that neither fixes a bug nor adds a feature
- [ ] `test`: Adding or updating test suites
- [ ] `chore`: Repository maintenance, CI, or dependency updates

---

## Affected Packages

- [ ] `EricksonLopez.Auditing.Abstractions`
- [ ] `EricksonLopez.Auditing` (Core)
- [ ] `EricksonLopez.Auditing.Analyzers`
- [ ] `EricksonLopez.Auditing.AzureKeyVault`
- [ ] `EricksonLopez.Auditing.Outbox`
- [ ] `EricksonLopez.Auditing.Testing`
- [ ] `EricksonLopez.Auditing.PostgreSql`
- [ ] `EricksonLopez.Auditing.SqlServer`
- [ ] `EricksonLopez.Auditing.MySql`
- [ ] `EricksonLopez.Auditing.Oracle`
- [ ] `EricksonLopez.Auditing.Sqlite`
- [ ] `EricksonLopez.Auditing.Dapper`
- [ ] `EricksonLopez.Auditing.EntityFrameworkCore`
- [ ] `EricksonLopez.Auditing.MongoDb`
- [ ] `EricksonLopez.Auditing.OpenTelemetry`
- [ ] `EricksonLopez.Auditing.Benchmarks`
- [ ] `EricksonLopez.Auditing.Showcase`

---

## Quality & Compliance Checklist

- [ ] My code adheres to the coding guidelines and architecture boundaries of this repository.
- [ ] The solution builds cleanly in Release configuration with zero warnings (`dotnet build -c Release`).
- [ ] All unit tests pass across target frameworks (`dotnet test EricksonLopez.Auditing.slnx --filter "Category!=Integration"`).
- [ ] New/modified logic includes unit test coverage.
- [ ] Mutation testing quality gate verified (`≥95%` break threshold across packages, `100%` core).
- [ ] Benchmark regression gate verified (0 B allocation on hot path, `≤5%` latency regression vs baseline).
- [ ] Native AOT compatibility has been preserved (zero reflection / trimmable).
- [ ] Multi-tenant isolation and append-only invariants have been preserved.
- [ ] Documentation has been updated accordingly under `/docs/` and `README.md`.
- [ ] Conventional commit guidelines have been followed.
