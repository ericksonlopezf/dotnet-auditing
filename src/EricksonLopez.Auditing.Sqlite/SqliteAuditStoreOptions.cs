// Copyright © Erickson Lopez. MIT License.
using System;
using System.Data;
using System.Text.RegularExpressions;

namespace EricksonLopez.Auditing.Sqlite;

/// <summary>Represents configuration options for <see cref="SqliteAuditStore"/>.</summary>
public sealed partial class SqliteAuditStoreOptions
{
    private string _table = "audit_records";

    [GeneratedRegex("^[a-zA-Z_][a-zA-Z0-9_]*$")]
    private static partial Regex IdentifierRegex();

    /// <summary>
    /// Gets or sets the factory function that creates open database connections for executing audit commands.
    /// </summary>
    public Func<IDbConnection> ConnectionFactory { get; set; } = null!;

    /// <summary>Gets or sets the table name where audit records are stored.</summary>
    /// <exception cref="ArgumentException"><paramref name="value"/> is <see langword="null"/>, empty, white space, or not a valid SQL identifier</exception>
    public string Table
    {
        get => _table;
        set
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            if (!IdentifierRegex().IsMatch(value))
                throw new ArgumentException($"Table '{value}' is not a valid SQL identifier. It must match ^[a-zA-Z_][a-zA-Z0-9_]*$", nameof(value));
            _table = value;
        }
    }
}
