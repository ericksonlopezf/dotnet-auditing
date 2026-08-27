// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace EricksonLopez.Auditing.Tests.Common;

[ExcludeFromCodeCoverage]
internal sealed class FakeDbDataReader : DbDataReader
{
    private readonly string[] _fieldNames;
    private readonly List<object?[]> _rows;
    private int _currentIndex = -1;

    public FakeDbDataReader(string[] fieldNames, List<object?[]> rows)
    {
        _fieldNames = fieldNames;
        _rows = rows;
    }

    public override int FieldCount => _fieldNames.Length;
    public override bool HasRows => _rows.Count > 0;
    public override bool IsClosed => false;
    public override int RecordsAffected => _rows.Count;

    public override bool Read()
    {
        _currentIndex++;
        return _currentIndex < _rows.Count;
    }

    public override Task<bool> ReadAsync(CancellationToken cancellationToken) => Task.FromResult(Read());

    public override string GetName(int ordinal) => _fieldNames[ordinal];
    public override int GetOrdinal(string name) => Array.FindIndex(_fieldNames, n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));
    public override object GetValue(int ordinal) => _rows[_currentIndex][ordinal] ?? DBNull.Value;
    public override int GetValues(object[] values)
    {
        int count = Math.Min(values.Length, _fieldNames.Length);
        for (int i = 0; i < count; i++) values[i] = GetValue(i);
        return count;
    }
    public override bool IsDBNull(int ordinal) => _rows[_currentIndex][ordinal] is null || _rows[_currentIndex][ordinal] is DBNull;

    [UnconditionalSuppressMessage("Trimming", "IL2073", Justification = "Test fake implementation returns runtime object Type")]
    [return: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.PublicProperties)]
    public override Type GetFieldType(int ordinal) => _rows.Count > 0 && _rows[0][ordinal] != null ? _rows[0][ordinal]!.GetType() : typeof(object);

    public override string GetDataTypeName(int ordinal) => GetFieldType(ordinal).Name;
    public override System.Collections.IEnumerator GetEnumerator() => throw new NotImplementedException();
    public override int Depth => 0;
    public override bool NextResult() => false;
    public override bool GetBoolean(int ordinal) => Convert.ToBoolean(GetValue(ordinal), CultureInfo.InvariantCulture);
    public override byte GetByte(int ordinal) => Convert.ToByte(GetValue(ordinal), CultureInfo.InvariantCulture);
    public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length) => 0;
    public override char GetChar(int ordinal) => Convert.ToChar(GetValue(ordinal), CultureInfo.InvariantCulture);
    public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length) => 0;
    public override DateTime GetDateTime(int ordinal) => Convert.ToDateTime(GetValue(ordinal), CultureInfo.InvariantCulture);
    public override decimal GetDecimal(int ordinal) => Convert.ToDecimal(GetValue(ordinal), CultureInfo.InvariantCulture);
    public override double GetDouble(int ordinal) => Convert.ToDouble(GetValue(ordinal), CultureInfo.InvariantCulture);
    public override float GetFloat(int ordinal) => Convert.ToSingle(GetValue(ordinal), CultureInfo.InvariantCulture);
    public override Guid GetGuid(int ordinal)
    {
        var val = GetValue(ordinal);
        return val is Guid g ? g : Guid.Parse(val.ToString()!);
    }
    public override short GetInt16(int ordinal) => Convert.ToInt16(GetValue(ordinal), CultureInfo.InvariantCulture);
    public override int GetInt32(int ordinal) => Convert.ToInt32(GetValue(ordinal), CultureInfo.InvariantCulture);
    public override long GetInt64(int ordinal) => Convert.ToInt64(GetValue(ordinal), CultureInfo.InvariantCulture);
    public override string GetString(int ordinal) => GetValue(ordinal)?.ToString() ?? string.Empty;
    public override object this[int ordinal] => GetValue(ordinal);
    public override object this[string name] => GetValue(GetOrdinal(name));
}
