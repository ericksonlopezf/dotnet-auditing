// Copyright © Erickson Lopez. MIT License.
using System;
using AwesomeAssertions;
using Xunit;

namespace EricksonLopez.Auditing.Abstractions.Tests;

public sealed class AmbientAuditTimeProviderTests
{
    private readonly AmbientAuditTimeProvider _provider = new();

    [Fact]
    public void GetUtcNow_WithoutScope_ShouldReturnRecentUtcTime()
    {
        var before = DateTimeOffset.UtcNow;
        var actual = _provider.GetUtcNow();
        var after = DateTimeOffset.UtcNow;

        actual.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public void GetUtcNow_WithOperationTimeScope_ShouldReturnFrozenTimestamp()
    {
        var expected = new DateTimeOffset(2026, 8, 31, 12, 0, 0, TimeSpan.Zero);

        using (AmbientAuditTimeProvider.BeginScope(expected))
        {
            _provider.GetUtcNow().Should().Be(expected);
        }

        var afterDispose = _provider.GetUtcNow();
        afterDispose.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void GetUtcNow_NestedScopes_ShouldRestoreParentTimestamp()
    {
        var parentTime = new DateTimeOffset(2026, 8, 31, 10, 0, 0, TimeSpan.Zero);
        var childTime = new DateTimeOffset(2026, 8, 31, 11, 0, 0, TimeSpan.Zero);

        using (AmbientAuditTimeProvider.BeginScope(parentTime))
        {
            _provider.GetUtcNow().Should().Be(parentTime);

            using (AmbientAuditTimeProvider.BeginScope(childTime))
            {
                _provider.GetUtcNow().Should().Be(childTime);
            }

            _provider.GetUtcNow().Should().Be(parentTime);
        }
    }
}



