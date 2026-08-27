// Copyright © Erickson Lopez. MIT License.
using System;

namespace EricksonLopez.Mediator;

/// <summary>
/// Validates that the length of the decorated string falls within the specified bounds.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class ValidateLengthAttribute : Attribute
{
    /// <summary>
    /// Gets the minimum allowed character length.
    /// </summary>
    public int MinimumLength { get; }

    /// <summary>
    /// Gets the maximum allowed character length.
    /// </summary>
    public int MaximumLength { get; }

    /// <summary>
    /// Gets or sets the custom error message returned when validation fails.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidateLengthAttribute"/> class
    /// with the specified minimum and maximum lengths.
    /// </summary>
    /// <param name="minimumLength">The minimum allowed character length.</param>
    /// <param name="maximumLength">The maximum allowed character length.</param>
    /// <param name="errorMessage">The optional custom error message returned when validation fails.</param>
    public ValidateLengthAttribute(int minimumLength, int maximumLength, string? errorMessage = null)
    {
        MinimumLength = minimumLength;
        MaximumLength = maximumLength;
        ErrorMessage = errorMessage;
    }
}
