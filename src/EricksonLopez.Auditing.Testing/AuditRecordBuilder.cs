// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;

namespace EricksonLopez.Auditing.Testing;

/// <summary>Provides a fluent builder for constructing <see cref="AuditRecord"/> test instances with customizable defaults.</summary>
public sealed class AuditRecordBuilder
{
    private Guid _id = Guid.NewGuid();
    private DateTimeOffset _occurredAt = TruncateToMilliseconds(DateTimeOffset.UtcNow);
    private AuditActor _actor = new(AuditActorType.User, "user-42", "Alice");
    private AuditAction _action = AuditAction.Create;
    private AuditResource _resource = new("Order", "order-1");
    private AuditOutcome _outcome = AuditOutcome.Success;
    private string _tenantId = "tenant-a";
    private string _source = "OrderService";
    private string? _correlationId;
    private string? _causationId;
    private string? _requestId;
    private string? _ipAddress;
    private string? _userAgent;
    private string? _errorCode;
    private string? _integrityHash;
    private string? _previousHash;
    private List<AuditChange>? _changes;

    /// <summary>Creates a new instance of the <see cref="AuditRecordBuilder"/> class with default values.</summary>
    /// <returns>A new <see cref="AuditRecordBuilder"/> instance.</returns>
    public static AuditRecordBuilder Create() => new();

    /// <summary>Creates a pre-configured <see cref="AuditRecord"/> instance with optional field overrides.</summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="actorId">The actor identifier.</param>
    /// <param name="resourceType">The resource type.</param>
    /// <param name="resourceId">The resource identifier.</param>
    /// <param name="outcome">The outcome status.</param>
    /// <param name="correlationId">The optional correlation identifier.</param>
    /// <returns>A new <see cref="AuditRecord"/> instance with the specified values.</returns>
    public static AuditRecord BuildDefault(
        string tenantId = "tenant-a",
        string actorId = "user-42",
        string resourceType = "Order",
        string resourceId = "order-1",
        AuditOutcome outcome = AuditOutcome.Success,
        string? correlationId = null)
    {
        return Create()
            .WithTenant(tenantId)
            .WithActor(AuditActorType.User, actorId, "Alice")
            .WithResource(resourceType, resourceId)
            .WithOutcome(outcome)
            .WithCorrelationId(correlationId)
            .Build();
    }

    /// <summary>Sets the unique record identifier.</summary>
    /// <param name="id">The unique identifier to set.</param>
    /// <returns>The current <see cref="AuditRecordBuilder"/> instance for method chaining.</returns>
    public AuditRecordBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }

    /// <summary>Sets the timestamp when the action occurred.</summary>
    /// <param name="occurredAt">The timestamp to set.</param>
    /// <returns>The current <see cref="AuditRecordBuilder"/> instance for method chaining.</returns>
    public AuditRecordBuilder WithOccurredAt(DateTimeOffset occurredAt)
    {
        _occurredAt = TruncateToMilliseconds(occurredAt);
        return this;
    }

    /// <summary>Sets the actor performing the action.</summary>
    /// <param name="actor">The actor to set.</param>
    /// <returns>The current <see cref="AuditRecordBuilder"/> instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="actor"/> is <see langword="null"/></exception>
    public AuditRecordBuilder WithActor(AuditActor actor)
    {
        _actor = actor ?? throw new ArgumentNullException(nameof(actor));
        return this;
    }

    /// <summary>Sets the actor performing the action using individual properties.</summary>
    /// <param name="type">The actor type classification.</param>
    /// <param name="id">The actor identifier.</param>
    /// <param name="displayName">The optional actor display name.</param>
    /// <returns>The current <see cref="AuditRecordBuilder"/> instance for method chaining.</returns>
    public AuditRecordBuilder WithActor(AuditActorType type, string id, string? displayName = null)
    {
        _actor = new AuditActor(type, id, displayName);
        return this;
    }

    /// <summary>Sets the operation executed on the resource.</summary>
    /// <param name="action">The action to set.</param>
    /// <returns>The current <see cref="AuditRecordBuilder"/> instance for method chaining.</returns>
    public AuditRecordBuilder WithAction(AuditAction action)
    {
        _action = action;
        return this;
    }

    /// <summary>Sets the operation executed on the resource by code.</summary>
    /// <param name="actionCode">The action code string to set.</param>
    /// <returns>The current <see cref="AuditRecordBuilder"/> instance for method chaining.</returns>
    public AuditRecordBuilder WithAction(string actionCode)
    {
        _action = new AuditAction(actionCode);
        return this;
    }

    /// <summary>Sets the target resource of the operation.</summary>
    /// <param name="resource">The target resource to set.</param>
    /// <returns>The current <see cref="AuditRecordBuilder"/> instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="resource"/> is <see langword="null"/></exception>
    public AuditRecordBuilder WithResource(AuditResource resource)
    {
        _resource = resource ?? throw new ArgumentNullException(nameof(resource));
        return this;
    }

    /// <summary>Sets the target resource of the operation using individual properties.</summary>
    /// <param name="type">The logical resource type.</param>
    /// <param name="id">The resource identifier.</param>
    /// <param name="aggregateType">The optional aggregate root type.</param>
    /// <param name="aggregateId">The optional aggregate root identifier.</param>
    /// <returns>The current <see cref="AuditRecordBuilder"/> instance for method chaining.</returns>
    public AuditRecordBuilder WithResource(
        string type,
        string id,
        string? aggregateType = null,
        string? aggregateId = null)
    {
        _resource = new AuditResource(type, id, aggregateType, aggregateId);
        return this;
    }

    /// <summary>Sets the outcome status of the audited action.</summary>
    /// <param name="outcome">The outcome status to set.</param>
    /// <returns>The current <see cref="AuditRecordBuilder"/> instance for method chaining.</returns>
    public AuditRecordBuilder WithOutcome(AuditOutcome outcome)
    {
        _outcome = outcome;
        return this;
    }

    /// <summary>Sets the tenant identifier scoping the record.</summary>
    /// <param name="tenantId">The tenant identifier to set.</param>
    /// <returns>The current <see cref="AuditRecordBuilder"/> instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="tenantId"/> is <see langword="null"/></exception>
    public AuditRecordBuilder WithTenant(string tenantId)
    {
        _tenantId = tenantId ?? throw new ArgumentNullException(nameof(tenantId));
        return this;
    }

    /// <summary>Sets the originating application or service component.</summary>
    /// <param name="source">The source component name to set.</param>
    /// <returns>The current <see cref="AuditRecordBuilder"/> instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/></exception>
    public AuditRecordBuilder WithSource(string source)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        return this;
    }

    /// <summary>Sets the optional correlation identifier.</summary>
    /// <param name="correlationId">The correlation identifier to set.</param>
    /// <returns>The current <see cref="AuditRecordBuilder"/> instance for method chaining.</returns>
    public AuditRecordBuilder WithCorrelationId(string? correlationId)
    {
        _correlationId = correlationId;
        return this;
    }

    /// <summary>Sets the optional causation identifier.</summary>
    /// <param name="causationId">The causation identifier to set.</param>
    /// <returns>The current <see cref="AuditRecordBuilder"/> instance for method chaining.</returns>
    public AuditRecordBuilder WithCausationId(string? causationId)
    {
        _causationId = causationId;
        return this;
    }

    /// <summary>Sets the optional request identifier.</summary>
    /// <param name="requestId">The request identifier to set.</param>
    /// <returns>The current <see cref="AuditRecordBuilder"/> instance for method chaining.</returns>
    public AuditRecordBuilder WithRequestId(string? requestId)
    {
        _requestId = requestId;
        return this;
    }

    /// <summary>Sets the optional client network IP address.</summary>
    /// <param name="ipAddress">The IP address to set.</param>
    /// <returns>The current <see cref="AuditRecordBuilder"/> instance for method chaining.</returns>
    public AuditRecordBuilder WithIpAddress(string? ipAddress)
    {
        _ipAddress = ipAddress;
        return this;
    }

    /// <summary>Sets the optional client user agent string.</summary>
    /// <param name="userAgent">The user agent string to set.</param>
    /// <returns>The current <see cref="AuditRecordBuilder"/> instance for method chaining.</returns>
    public AuditRecordBuilder WithUserAgent(string? userAgent)
    {
        _userAgent = userAgent;
        return this;
    }

    /// <summary>Sets the optional error code for unsuccessful outcomes.</summary>
    /// <param name="errorCode">The error code to set.</param>
    /// <returns>The current <see cref="AuditRecordBuilder"/> instance for method chaining.</returns>
    public AuditRecordBuilder WithErrorCode(string? errorCode)
    {
        _errorCode = errorCode;
        return this;
    }

    /// <summary>Sets the HMAC cryptographic integrity hash.</summary>
    /// <param name="hash">The integrity hash to set.</param>
    /// <returns>The current <see cref="AuditRecordBuilder"/> instance for method chaining.</returns>
    public AuditRecordBuilder WithIntegrityHash(string? hash)
    {
        _integrityHash = hash;
        return this;
    }

    /// <summary>Sets the cryptographic hash of the preceding record in the chain.</summary>
    /// <param name="previousHash">The previous record hash to set.</param>
    /// <returns>The current <see cref="AuditRecordBuilder"/> instance for method chaining.</returns>
    public AuditRecordBuilder WithPreviousHash(string? previousHash)
    {
        _previousHash = previousHash;
        return this;
    }

    /// <summary>Adds a single field-level change to the record.</summary>
    /// <param name="field">The modified field name.</param>
    /// <param name="oldValue">The value before the change.</param>
    /// <param name="newValue">The value after the change.</param>
    /// <param name="isRedacted">A value indicating whether the field value was redacted.</param>
    /// <returns>The current <see cref="AuditRecordBuilder"/> instance for method chaining.</returns>
    public AuditRecordBuilder AddChange(string field, string? oldValue, string? newValue, bool isRedacted = false)
    {
        _changes ??= new List<AuditChange>();
        _changes.Add(new AuditChange(field, oldValue, newValue, isRedacted));
        return this;
    }

    /// <summary>Adds a redacted field change to the record.</summary>
    /// <param name="field">The redacted field name.</param>
    /// <returns>The current <see cref="AuditRecordBuilder"/> instance for method chaining.</returns>
    public AuditRecordBuilder AddRedactedChange(string field)
    {
        _changes ??= new List<AuditChange>();
        _changes.Add(AuditChange.Redacted(field));
        return this;
    }

    /// <summary>Sets the complete collection of field-level changes.</summary>
    /// <param name="changes">The collection of changes to set.</param>
    /// <returns>The current <see cref="AuditRecordBuilder"/> instance for method chaining.</returns>
    public AuditRecordBuilder WithChanges(IReadOnlyList<AuditChange>? changes)
    {
        _changes = changes != null ? new List<AuditChange>(changes) : null;
        return this;
    }

    /// <summary>Creates the immutable <see cref="AuditRecord"/> instance configured by this builder.</summary>
    /// <returns>A new immutable <see cref="AuditRecord"/> instance.</returns>
    public AuditRecord Build()
    {
        return new AuditRecord
        {
            Id = _id,
            OccurredAt = _occurredAt,
            Actor = _actor,
            Action = _action,
            Resource = _resource,
            Outcome = _outcome,
            ErrorCode = _errorCode,
            Context = new AuditContext(
                TenantId: _tenantId,
                Source: _source,
                CorrelationId: _correlationId,
                CausationId: _causationId,
                RequestId: _requestId,
                IpAddress: _ipAddress,
                UserAgent: _userAgent),
            Changes = _changes,
            IntegrityHash = _integrityHash,
            PreviousHash = _previousHash
        };
    }

    private static DateTimeOffset TruncateToMilliseconds(DateTimeOffset dt)
    {
        return new DateTimeOffset(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, dt.Second, dt.Millisecond, dt.Offset);
    }
}
