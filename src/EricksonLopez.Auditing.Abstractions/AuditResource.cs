// Copyright © Erickson Lopez. MIT License.
using System;

namespace EricksonLopez.Auditing;

/// <summary>Represents the target resource on which an audited action was performed.</summary>
/// <param name="Type">The logical type or entity class of the resource.</param>
/// <param name="Id">The stable identifier of the specific resource instance.</param>
/// <param name="AggregateType">The optional aggregate root entity type when the resource is a child entity.</param>
/// <param name="AggregateId">The optional identifier of the aggregate root entity.</param>
/// <exception cref="ArgumentException"><paramref name="Type"/> or <paramref name="Id"/> is <see langword="null"/> or white-space</exception>
public sealed record AuditResource(
    string Type,
    string Id,
    string? AggregateType = null,
    string? AggregateId = null)
{
    private readonly string _type = !string.IsNullOrWhiteSpace(Type) ? Type : throw new ArgumentException("Resource Type cannot be null or whitespace.", nameof(Type));
    private readonly string _id = !string.IsNullOrWhiteSpace(Id) ? Id : throw new ArgumentException("Resource Id cannot be null or whitespace.", nameof(Id));

    /// <summary>Gets the logical type or entity class of the resource.</summary>
    /// <exception cref="ArgumentException"><paramref name="value"/> is <see langword="null"/> or white-space</exception>
    public string Type
    {
        get => _type;
        init
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            _type = value;
        }
    }

    /// <summary>Gets the stable identifier of the specific resource instance.</summary>
    /// <exception cref="ArgumentException"><paramref name="value"/> is <see langword="null"/> or white-space</exception>
    public string Id
    {
        get => _id;
        init
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            _id = value;
        }
    }
}
