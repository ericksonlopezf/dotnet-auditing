// Copyright © Erickson Lopez. MIT License.
using System;

namespace EricksonLopez.Auditing;

/// <summary>
/// Defines an abstraction for obtaining the current deterministic audit timestamp.
/// </summary>
public interface IAuditTimeProvider
{
    /// <summary>
    /// Gets the current deterministic audit timestamp in UTC.
    /// </summary>
    /// <returns>The current UTC timestamp.</returns>
    DateTimeOffset GetUtcNow();
}
