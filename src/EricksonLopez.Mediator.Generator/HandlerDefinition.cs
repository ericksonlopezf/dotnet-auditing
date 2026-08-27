// Copyright © Erickson Lopez. MIT License.
using Microsoft.CodeAnalysis;

namespace EricksonLopez.Mediator.Generator;

/// <summary>
/// Represents the metadata of a discovered mediator handler.
/// </summary>
/// <param name="HandlerType">The type symbol of the handler.</param>
/// <param name="RequestType">The type symbol of the request processed by the handler.</param>
/// <param name="ResponseType">The type symbol of the response produced by the handler.</param>
/// <param name="Behaviors">The pipeline behaviors applicable to this handler.</param>
/// <param name="Lifetime">The configured dependency injection lifetime.</param>
/// <param name="Validations">The collection of property validation rules for the request.</param>
public record HandlerDefinition(
    INamedTypeSymbol HandlerType,
    INamedTypeSymbol RequestType,
    INamedTypeSymbol ResponseType,
    EquatableArray<INamedTypeSymbol> Behaviors,
    string Lifetime,
    EquatableArray<PropertyValidation> Validations = default
);
