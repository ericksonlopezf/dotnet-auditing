// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace EricksonLopez.Auditing;

/// <summary>Defines the persistence store contract for appending and querying immutable audit records.</summary>
public interface IAuditStore
{
    /// <summary>Appends a single audit record to the persistent store.</summary>
    /// <param name="record">The audit record to persist.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A value task representing the asynchronous operation.</returns>
    ValueTask AppendAsync(AuditRecord record, CancellationToken cancellationToken = default);

    /// <summary>Appends a collection of audit records to the persistent store in a single batch operation.</summary>
    /// <param name="records">The collection of audit records to persist.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A value task representing the asynchronous operation.</returns>
    ValueTask AppendBatchAsync(
        IReadOnlyList<AuditRecord> records,
        CancellationToken cancellationToken = default);

    /// <summary>Queries persisted audit records using the specified filter and pagination parameters.</summary>
    /// <param name="query">The query parameters and pagination options.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A value task representing the asynchronous operation. The task result contains the paginated query results.</returns>
    ValueTask<AuditQueryResult> QueryAsync(
        AuditQuery query,
        CancellationToken cancellationToken = default);
}
