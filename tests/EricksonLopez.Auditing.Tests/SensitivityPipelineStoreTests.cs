// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AwesomeAssertions;
using EricksonLopez.Auditing;
using EricksonLopez.Auditing.Testing;
using Xunit;

namespace EricksonLopez.Auditing.Tests;

public sealed class SensitivityPipelineStoreTests
{
    [Fact]
    public async Task Sanitize_RecordWithSensitiveField_RedactsChangeValues()
    {
        var config = new AuditConfiguration();
        var pipeline = new AuditSensitivityPipeline(config);

        var record = AuditRecordBuilder.Create()
            .WithAction("UPDATE_USER")
            .AddChange("Email", "old@test.com", "new@test.com")
            .AddChange("Password", "secret123", "secret456")
            .AddRedactedChange("Salary")
            .Build();

        var sanitized = await pipeline.SanitizeAsync(record);

        sanitized.Changes.Should().NotBeNull();
        // Password is in GlobalFieldDenylist so it is suppressed; Email and Salary remain.
        sanitized.Changes!.Count.Should().Be(2);

        var emailChange = sanitized.Changes[0];
        emailChange.Field.Should().Be("Email");
        emailChange.OldValue.Should().Be("old@test.com");
        emailChange.NewValue.Should().Be("new@test.com");
        emailChange.IsRedacted.Should().BeFalse();

        var salaryChange = sanitized.Changes[1];
        salaryChange.Field.Should().Be("Salary");
        salaryChange.OldValue.Should().BeNull();
        salaryChange.NewValue.Should().BeNull();
        salaryChange.IsRedacted.Should().BeTrue();
    }

    [Fact]
    public async Task Sanitize_RecordWithoutChanges_ReturnsEquivalentRecord()
    {
        var config = new AuditConfiguration();
        var pipeline = new AuditSensitivityPipeline(config);

        var record = AuditRecordBuilder.Create()
            .WithAction("VIEW")
            .Build();

        var sanitized = await pipeline.SanitizeAsync(record);

        sanitized.Should().BeEquivalentTo(record);
    }
}



