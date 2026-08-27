# Security Policy

## Supported Versions

The following table lists the versions of `EricksonLopez.Mediator` and their support status based on the current Git tags and `.csproj` versions.

| Version | Supported          | Notes                                              |
| ------- | ------------------ | -------------------------------------------------- |
| 1.0.x   | :white_check_mark: | Currently supported (`1.0.0-rc1`), targets .NET 8/9/10 |
| < 1.0.x | :x:                | Unsupported |

## Reporting a Vulnerability

If you discover a potential security vulnerability in this project, please report it immediately. We will investigate all legitimate reports and do our best to quickly resolve the problem.

1. **Do not open a public issue.** This could expose the vulnerability to malicious actors before we have a chance to patch it.
2. Email your findings to [ericksonlopezf@gmail.com](mailto:ericksonlopezf@gmail.com).
3. Please provide as much information as possible, including:
   - A detailed description of the vulnerability.
   - Steps to reproduce the issue.
   - Potential impact of the vulnerability.

We aim to respond to all reports within 48 hours.

## Supply Chain Security

The `EricksonLopez.Mediator` publishing pipeline enforces state-of-the-art supply chain security practices:

- **Sigstore Provenance Attestation**: All published `.nupkg` artifacts generate cryptographic build provenance attestations using GitHub Actions (`actions/attest-build-provenance@v2`), allowing downstream consumers to verify that packages originate from this exact repository and workflow.
- **NuGet Trusted Publishing (OIDC)**: Package publication to NuGet.org uses short-lived OpenID Connect (OIDC) tokens via `NuGet/login@v1`. No static NuGet API keys are stored in repository secrets.
- **Strong Name Signing**: All production assemblies are strongly named with an RSA key (`.snk`) decoded from the `SNK_KEY` secret during CI/CD execution.
- **Automated Dependency Scanning**: Dependabot actively audits NuGet packages and GitHub Actions for vulnerabilities with weekly and monthly cadences.
- **Quality Gates & Mutation Testing**: Pre-release verification includes strict quality gates requiring test suite execution, code coverage analysis via Coverlet/Codecov, and Stryker.NET mutation testing.

## Known Security Boundaries

- **Source Generator Security**: The `EricksonLopez.Mediator.Generator` executes exclusively within the Roslyn compiler workspace. It analyzes syntax trees and emits C# source files without making network requests, accessing external processes, or reading arbitrary files from disk.
- **Dependency Injection & Runtime Isolation**: The generated dispatch and DI wiring mechanisms rely on the `IServiceProvider` configured by the hosting application. It is the application's responsibility to configure proper service lifetimes and authenticate/authorize requests before delegating to `IMediator.Send` or `IPublisher.Publish`.
