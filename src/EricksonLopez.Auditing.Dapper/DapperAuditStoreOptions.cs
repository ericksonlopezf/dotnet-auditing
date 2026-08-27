// Copyright © Erickson Lopez. MIT License.
using System;
using System.Data;

namespace EricksonLopez.Auditing.Dapper;

/// <summary>Represents configuration options for <see cref="DapperAuditStore"/>.</summary>
public sealed class DapperAuditStoreOptions
{
    /// <summary>
    /// Gets or sets the factory function that creates open database connections for executing audit commands.
    /// </summary>
    public Func<IDbConnection> ConnectionFactory { get; set; } = null!;

    /// <summary>Gets or sets the database table name where audit records are stored.</summary>
    public string Table { get; set; } = "audit_records";
}
