// Copyright © Erickson Lopez. MIT License.
using System.Threading;
using System.Threading.Tasks;

namespace EricksonLopez.Auditing;

/// <summary>
/// Provides a simplified facade for creating and persisting audit records without manually resolving actors and scopes.
/// </summary>
public interface IAuditLogger
{
    /// <summary>Logs an audit event asynchronously.</summary>
    /// <param name="action">The operation that was executed.</param>
    /// <param name="resource">The target resource on which the operation was performed.</param>
    /// <param name="outcome">The observable result of the operation.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    ValueTask LogAsync(AuditAction action, AuditResource resource, AuditOutcome outcome, CancellationToken cancellationToken = default);
}
