// Copyright © Erickson Lopez. MIT License.
using Microsoft.CodeAnalysis;

namespace EricksonLopez.Mediator.Generator;

/// <summary>
/// Represents the discovered metadata for a notification type and its associated handlers and behaviors.
/// </summary>
/// <param name="NotificationType">The type symbol representing the notification.</param>
/// <param name="Behaviors">The collection of pipeline behavior symbols applied to this notification.</param>
/// <param name="PublishStrategy">The execution strategy name used when publishing the notification.</param>
/// <param name="Handlers">The collection of discovered notification handlers.</param>
public record NotificationDefinition(
    INamedTypeSymbol NotificationType,
    EquatableArray<INamedTypeSymbol> Behaviors,
    string PublishStrategy,
    EquatableArray<HandlerDefinition> Handlers
);
