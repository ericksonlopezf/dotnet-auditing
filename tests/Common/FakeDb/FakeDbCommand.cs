// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;

namespace EricksonLopez.Auditing.Tests.Common;

[ExcludeFromCodeCoverage]
internal sealed class FakeDbCommand : DbCommand
{
    private readonly FakeDbConnection _connection;
    public FakeDbCommand(FakeDbConnection connection)
    {
        _connection = connection;
        DbParameterCollection = new FakeDbParameterCollection();
    }
    [AllowNull]
    public override string CommandText { get; set; } = string.Empty;
    public override int CommandTimeout { get; set; }
    public override CommandType CommandType { get; set; } = CommandType.Text;
    public override bool DesignTimeVisible { get; set; }
    public override UpdateRowSource UpdatedRowSource { get; set; }
    protected override DbConnection? DbConnection { get => _connection; set { } }
    protected override DbParameterCollection DbParameterCollection { get; }
    protected override DbTransaction? DbTransaction { get; set; }
    public override void Cancel() { }
    protected override DbParameter CreateDbParameter() => new FakeDbParameter();
    protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
    {
        if (_connection.State != ConnectionState.Open)
        {
            throw new InvalidOperationException("Connection must be open to execute reader.");
        }
        _connection.ExecutedCommands.Add(this);
        if (_connection.ReaderQueues.Count > 0)
        {
            return _connection.ReaderQueues.Dequeue()(this);
        }
        return new FakeDbDataReader(Array.Empty<string>(), new List<object?[]>());
    }
    public override int ExecuteNonQuery()
    {
        if (_connection.State != ConnectionState.Open)
        {
            throw new InvalidOperationException("Connection must be open to execute non-query.");
        }
        _connection.ExecutedCommands.Add(this);
        if (_connection.NonQueryResults.Count > 0)
        {
            return _connection.NonQueryResults.Dequeue();
        }
        return 1;
    }
    public override object? ExecuteScalar()
    {
        if (_connection.State != ConnectionState.Open)
        {
            throw new InvalidOperationException("Connection must be open to execute scalar.");
        }
        _connection.ExecutedCommands.Add(this);
        return null;
    }
    public override void Prepare() { }
}
