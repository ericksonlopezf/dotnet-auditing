# Project Governance

This document outlines the governance model for the `EricksonLopez.Auditing` project ecosystem.

## 🎯 Overview

`EricksonLopez.Auditing` is an open-source project maintained by **Erickson Lopez** and community contributors. The project provides enterprise-grade, immutable, tamper-evident audit logging, cryptographic hash chaining (HMAC-SHA256), PII sensitivity redaction, and decoupled persistence providers for .NET applications.

## 👥 Roles & Responsibilities

### Maintainers
Maintainers are individuals responsible for the overall direction, quality, security, and governance of the repository.

**Current Maintainers:**
- **Erickson Lopez** ([@ericksonlopezf](https://github.com/ericksonlopezf)) — Lead Architect & Project Maintainer

**Maintainer Responsibilities:**
- Reviewing and merging Pull Requests.
- Maintaining API stability and semantic versioning.
- Managing releases and publishing NuGet packages.
- Monitoring security reports and triaging issues.
- Setting project roadmap priorities.

### Contributors
Contributors are community members who participate by opening issues, improving documentation, submitting pull requests, or participating in discussions.

## ⚖️ Decision-Making Process

1. **Minor Changes & Bug Fixes**: Decisions are made through standard code review on Pull Requests. Approval by a Maintainer is required to merge.
2. **Major Features & Architectural Changes**: Proposals must be initiated via GitHub Discussions or an issue. Once consensus is reached, an Architectural Decision Record (ADR) will be created in `docs/decisions/`.
3. **Breaking Changes**: Breaking changes are strictly limited to major version updates (e.g., `3.0.0`) and require formal review by Maintainers.

## 📢 Licensing & Intellectual Property

All contributions to `EricksonLopez.Auditing` are submitted under the terms of the [MIT License](LICENSE).
