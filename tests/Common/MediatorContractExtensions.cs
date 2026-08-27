// Copyright © Erickson Lopez. MIT License.
using System;
using AwesomeAssertions;

namespace EricksonLopez.Mediator.Tests.Common;

/// <summary>
/// Reusable assertion helpers for validating mediator dispatch contracts, pipeline behavior, and allocation invariants.
/// </summary>
public static class MediatorContractExtensions
{
    /// <summary>
    /// Executes the specified action and asserts that zero heap allocations occurred during execution.
    /// Performs a warm-up phase to ensure JIT compilation, static constructors, and caches are primed.
    /// </summary>
    /// <param name="action">The action to execute and measure.</param>
    /// <param name="warmupIterations">Number of warm-up iterations prior to measurement.</param>
    /// <param name="measurementIterations">Number of iterations to execute during measurement.</param>
    public static void AssertZeroAllocations(
        Action action,
        int warmupIterations = 5,
        int measurementIterations = 20)
    {
        ArgumentNullException.ThrowIfNull(action);

        // Warm-up to trigger JIT compilation and static initializers
        for (int i = 0; i < warmupIterations; i++)
        {
            action();
        }

        // Measure allocated bytes on the current thread
        long beforeAllocated = GC.GetAllocatedBytesForCurrentThread();

        for (int i = 0; i < measurementIterations; i++)
        {
            action();
        }

        long afterAllocated = GC.GetAllocatedBytesForCurrentThread();
        long totalAllocated = afterAllocated - beforeAllocated;

        totalAllocated.Should().Be(0, $"expected 0 bytes allocated across {measurementIterations} iterations, but {totalAllocated} bytes were allocated");
    }
}
