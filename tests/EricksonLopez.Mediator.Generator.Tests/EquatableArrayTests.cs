// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Linq;
using EricksonLopez.Mediator.Generator;
using Xunit;

namespace EricksonLopez.Mediator.Generator.Tests;

public class EquatableArrayTests
{
    [Fact]
    public void Constructor_WithNullArray_CreatesEmptyArray()
    {
        var array = new EquatableArray<string>(null);
        Assert.True(array.Count == 0);
        Assert.Empty(array);
        Assert.Empty(array.ToList());
    }

    [Fact]
    public void Constructor_WithArray_WrapsArray()
    {
        var source = new[] { "A", "B", "C" };
        var array = new EquatableArray<string>(source);
        Assert.Equal(3, array.Count);
        Assert.Equal("A", array[0]);
        Assert.Equal("B", array[1]);
        Assert.Equal("C", array[2]);
    }

    [Fact]
    public void Indexer_Throws_WhenOutOfBounds()
    {
        var source = new[] { "A" };
        var array = new EquatableArray<string>(source);

        Assert.Throws<IndexOutOfRangeException>(() => _ = array[1]);
    }

    [Fact]
    public void Equals_WithBothNull_ReturnsTrue()
    {
        var array1 = new EquatableArray<string>(null);
        var array2 = new EquatableArray<string>(null);

        Assert.True(array1.Equals(array2));
        Assert.True(array1.Equals((object)array2));
    }

    [Fact]
    public void Equals_WithOneNull_ReturnsFalse()
    {
        var array1 = new EquatableArray<string>(null);
        var array2 = new EquatableArray<string>(new[] { "A" });

        Assert.False(array1.Equals(array2));
        Assert.False(array2.Equals(array1));
        Assert.False(array1.Equals((object)array2));
    }

    [Fact]
    public void Equals_WithDifferentLengths_ReturnsFalse()
    {
        var array1 = new EquatableArray<string>(new[] { "A" });
        var array2 = new EquatableArray<string>(new[] { "A", "B" });

        Assert.False(array1.Equals(array2));
    }

    [Fact]
    public void Equals_WithDifferentElements_ReturnsFalse()
    {
        var array1 = new EquatableArray<string>(new[] { "A", "B" });
        var array2 = new EquatableArray<string>(new[] { "A", "C" });

        Assert.False(array1.Equals(array2));
    }

    [Fact]
    public void Equals_WithSameElements_ReturnsTrue()
    {
        var array1 = new EquatableArray<string>(new[] { "A", "B" });
        var array2 = new EquatableArray<string>(new[] { "A", "B" });

        Assert.True(array1.Equals(array2));
    }

    [Fact]
    public void Equals_Object_WithWrongType_ReturnsFalse()
    {
        var array = new EquatableArray<string>(new[] { "A" });
        Assert.False(array.Equals("A"));
        Assert.False(array.Equals((object?)null));
    }

    [Fact]
    public void GetHashCode_WithNullArray_ReturnsZero()
    {
        var array = new EquatableArray<string>(null);
        Assert.Equal(0, array.GetHashCode());
    }

    [Fact]
    public void GetHashCode_WithNullElements_ComputesHash()
    {
        var array = new EquatableArray<string?>(new string?[] { null, "B" });
        var hash = array.GetHashCode();
        Assert.NotEqual(0, hash);
    }

    [Fact]
    public void GetHashCode_WithSameElements_ComputesSameHash()
    {
        var array1 = new EquatableArray<string>(new[] { "A", "B" });
        var array2 = new EquatableArray<string>(new[] { "A", "B" });

        Assert.Equal(array1.GetHashCode(), array2.GetHashCode());
    }

    [Fact]
    public void GetHashCode_WithDifferentElements_ComputesDifferentHash()
    {
        var array1 = new EquatableArray<string>(new[] { "A" });
        var array2 = new EquatableArray<string>(new[] { "B" });

        Assert.NotEqual(array1.GetHashCode(), array2.GetHashCode());
    }

    [Fact]
    public void GetHashCode_WithDifferentOrder_ComputesDifferentHash()
    {
        var array1 = new EquatableArray<string>(new[] { "A", "B" });
        var array2 = new EquatableArray<string>(new[] { "B", "A" });

        Assert.NotEqual(array1.GetHashCode(), array2.GetHashCode());
    }

    [Fact]
    public void GetHashCode_ExactCalculation_MatchesContract()
    {
        var array = new EquatableArray<string>(new[] { "A" });
        int expected = unchecked(17 * 31 + "A".GetHashCode());
        Assert.Equal(expected, array.GetHashCode());
    }

    [Fact]
    public void AsSpan_ReturnsUnderlyingSpan()
    {
        var source = new[] { "A", "B" };
        var array = new EquatableArray<string>(source);

        var span = array.AsSpan();
        Assert.Equal(2, span.Length);
        Assert.Equal("A", span[0]);
    }

    [Fact]
    public void GetEnumerator_ReturnsItems()
    {
        var source = new[] { "A", "B" };
        var array = new EquatableArray<string>(source);

        var list = new List<string>();
        foreach (var item in array)
        {
            list.Add(item);
        }

        Assert.Equal(new[] { "A", "B" }, list);
    }

    [Fact]
    public void GetEnumerator_WithNull_ReturnsEmpty()
    {
        var array = new EquatableArray<string>(null);

        var list = new List<string>();
        foreach (var item in array)
        {
            list.Add(item);
        }

        Assert.Empty(list);
    }

    [Fact]
    public void IEnumerableGetEnumerator_ReturnsItems()
    {
        var source = new[] { "A" };
        System.Collections.IEnumerable array = new EquatableArray<string>(source);

        var enumerator = array.GetEnumerator();
        Assert.True(enumerator.MoveNext());
        Assert.Equal("A", enumerator.Current);
        Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public void ToList_WithElements_ReturnsList()
    {
        var source = new[] { "A", "B" };
        var array = new EquatableArray<string>(source);

        var list = array.ToList();
        Assert.Equal(new[] { "A", "B" }, list);
    }

    [Fact]
    public void ImplicitOperator_FromArray_CreatesEquatableArray()
    {
        var source = new[] { "A", "B" };
        EquatableArray<string> array = source;

        Assert.Equal(2, array.Count);
        Assert.Equal("A", array[0]);
    }

    [Fact]
    public void EqualityOperators_WithEqualAndUnequalArrays_ReturnExpectedResults()
    {
        var array1 = new EquatableArray<string>(new[] { "A", "B" });
        var array2 = new EquatableArray<string>(new[] { "A", "B" });
        var array3 = new EquatableArray<string>(new[] { "A", "C" });
        var arrayNull1 = new EquatableArray<string>(null);
        var arrayNull2 = new EquatableArray<string>(null);

        Assert.True(array1 == array2);
        Assert.False(array1 != array2);

        Assert.False(array1 == array3);
        Assert.True(array1 != array3);

        Assert.True(arrayNull1 == arrayNull2);
        Assert.False(arrayNull1 != arrayNull2);

        Assert.False(array1 == arrayNull1);
        Assert.True(array1 != arrayNull1);
    }
}
