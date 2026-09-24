// Copyright © Erickson Lopez. MIT License.
using System;
using System.Threading;
using System.Threading.Tasks;

namespace EricksonLopez.Auditing;

/// <summary>
/// Provides an implementation of <see cref="IAuditLogger{TCategoryName}"/> that constructs and appends <see cref="AuditRecord"/> instances.
/// </summary>
/// <typeparam name="TCategoryName">The category type associated with logged audit records.</typeparam>
public sealed class AuditLogger<TCategoryName> : IAuditLogger<TCategoryName>
{
    private readonly IAuditStore _store;
    private readonly IAuditActorProvider _actorProvider;
    private readonly IAuditContextProvider? _contextProvider;

    /// <summary>Initializes a new instance of the <see cref="AuditLogger{TCategoryName}"/> class.</summary>
    /// <param name="store">The store used to persist audit records.</param>
    /// <param name="actorProvider">The provider to resolve the current actor.</param>
    /// <param name="contextProvider">The optional provider to resolve ambient context.</param>
    /// <exception cref="ArgumentNullException"><paramref name="store"/> or <paramref name="actorProvider"/> is <see langword="null"/></exception>
    public AuditLogger(IAuditStore store, IAuditActorProvider actorProvider, IAuditContextProvider? contextProvider = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _actorProvider = actorProvider ?? throw new ArgumentNullException(nameof(actorProvider));
        _contextProvider = contextProvider;
    }

    /// <inheritdoc/>
    public async ValueTask LogAsync(AuditAction action, AuditResource resource, AuditOutcome outcome, CancellationToken cancellationToken = default)
    {
        var context = _contextProvider?.GetCurrentContext() ?? new AuditContext(AuditContext.SystemTenantId, "Unknown");
        var actor = _actorProvider.GetCurrentActor();

        var record = new AuditRecord
        {
            Id = AuditId.NewId(),
            OccurredAt = DateTimeOffset.UtcNow,
            Actor = actor,
            Action = action,
            Resource = resource,
            Outcome = outcome,
            Context = context
        };

        await _store.AppendAsync(record, cancellationToken).ConfigureAwait(false);
    }
}
