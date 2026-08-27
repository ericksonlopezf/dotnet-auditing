// Copyright © Erickson Lopez. MIT License.
using Microsoft.CodeAnalysis;

namespace EricksonLopez.Mediator.Generator;

/// <summary>
/// Represents the metadata of a discovered pipeline behavior and its service lifetime.
/// </summary>
/// <param name="BehaviorType">The type symbol representing the pipeline behavior.</param>
/// <param name="Lifetime">The configured dependency injection lifetime for the behavior.</param>
public record BehaviorDefinition(
    INamedTypeSymbol BehaviorType,
    string Lifetime
);
