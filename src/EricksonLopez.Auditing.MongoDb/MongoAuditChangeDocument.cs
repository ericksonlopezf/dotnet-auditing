// Copyright © Erickson Lopez. MIT License.
using MongoDB.Bson.Serialization.Attributes;

namespace EricksonLopez.Auditing.MongoDb;

/// <summary>Represents a single field-level change sub-document in MongoDB.</summary>
public sealed class MongoAuditChangeDocument
{
    /// <summary>Gets or sets the name of the modified field or property.</summary>
    [BsonElement("field")]
    public string Field { get; set; } = null!;

    /// <summary>Gets or sets the value before the change.</summary>
    [BsonElement("oldValue")]
    [BsonIgnoreIfNull]
    public string? OldValue { get; set; }

    /// <summary>Gets or sets the value after the change.</summary>
    [BsonElement("newValue")]
    [BsonIgnoreIfNull]
    public string? NewValue { get; set; }

    /// <summary>Gets or sets a value indicating whether the actual values were withheld by sensitivity policies.</summary>
    [BsonElement("isRedacted")]
    public bool IsRedacted { get; set; }
}
