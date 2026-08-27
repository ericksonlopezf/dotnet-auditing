// Copyright © Erickson Lopez. MIT License.

namespace EricksonLopez.Auditing;

/// <summary>Defines a provider for resolving the current ambient execution context.</summary>
public interface IAuditContextProvider
{
    /// <summary>Resolves the current ambient execution context.</summary>
    /// <returns>The current <see cref="AuditContext"/> instance.</returns>
    AuditContext GetCurrentContext();
}
