// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Threading;

namespace EricksonLopez.Auditing;

/// <summary>Provides an ambient scope for enriching audit records with additional context.</summary>
/// <remarks>
/// Disposing this scope restores the previous ambient scope, supporting nested execution contexts.
/// </remarks>
public sealed class AuditScope : IDisposable
{
    private static readonly AsyncLocal<AuditScope?> _current = new();

    private readonly AuditScope? _parent;
    private readonly Dictionary<string, string> _metadata;
    private bool _disposed;

    private AuditScope(AuditScope? parent, Dictionary<string, string> metadata)
    {
        _parent = parent;
        _metadata = metadata;
    }

    /// <summary>Gets the current ambient <see cref="AuditScope"/>, or <see langword="null"/> if none is active.</summary>
    public static AuditScope? Current => _current.Value;

    /// <summary>Gets the metadata key-value pairs registered within this scope.</summary>
    public IReadOnlyDictionary<string, string> Metadata => _metadata;

    /// <summary>Begins a new ambient audit scope with optional initial metadata.</summary>
    /// <param name="initialMetadata">The optional initial key-value metadata to seed into the new scope.</param>
    /// <returns>A new <see cref="AuditScope"/> instance. Disposing it restores the enclosing scope.</returns>
    /// <remarks>
    /// Child scopes start with an empty metadata dictionary and do NOT inherit entries from the parent scope.
    /// Each scope independently manages its own metadata via <see cref="WithMetadata"/>.
    /// Parent scope metadata is fully preserved and restored upon child scope disposal.
    /// </remarks>
    public static AuditScope Begin(IReadOnlyDictionary<string, string>? initialMetadata = null)
    {
        var parent = _current.Value;
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal);

        if (initialMetadata is not null)
        {
            foreach (var kvp in initialMetadata)
            {
                metadata[kvp.Key] = kvp.Value;
            }
        }

        var scope = new AuditScope(parent, metadata);
        _current.Value = scope;
        return scope;
    }

    /// <summary>Adds or updates a metadata entry within this scope.</summary>
    /// <param name="key">The metadata key to set.</param>
    /// <param name="value">The metadata value to associate with the specified key.</param>
    /// <returns>The current <see cref="AuditScope"/> instance for method chaining.</returns>
    /// <exception cref="ArgumentException"><paramref name="key"/> is <see langword="null"/> or empty</exception>
    public AuditScope WithMetadata(string key, string value)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        _metadata[key] = value;
        return this;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _current.Value = _parent;
    }
}
