// Copyright © Erickson Lopez. MIT License.
using System.Diagnostics;

namespace EricksonLopez.Auditing.OpenTelemetry;

/// <summary>Provides the OpenTelemetry activity source and semantic tag conventions for auditing operations.</summary>
public static class AuditActivitySource
{
    /// <summary>Gets the canonical activity source name for auditing telemetry.</summary>
    public const string ActivitySourceName = "EricksonLopez.Auditing";

    /// <summary>Gets the <see cref="ActivitySource"/> instance used for creating audit telemetry spans.</summary>
    public static readonly ActivitySource Source = new(ActivitySourceName, "1.0.0");

    /// <summary>Defines semantic attribute names following OpenTelemetry conventions for audit spans.</summary>
    public static class Tags
    {
        /// <summary>Specifies the semantic attribute name for tenant identifiers.</summary>
        public const string TenantId = "audit.tenant_id";

        /// <summary>Specifies the semantic attribute name for action codes.</summary>
        public const string ActionCode = "audit.action_code";

        /// <summary>Specifies the semantic attribute name for resource types.</summary>
        public const string ResourceType = "audit.resource_type";

        /// <summary>Specifies the semantic attribute name for resource identifiers.</summary>
        public const string ResourceId = "audit.resource_id";

        /// <summary>Specifies the semantic attribute name for actor identifiers.</summary>
        public const string ActorId = "audit.actor_id";

        /// <summary>Specifies the semantic attribute name for actor type classifications.</summary>
        public const string ActorType = "audit.actor_type";

        /// <summary>Specifies the semantic attribute name for audit outcome statuses.</summary>
        public const string Outcome = "audit.outcome";

        /// <summary>Specifies the semantic attribute name for unique audit record identifiers.</summary>
        public const string RecordId = "audit.record_id";
    }
}
