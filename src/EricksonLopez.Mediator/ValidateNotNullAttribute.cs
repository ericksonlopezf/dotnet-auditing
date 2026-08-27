// Copyright © Erickson Lopez. MIT License.
using System;

namespace EricksonLopez.Mediator;

/// <summary>
/// Validates that the decorated property is not <see langword="null"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class ValidateNotNullAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the custom error message returned when validation fails.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidateNotNullAttribute"/> class.
    /// </summary>
    /// <param name="errorMessage">The optional custom error message returned when validation fails.</param>
    public ValidateNotNullAttribute(string? errorMessage = null)
    {
        ErrorMessage = errorMessage;
    }
}
