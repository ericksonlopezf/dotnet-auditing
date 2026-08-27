// Copyright © Erickson Lopez. MIT License.
using System;
using System.Data;

namespace EricksonLopez.Auditing.PostgreSql;

/// <summary>Represents configuration options for <see cref="PostgreSqlAuditStore"/>.</summary>
public sealed class PostgreSqlAuditStoreOptions
{
    /// <summary>
    /// Gets or sets the factory function that creates open database connections for executing audit commands.
    /// </summary>
    public Func<IDbConnection> ConnectionFactory { get; set; } = null!;

    /// <summary>Gets or sets the PostgreSQL schema containing the audit table.</summary>
    public string Schema { get; set; } = "audit";

    /// <summary>Gets or sets the table name where audit records are stored.</summary>
    public string Table { get; set; } = "records";
}
