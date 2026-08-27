// Copyright © Erickson Lopez. MIT License.
using System;
using System.Data;

namespace EricksonLopez.Auditing.Oracle;

/// <summary>Represents configuration options for <see cref="OracleAuditStore"/>.</summary>
public sealed class OracleAuditStoreOptions
{
    /// <summary>
    /// Gets or sets the factory function that creates open database connections for executing audit commands.
    /// </summary>
    public Func<IDbConnection> ConnectionFactory { get; set; } = null!;

    /// <summary>Gets or sets the database schema or owner containing the audit table.</summary>
    public string Schema { get; set; } = "AUDIT_USER";

    /// <summary>Gets or sets the table name where audit records are stored.</summary>
    public string Table { get; set; } = "AUDIT_RECORDS";
}
