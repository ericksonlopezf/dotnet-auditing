// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Threading;
using AwesomeAssertions;
using EricksonLopez.Auditing;
using FsCheck.Xunit;
using Xunit;

namespace EricksonLopez.Auditing.Tests;

public sealed class AuditIdTests
{
    [Fact]
    public void NewId_ReturnsNonEmptyGuid()
    {
        var id = AuditId.NewId();
        id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void NewId_ConsecutiveCalls_ProduceUniqueIds()
    {
        var ids = new HashSet<Guid>();
        for (int i = 0; i < 1000; i++)
        {
            ids.Add(AuditId.NewId()).Should().BeTrue("each generated ID must be globally unique");
        }
    }

    [Fact]
    public void NewId_TimeOrdered_MonotonicallyIncreasesAcrossMilliseconds()
    {
        var id1 = AuditId.NewId();
        var startMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        SpinWait.SpinUntil(() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() > startMs, 100);
        var id2 = AuditId.NewId();

        // In string/byte representation, UUIDv7 embeds timestamp in most significant bits
        var str1 = id1.ToString("N");
        var str2 = id2.ToString("N");

        string.CompareOrdinal(str1, str2).Should().BeLessThan(0,
            "UUIDv7 generated later in time must be lexicographically greater");
    }

    [Fact]
    public void NewId_HasVersion7AndVariantBits()
    {
        var id = AuditId.NewId();

        // ToString("D") format: xxxxxxxx-xxxx-7xxx-yxxx-xxxxxxxxxxxx where y is 8, 9, a, or b.
        var str = id.ToString("D");
        str[14].Should().Be('7', "UUID version bit must be 7");
        "89ab89AB".Should().Contain(str[19].ToString(), "UUID variant bits must be 10xx (RFC 9562)");
    }

    [Property(MaxTest = 100)]
    public bool GeneratedIds_AreAlwaysNonEmpty_AndUnique(int _)
    {
        var idA = AuditId.NewId();
        var idB = AuditId.NewId();
        return idA != Guid.Empty && idB != Guid.Empty && idA != idB;
    }
}
