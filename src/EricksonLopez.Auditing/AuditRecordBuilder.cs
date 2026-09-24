// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;

namespace EricksonLopez.Auditing;

/// <summary>
/// Provides a fluent builder for constructing immutable <see cref="AuditRecord"/> instances.
/// </summary>
public sealed class AuditRecordBuilder
{
    private Guid _id = Guid.NewGuid();
    private DateTimeOffset _occurredAt = TruncateToMilliseconds(DateTimeOffset.UtcNow);
    private AuditActor? _actor;
    private AuditAction? _action;
    private AuditResource? _resource;
    private AuditOutcome _outcome = AuditOutcome.Success;
    private string _tenantId = "default";
    private string _source = "Application";
    private string? _correlationId;
    private string? _causationId;
    private string? _requestId;
    private string? _ipAddress;
    private string? _userAgent;
    private string? _errorCode;
    private string? _integrityHash;
    private string? _previousHash;
    private List<AuditChange>? _changes;

    /// <summary>Creates a new instance of the <see cref="AuditRecordBuilder"/> class.</summary>
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

    /// <summary>Sets the unique identifier for the audit record.</summary>
    /// <param name="id">The unique identifier.</param>
    /// <returns>The builder instance.</returns>
    public AuditRecordBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }

    /// <summary>Sets the UTC timestamp when the action occurred.</summary>
    /// <param name="occurredAt">The occurrence timestamp.</param>
    /// <returns>The builder instance.</returns>
    public AuditRecordBuilder WithOccurredAt(DateTimeOffset occurredAt)
    {
        _occurredAt = TruncateToMilliseconds(occurredAt);
        return this;
    }

    /// <summary>Sets the actor that executed the action.</summary>
    /// <param name="actor">The actor details.</param>
    /// <returns>The builder instance.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="actor"/> is <see langword="null"/>.</exception>
    public AuditRecordBuilder WithActor(AuditActor actor)
    {
        _actor = actor ?? throw new ArgumentNullException(nameof(actor));
        return this;
    }

    /// <summary>Sets the actor details that executed the action.</summary>
    /// <param name="type">The actor type classification.</param>
    /// <param name="id">The actor identifier.</param>
    /// <param name="displayName">The optional actor display name.</param>
    /// <returns>The builder instance.</returns>
    public AuditRecordBuilder WithActor(AuditActorType type, string id, string? displayName = null)
    {
        _actor = new AuditActor(type, id, displayName);
        return this;
    }

    /// <summary>Sets the action that was performed.</summary>
    /// <param name="action">The audit action.</param>
    /// <returns>The builder instance.</returns>
    public AuditRecordBuilder WithAction(AuditAction action)
    {
        _action = action;
        return this;
    }

    /// <summary>Sets the action code that was performed.</summary>
    /// <param name="code">The action code.</param>
    /// <returns>The builder instance.</returns>
    public AuditRecordBuilder WithAction(string code)
    {
        _action = new AuditAction(code);
        return this;
    }

    /// <summary>Sets the target resource.</summary>
    /// <param name="resource">The audit resource.</param>
    /// <returns>The builder instance.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="resource"/> is <see langword="null"/>.</exception>
    public AuditRecordBuilder WithResource(AuditResource resource)
    {
        _resource = resource ?? throw new ArgumentNullException(nameof(resource));
        return this;
    }

    /// <summary>Sets the target resource details.</summary>
    /// <param name="type">The resource type.</param>
    /// <param name="id">The resource identifier.</param>
    /// <param name="aggregateType">The optional aggregate root type.</param>
    /// <param name="aggregateId">The optional aggregate root identifier.</param>
    /// <returns>The builder instance.</returns>
    public AuditRecordBuilder WithResource(string type, string id, string? aggregateType = null, string? aggregateId = null)
    {
        _resource = new AuditResource(type, id, aggregateType, aggregateId);
        return this;
    }

    /// <summary>Sets the outcome of the audited action.</summary>
    /// <param name="outcome">The outcome classification.</param>
    /// <returns>The builder instance.</returns>
    public AuditRecordBuilder WithOutcome(AuditOutcome outcome)
    {
        _outcome = outcome;
        return this;
    }

    /// <summary>Sets the tenant identifier.</summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <returns>The builder instance.</returns>
    /// <exception cref="ArgumentException"><paramref name="tenantId"/> is <see langword="null"/> or empty.</exception>
    public AuditRecordBuilder WithTenant(string tenantId)
    {
        ArgumentException.ThrowIfNullOrEmpty(tenantId);
        _tenantId = tenantId;
        return this;
    }

    /// <summary>Sets the system or component source name.</summary>
    /// <param name="source">The source identifier.</param>
    /// <returns>The builder instance.</returns>
    /// <exception cref="ArgumentException"><paramref name="source"/> is <see langword="null"/> or empty.</exception>
    public AuditRecordBuilder WithSource(string source)
    {
        ArgumentException.ThrowIfNullOrEmpty(source);
        _source = source;
        return this;
    }

    /// <summary>Sets the correlation identifier.</summary>
    /// <param name="correlationId">The correlation identifier.</param>
    /// <returns>The builder instance.</returns>
    public AuditRecordBuilder WithCorrelationId(string? correlationId)
    {
        _correlationId = correlationId;
        return this;
    }

    /// <summary>Sets the causation identifier.</summary>
    /// <param name="causationId">The causation identifier.</param>
    /// <returns>The builder instance.</returns>
    public AuditRecordBuilder WithCausationId(string? causationId)
    {
        _causationId = causationId;
        return this;
    }

    /// <summary>Sets the request identifier.</summary>
    /// <param name="requestId">The request identifier.</param>
    /// <returns>The builder instance.</returns>
    public AuditRecordBuilder WithRequestId(string? requestId)
    {
        _requestId = requestId;
        return this;
    }

    /// <summary>Sets the client IP address.</summary>
    /// <param name="ipAddress">The client IP address.</param>
    /// <returns>The builder instance.</returns>
    public AuditRecordBuilder WithIpAddress(string? ipAddress)
    {
        _ipAddress = ipAddress;
        return this;
    }

    /// <summary>Sets the user agent string.</summary>
    /// <param name="userAgent">The client user agent string.</param>
    /// <returns>The builder instance.</returns>
    public AuditRecordBuilder WithUserAgent(string? userAgent)
    {
        _userAgent = userAgent;
        return this;
    }

    /// <summary>Sets the error code if outcome is a failure.</summary>
    /// <param name="errorCode">The error code.</param>
    /// <returns>The builder instance.</returns>
    public AuditRecordBuilder WithErrorCode(string? errorCode)
    {
        _errorCode = errorCode;
        return this;
    }

    /// <summary>Sets the cryptographic integrity hash.</summary>
    /// <param name="hash">The HMAC hash.</param>
    /// <param name="previousHash">The optional preceding hash in the chain.</param>
    /// <returns>The builder instance.</returns>
    public AuditRecordBuilder WithIntegrityHash(string? hash, string? previousHash = null)
    {
        _integrityHash = hash;
        _previousHash = previousHash;
        return this;
    }

    /// <summary>Sets the cryptographic hash of the preceding record in the chain.</summary>
    /// <param name="previousHash">The previous record hash to set.</param>
    /// <returns>The builder instance.</returns>
    public AuditRecordBuilder WithPreviousHash(string? previousHash)
    {
        _previousHash = previousHash;
        return this;
    }

    /// <summary>Adds a property modification change.</summary>
    /// <param name="field">The property name.</param>
    /// <param name="oldValue">The original value.</param>
    /// <param name="newValue">The updated value.</param>
    /// <param name="isRedacted">Indicates whether the change is redacted.</param>
    /// <returns>The builder instance.</returns>
    public AuditRecordBuilder AddChange(string field, string? oldValue, string? newValue, bool isRedacted = false)
    {
        _changes ??= new List<AuditChange>();
        _changes.Add(new AuditChange(field, oldValue, newValue, isRedacted));
        return this;
    }

    /// <summary>Adds a redacted property change.</summary>
    /// <param name="field">The property name.</param>
    /// <returns>The builder instance.</returns>
    public AuditRecordBuilder AddRedactedChange(string field)
    {
        _changes ??= new List<AuditChange>();
        _changes.Add(AuditChange.Redacted(field));
        return this;
    }

    /// <summary>Sets the complete changes collection.</summary>
    /// <param name="changes">The changes collection.</param>
    /// <returns>The builder instance.</returns>
    public AuditRecordBuilder WithChanges(IEnumerable<AuditChange>? changes)
    {
        _changes = changes != null ? new List<AuditChange>(changes) : null;
        return this;
    }

    /// <summary>Builds the immutable <see cref="AuditRecord"/> instance.</summary>
    /// <returns>A constructed <see cref="AuditRecord"/>.</returns>
    public AuditRecord Build()
    {
        return new AuditRecord
        {
            Id = _id,
            OccurredAt = _occurredAt,
            Actor = _actor ?? new AuditActor(AuditActorType.SystemProcess, "system", "System"),
            Action = _action ?? AuditAction.Create,
            Resource = _resource ?? new AuditResource("General", _id.ToString()),
            Outcome = _outcome,
            ErrorCode = _errorCode,
            Changes = _changes,
            IntegrityHash = _integrityHash,
            PreviousHash = _previousHash,
            Context = new AuditContext(
                TenantId: _tenantId,
                Source: _source,
                CorrelationId: _correlationId,
                CausationId: _causationId,
                RequestId: _requestId,
                IpAddress: _ipAddress,
                UserAgent: _userAgent)
        };
    }

    private static DateTimeOffset TruncateToMilliseconds(DateTimeOffset dt)
    {
        return new DateTimeOffset(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, dt.Second, dt.Millisecond, dt.Offset);
    }
}
