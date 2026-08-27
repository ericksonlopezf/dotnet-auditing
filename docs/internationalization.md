# Internationalization & Exception Culture Safety

## 1. Diagnostic Culture Invariance
All Roslyn compiler diagnostics (`ELM001` - `ELM011`), log formatters, and telemetry metric labels emitted by `EricksonLopez.Mediator` use strict invariant culture formatting (`CultureInfo.InvariantCulture`).

This ensures deterministic compilation logs, consistent OpenTelemetry attribute tagging, and uniform exception messages across heterogeneous multi-cloud deployment topologies.
