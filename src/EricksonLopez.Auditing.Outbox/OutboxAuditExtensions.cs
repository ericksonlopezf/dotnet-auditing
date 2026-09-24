// Copyright © Erickson Lopez. MIT License.
using System;
using Microsoft.Extensions.DependencyInjection;

namespace EricksonLopez.Auditing.Outbox;

/// <summary>Provides extension methods for registering the Outbox audit store with <see cref="IAuditBuilder"/>.</summary>
public static class OutboxAuditExtensions
{
    /// <summary>Configures the audit pipeline to use the transactional outbox pattern.</summary>
    /// <param name="builder">The audit builder being configured.</param>
    /// <returns>The <see cref="IAuditBuilder"/> for method chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/></exception>
    public static IAuditBuilder UseOutbox(this IAuditBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.AddSingleton<IAuditStore, OutboxAuditStore>();
        return builder;
    }
}
