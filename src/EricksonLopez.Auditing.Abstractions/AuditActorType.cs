// Copyright © Erickson Lopez. MIT License.

namespace EricksonLopez.Auditing;

/// <summary>Specifies the classification of the entity that performed an audited action.</summary>
public enum AuditActorType : byte
{
    /// <summary>Specifies an authenticated human user.</summary>
    User = 1,

    /// <summary>Specifies an internal system process with no human identity.</summary>
    SystemProcess = 2,

    /// <summary>Specifies an external service acting via service-to-service authentication.</summary>
    Service = 3,

    /// <summary>Specifies an automated scheduled job or background worker.</summary>
    ScheduledJob = 4,

    /// <summary>Specifies an external integration or third-party system.</summary>
    Integration = 5,

    /// <summary>Specifies an unauthenticated or unidentifiable principal.</summary>
    Anonymous = 6
}
