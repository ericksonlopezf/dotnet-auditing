// Copyright © Erickson Lopez. MIT License.
using System;
using System.Threading;

namespace EricksonLopez.Auditing;

/// <summary>
/// Provides ambient or UTC timestamps with support for deterministic time-freezing scopes in testing and transactions.
/// </summary>
public sealed class AmbientAuditTimeProvider : IAuditTimeProvider
{
    private static readonly AsyncLocal<DateTimeOffset?> _ambientTime = new();

    /// <summary>
    /// Gets the singleton instance of <see cref="AmbientAuditTimeProvider"/>.
    /// </summary>
    public static AmbientAuditTimeProvider Instance { get; } = new();

    /// <inheritdoc/>
    public DateTimeOffset GetUtcNow() => _ambientTime.Value ?? DateTimeOffset.UtcNow;

    /// <summary>
    /// Begins a time scope where <see cref="GetUtcNow"/> returns the specified frozen timestamp until disposed.
    /// </summary>
    /// <param name="frozenUtc">The deterministic UTC timestamp to freeze.</param>
    /// <returns>A disposable scope that restores the previous ambient time upon disposal.</returns>
    public static IDisposable BeginScope(DateTimeOffset frozenUtc)
    {
        var previous = _ambientTime.Value;
        _ambientTime.Value = frozenUtc;
        return new TimeScope(previous);
    }

    private sealed class TimeScope : IDisposable
    {
        private readonly DateTimeOffset? _previous;
        private bool _disposed;

        public TimeScope(DateTimeOffset? previous) => _previous = previous;

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _ambientTime.Value = _previous;
            }
        }
    }
}
