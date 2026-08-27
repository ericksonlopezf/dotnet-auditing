// Copyright © Erickson Lopez. MIT License.
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;

namespace EricksonLopez.Auditing.Tests.Common;

[ExcludeFromCodeCoverage]
internal sealed class FakeDbTransaction : DbTransaction
{
    private readonly FakeDbConnection _connection;
    public FakeDbTransaction(FakeDbConnection connection) => _connection = connection;
    public override IsolationLevel IsolationLevel => IsolationLevel.ReadCommitted;
    protected override DbConnection DbConnection => _connection;
    public bool Committed { get; private set; }
    public bool RolledBack { get; private set; }
    public override void Commit() => Committed = true;
    public override void Rollback() => RolledBack = true;
}
