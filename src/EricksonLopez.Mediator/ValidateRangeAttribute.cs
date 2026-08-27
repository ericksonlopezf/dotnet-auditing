// Copyright © Erickson Lopez. MIT License.
using System;

namespace EricksonLopez.Mediator;

/// <summary>
/// Validates that the decorated numeric value falls within the specified range.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class ValidateRangeAttribute : Attribute
{
    /// <summary>
    /// Gets the minimum acceptable numeric value.
    /// </summary>
    public double Minimum { get; }

    /// <summary>
    /// Gets the maximum acceptable numeric value.
    /// </summary>
    public double Maximum { get; }

    /// <summary>
    /// Gets or sets the custom error message returned when validation fails.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidateRangeAttribute"/> class
    /// with the specified minimum and maximum bounds.
    /// </summary>
    /// <param name="minimum">The minimum acceptable numeric value.</param>
    /// <param name="maximum">The maximum acceptable numeric value.</param>
    /// <param name="errorMessage">The optional custom error message returned when validation fails.</param>
    public ValidateRangeAttribute(double minimum, double maximum, string? errorMessage = null)
    {
        Minimum = minimum;
        Maximum = maximum;
        ErrorMessage = errorMessage;
    }
}
