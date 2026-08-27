// Copyright © Erickson Lopez. MIT License.

namespace EricksonLopez.Mediator.Generator;

/// <summary>
/// Represents a compile-time property validation rule defined on a request.
/// </summary>
/// <param name="PropertyName">The name of the property being validated.</param>
/// <param name="PropertyType">The string representation of the property type.</param>
/// <param name="RuleType">The kind of validation rule to enforce.</param>
/// <param name="CustomMessage">The optional custom error message when validation fails.</param>
/// <param name="Min">The minimum numeric bound for range validation.</param>
/// <param name="Max">The maximum numeric bound for range validation.</param>
/// <param name="MinLength">The minimum character length bound for string length validation.</param>
/// <param name="MaxLength">The maximum character length bound for string length validation.</param>
/// <param name="RegexPattern">The regular expression pattern for regex validation.</param>
public record PropertyValidation(
    string PropertyName,
    string PropertyType,
    string RuleType,
    string? CustomMessage,
    double Min,
    double Max,
    int MinLength,
    int MaxLength,
    string? RegexPattern
);
