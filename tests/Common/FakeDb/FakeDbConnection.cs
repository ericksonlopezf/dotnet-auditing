// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

namespace EricksonLopez.Auditing.Tests.Common;

[ExcludeFromCodeCoverage]
internal sealed class FakeDbConnection : DbConnection
{
    private ConnectionState _state = ConnectionState.Closed;

    public int OpenCount { get; private set; }
    public List<FakeDbCommand> ExecutedCommands { get; } = new();
    public List<FakeDbTransaction> CreatedTransactions { get; } = new();
    public Queue<Func<FakeDbCommand, DbDataReader>> ReaderQueues { get; } = new();
    public Queue<int> NonQueryResults { get; } = new();

    [AllowNull]
    public override string ConnectionString { get; set; } = "FakeConnection";
    public override string Database => "FakeDb";
    public override string DataSource => "FakeDataSource";
    public override string ServerVersion => "1.0";
    public override ConnectionState State => _state;

    public void SetState(ConnectionState state) => _state = state;
    public override void ChangeDatabase(string databaseName) { }
    public override void Close() => _state = ConnectionState.Closed;
    public bool FailOnOpen { get; set; }

    public override void Open()
    {
        if (FailOnOpen) throw new InvalidOperationException("Simulated connection open failure.");
        _state = ConnectionState.Open;
        OpenCount++;
    }

    public override Task OpenAsync(CancellationToken cancellationToken)
    {
        if (FailOnOpen) return Task.FromException(new InvalidOperationException("Simulated connection open failure."));
        Open();
        return Task.CompletedTask;
    }

    public bool EnforceAsyncTransaction { get; set; }
    public bool EnforceOpenOnCreateCommand { get; set; }

    protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
    {
        if (EnforceAsyncTransaction)
        {
            throw new InvalidOperationException("Synchronous BeginDbTransaction was invoked when asynchronous transaction is required.");
        }
        if (_state != ConnectionState.Open)
        {
            throw new InvalidOperationException("Connection must be open to begin a transaction.");
        }
        var tx = new FakeDbTransaction(this);
        CreatedTransactions.Add(tx);
        return tx;
    }

    protected override ValueTask<DbTransaction> BeginDbTransactionAsync(IsolationLevel isolationLevel, CancellationToken cancellationToken)
    {
        if (_state != ConnectionState.Open)
        {
            throw new InvalidOperationException("Connection must be open to begin a transaction.");
        }
        var tx = new FakeDbTransaction(this);
        CreatedTransactions.Add(tx);
        return new ValueTask<DbTransaction>(tx);
    }

    protected override DbCommand CreateDbCommand()
    {
        if (EnforceOpenOnCreateCommand && _state != ConnectionState.Open)
        {
            throw new InvalidOperationException("Connection must be open when CreateCommand is called.");
        }
        return new FakeDbCommand(this);
    }
}



