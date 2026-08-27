// Copyright © Erickson Lopez. MIT License.
using System;

namespace EricksonLopez.Mediator;

/// <summary>
/// Validates that the decorated string matches the specified regular expression pattern.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class ValidateRegexAttribute : Attribute
{
    /// <summary>
    /// Gets the regular expression pattern used for validation.
    /// </summary>
    public string Pattern { get; }

    /// <summary>
    /// Gets or sets the custom error message returned when validation fails.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidateRegexAttribute"/> class
    /// with the specified pattern.
    /// </summary>
    /// <param name="pattern">The regular expression pattern used for validation.</param>
    /// <param name="errorMessage">The optional custom error message returned when validation fails.</param>
    public ValidateRegexAttribute(string pattern, string? errorMessage = null)
    {
        Pattern = pattern;
        ErrorMessage = errorMessage;
    }
}
