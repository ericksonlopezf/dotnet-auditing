// Copyright © Erickson Lopez. MIT License.

namespace EricksonLopez.Auditing;

/// <summary>Specifies the failure handling policy when persisting an audit record fails.</summary>
public enum AuditFailureBehavior
{
    /// <summary>Propagates the store exception to the caller, blocking the business operation.</summary>
    FailClosed = 1,

    /// <summary>Suppresses the store exception and allows the business operation to proceed.</summary>
    FailOpen = 2,

    /// <summary>Enqueues the record for deferred processing via a resilient background mechanism.</summary>
    Deferred = 3
}
