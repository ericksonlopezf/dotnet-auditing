// Copyright © Erickson Lopez. MIT License.
using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using EricksonLopez.Auditing;

namespace EricksonLopez.Auditing.Benchmarks;

[MemoryDiagnoser]
public class SensitivityPipelineBenchmarks
{
    private AuditSensitivityPipeline _pipeline = null!;
    private List<AuditChange> _changes = null!;

    [GlobalSetup]
    public void Setup()
    {
        var config = new AuditConfiguration();
        _pipeline = new AuditSensitivityPipeline(config);

        _changes = new List<AuditChange>
        {
            new AuditChange("Username", "oldUser", "newUser"),
            new AuditChange("Password", "plainOldPass", "plainNewPass"),
            new AuditChange("Email", "old@example.com", "new@example.com"),
            new AuditChange("CreditCardNumber", "4111111111111111", "4222222222222222"),
            new AuditChange("Status", "Pending", "Active"),
            new AuditChange("ApiKey", "secret-key-12345", "secret-key-67890"),
            new AuditChange("Amount", "100.00", "200.00"),
            new AuditChange("TaxId", "123-45-6789", "987-65-4321")
        };
    }

    [Benchmark]
    public IReadOnlyList<AuditChange>? ApplySensitivityPipeline()
    {
        return _pipeline.Apply(_changes);
    }
}
