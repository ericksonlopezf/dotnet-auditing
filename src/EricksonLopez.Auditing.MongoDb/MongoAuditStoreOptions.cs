// Copyright © Erickson Lopez. MIT License.

namespace EricksonLopez.Auditing.MongoDb;

/// <summary>Represents configuration options for <see cref="MongoAuditStore"/>.</summary>
public sealed class MongoAuditStoreOptions
{
    /// <summary>Gets or sets the MongoDB collection name for storing audit records.</summary>
    public string CollectionName { get; set; } = "audit_records";

    /// <summary>Gets or sets the MongoDB database name hosting the audit records collection.</summary>
    public string DatabaseName { get; set; } = "AuditingDb";
}
