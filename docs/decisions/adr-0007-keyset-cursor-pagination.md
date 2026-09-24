<!-- Copyright © Erickson Lopez. MIT License. -->
# ADR-0007: Keyset Pagination for O(1) Large Scale Queries

## Status
Accepted

## Date
2026-09-04

## Context

Audit tables routinely accumulate hundreds of millions of records. Traditional offset-based pagination (`OFFSET N LIMIT M`) requires the database engine to scan and discard the first $N$ rows, resulting in $O(N)$ query degradation and high CPU/disk I/O when reading older records.

## Decision

Deprecate and remove `Skip` and `Take` from `AuditQuery`. Replace them with cursor-based Keyset Pagination using `AuditQuery.ContinuationToken` and `AuditQuery.PageSize`. Storage queries seek directly on the composite index `(tenant_id, occurred_at DESC, id DESC)`. The next page cursor is returned as `AuditQueryResult.NextPageToken` (an opaque base64-encoded token produced by `AuditCursorToken.Create`). To advance to the next page, set `ContinuationToken = result.NextPageToken` in the subsequent query.

## Consequences

### Positive
* Constant $O(1)$ seek performance regardless of table depth or historical range.
* Immune to "missing row" or "duplicate row" anomalies when new records are appended concurrently during pagination.

### Negative / Trade-offs
* Random-access page jumping (e.g. jumping directly to page 50) is not supported; queries must navigate sequentially using cursors.
