// Copyright © Erickson Lopez. MIT License.
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace EricksonLopez.Auditing;

/// <summary>
/// Defines a contract for sanitizing audit records and field changes before persistence.
/// </summary>
public interface IAuditSensitivityPipeline
{
    /// <summary>
    /// Sanitizes the specified audit record by applying redaction or exclusions to sensitive fields.
    /// </summary>
    /// <param name="record">The audit record to sanitize.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation. The task result contains the sanitized audit record.</returns>
    ValueTask<AuditRecord> SanitizeAsync(AuditRecord record, CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies sensitivity policies to a collection of field changes, returning a sanitized collection.
    /// </summary>
    /// <param name="changes">The raw collection of field changes to process.</param>
    /// <param name="tenantId">The tenant ID to use for fetching encryption keys.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation. The task result contains the sanitized collection of changes, or <see langword="null"/> if all changes were excluded or the input was <see langword="null"/>.</returns>
    ValueTask<IReadOnlyList<AuditChange>?> ApplyAsync(IReadOnlyList<AuditChange>? changes, string tenantId, CancellationToken cancellationToken = default);
}
