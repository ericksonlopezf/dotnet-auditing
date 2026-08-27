// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace EricksonLopez.Mediator.Generator;

/// <summary>
/// Represents an immutable array that provides value-based equality semantics.
/// </summary>
/// <remarks>
/// This struct wraps an underlying array and computes value equality based on sequence elements.
/// </remarks>
/// <typeparam name="T">The type of elements in the array.</typeparam>
public readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>, IReadOnlyList<T>
{
    private readonly T[]? _array;

    /// <summary>
    /// Initializes a new instance of the <see cref="EquatableArray{T}"/> struct with the specified array.
    /// </summary>
    /// <param name="array">The underlying array to wrap.</param>
    public EquatableArray(T[]? array)
    {
        _array = array;
    }

    /// <summary>
    /// Gets the element at the specified zero-based index.
    /// </summary>
    /// <param name="index">The zero-based index of the element to retrieve.</param>
    /// <returns>The element at the specified index.</returns>
    /// <exception cref="IndexOutOfRangeException"><paramref name="index"/> is less than zero or greater than or equal to <see cref="Count"/></exception>
    public T this[int index] => _array![index];

    /// <summary>
    /// Gets the number of elements contained in the array.
    /// </summary>
    public int Count => _array?.Length ?? 0;

    /// <summary>
    /// Determines whether the specified <see cref="EquatableArray{T}"/> is equal to the current instance.
    /// </summary>
    /// <param name="other">The array to compare with the current instance.</param>
    /// <returns><see langword="true"/> if the specified array is equal to the current instance; otherwise, <see langword="false"/>.</returns>
    public bool Equals(EquatableArray<T> other)
    {
        if (_array == null && other._array == null) return true;
        if (_array == null || other._array == null) return false;
        if (_array.Length != other._array.Length) return false;

        for (int i = 0; i < _array.Length; i++)
        {
            if (!EqualityComparer<T>.Default.Equals(_array[i], other._array[i]))
                return false;
        }
        return true;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        return obj is EquatableArray<T> array && Equals(array);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        if (_array == null) return 0;

        unchecked
        {
            int hash = 17;
            foreach (var item in _array)
            {
                hash = hash * 31 + (item == null ? 0 : item.GetHashCode());
            }
            return hash;
        }
    }

    /// <summary>
    /// Creates a read-only span over the underlying array.
    /// </summary>
    /// <returns>A read-only span covering the elements of the array.</returns>
    public ReadOnlySpan<T> AsSpan() => _array;

    /// <summary>
    /// Returns an enumerator that iterates through the collection.
    /// </summary>
    /// <returns>An enumerator that can be used to iterate through the collection.</returns>
    public IEnumerator<T> GetEnumerator()
    {
        if (_array == null) yield break;
        foreach (var item in _array)
        {
            yield return item;
        }
    }

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Creates a new <see cref="List{T}"/> containing the elements of the array.
    /// </summary>
    /// <returns>A new list containing the elements of the array.</returns>
    public List<T> ToList() => _array == null ? new List<T>() : new List<T>(_array);

    /// <summary>
    /// Defines an implicit conversion from an array to an <see cref="EquatableArray{T}"/>.
    /// </summary>
    /// <param name="array">The array to convert.</param>
    public static implicit operator EquatableArray<T>(T[] array) => new(array);

    /// <summary>
    /// Determines whether two specified instances of <see cref="EquatableArray{T}"/> are equal.
    /// </summary>
    /// <param name="left">The first array to compare.</param>
    /// <param name="right">The second array to compare.</param>
    /// <returns><see langword="true"/> if the arrays are equal; otherwise, <see langword="false"/>.</returns>
    public static bool operator ==(EquatableArray<T> left, EquatableArray<T> right) => left.Equals(right);

    /// <summary>
    /// Determines whether two specified instances of <see cref="EquatableArray{T}"/> are not equal.
    /// </summary>
    /// <param name="left">The first array to compare.</param>
    /// <param name="right">The second array to compare.</param>
    /// <returns><see langword="true"/> if the arrays are not equal; otherwise, <see langword="false"/>.</returns>
    public static bool operator !=(EquatableArray<T> left, EquatableArray<T> right) => !left.Equals(right);
}
