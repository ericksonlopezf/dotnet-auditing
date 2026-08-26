// Copyright © Erickson Lopez. MIT License.
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;

namespace EricksonLopez.Auditing;

/// <summary>Defines a fluent builder for configuring auditing services and registering storage providers.</summary>
public interface IAuditBuilder
{
    /// <summary>Gets the underlying service collection being configured.</summary>
    IServiceCollection Services { get; }

    /// <summary>Registers a custom actor provider type to resolve current actor identities.</summary>
    /// <typeparam name="TProvider">The type of the actor provider implementation.</typeparam>
    /// <returns>The current <see cref="IAuditBuilder"/> instance for fluent chaining.</returns>
    IAuditBuilder UseActorProvider<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TProvider>()
        where TProvider : class, IAuditActorProvider;

    /// <summary>Enables HMAC-SHA256 cryptographic chain integrity verification for audit records.</summary>
    /// <returns>The current <see cref="IAuditBuilder"/> instance for fluent chaining.</returns>
    IAuditBuilder EnableIntegrityChain();

    /// <summary>Registers an audit persistence store implementation.</summary>
    /// <typeparam name="TStore">The type of the audit store implementation.</typeparam>
    /// <returns>The current <see cref="IAuditBuilder"/> instance for fluent chaining.</returns>
    IAuditBuilder UseStore<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TStore>()
        where TStore : class, IAuditStore;
}
