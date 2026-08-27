// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace EricksonLopez.Auditing.MongoDb;

/// <summary>Represents a persisted audit record document in MongoDB.</summary>
public sealed class MongoAuditRecordDocument
{
    /// <summary>Gets or sets the unique, time-ordered identifier of the audit record.</summary>
    [BsonId]
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid Id { get; set; }

    /// <summary>Gets or sets the timestamp when the action occurred in UTC.</summary>
    [BsonElement("occurredAt")]
    public DateTime OccurredAt { get; set; }

    /// <summary>Gets or sets the tenant identifier scoping the audit record.</summary>
    [BsonElement("tenantId")]
    public string TenantId { get; set; } = null!;

    /// <summary>Gets or sets the originating system source component.</summary>
    [BsonElement("source")]
    public string Source { get; set; } = null!;

    /// <summary>Gets or sets the classification code of the actor.</summary>
    [BsonElement("actorType")]
    public byte ActorType { get; set; }

    /// <summary>Gets or sets the stable identifier of the actor.</summary>
    [BsonElement("actorId")]
    public string ActorId { get; set; } = null!;

    /// <summary>Gets or sets the optional display name of the actor.</summary>
    [BsonElement("actorName")]
    [BsonIgnoreIfNull]
    public string? ActorName { get; set; }

    /// <summary>Gets or sets the action code identifying the executed operation.</summary>
    [BsonElement("actionCode")]
    public string ActionCode { get; set; } = null!;

    /// <summary>Gets or sets the logical type of the target resource.</summary>
    [BsonElement("resourceType")]
    public string ResourceType { get; set; } = null!;

    /// <summary>Gets or sets the stable identifier of the target resource.</summary>
    [BsonElement("resourceId")]
    public string ResourceId { get; set; } = null!;

    /// <summary>Gets or sets the optional aggregate root entity type.</summary>
    [BsonElement("aggregateType")]
    [BsonIgnoreIfNull]
    public string? AggregateType { get; set; }

    /// <summary>Gets or sets the optional aggregate root identifier.</summary>
    [BsonElement("aggregateId")]
    [BsonIgnoreIfNull]
    public string? AggregateId { get; set; }

    /// <summary>Gets or sets the outcome classification code of the operation.</summary>
    [BsonElement("outcome")]
    public byte Outcome { get; set; }

    /// <summary>Gets or sets the optional error code for unsuccessful outcomes.</summary>
    [BsonElement("errorCode")]
    [BsonIgnoreIfNull]
    public string? ErrorCode { get; set; }

    /// <summary>Gets or sets the optional correlation identifier linking related operations.</summary>
    [BsonElement("correlationId")]
    [BsonIgnoreIfNull]
    public string? CorrelationId { get; set; }

    /// <summary>Gets or sets the optional identifier of the direct causal event.</summary>
    [BsonElement("causationId")]
    [BsonIgnoreIfNull]
    public string? CausationId { get; set; }

    /// <summary>Gets or sets the optional transport-level request identifier.</summary>
    [BsonElement("requestId")]
    [BsonIgnoreIfNull]
    public string? RequestId { get; set; }

    /// <summary>Gets or sets the optional client network IP address.</summary>
    [BsonElement("ipAddress")]
    [BsonIgnoreIfNull]
    public string? IpAddress { get; set; }

    /// <summary>Gets or sets the optional client user agent string.</summary>
    [BsonElement("userAgent")]
    [BsonIgnoreIfNull]
    public string? UserAgent { get; set; }

    /// <summary>Gets or sets the collection of field-level changes captured in the record.</summary>
    [BsonElement("changes")]
    [BsonIgnoreIfNull]
    public List<MongoAuditChangeDocument>? Changes { get; set; }

    /// <summary>Gets or sets the cryptographic HMAC hash for chain integrity verification.</summary>
    [BsonElement("integrityHash")]
    [BsonIgnoreIfNull]
    public string? IntegrityHash { get; set; }

    /// <summary>Gets or sets the cryptographic HMAC hash of the preceding record in the chain.</summary>
    [BsonElement("previousHash")]
    [BsonIgnoreIfNull]
    public string? PreviousHash { get; set; }
}
