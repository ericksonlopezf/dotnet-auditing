// Copyright © Erickson Lopez. MIT License.
using Microsoft.CodeAnalysis;

namespace EricksonLopez.Mediator.Generator;

/// <summary>
/// Represents the aggregated compilation model containing all discovered mediator components.
/// </summary>
/// <param name="CommandHandlers">The discovered command handlers.</param>
/// <param name="QueryHandlers">The discovered query handlers.</param>
/// <param name="NotificationHandlers">The discovered notification handlers.</param>
/// <param name="Notifications">The discovered notification definitions grouped by notification type.</param>
/// <param name="StreamHandlers">The discovered stream request handlers.</param>
/// <param name="AllBehaviors">All discovered pipeline behaviors across the compilation.</param>
/// <param name="ResultResponseTypes">All response types implementing the result pattern.</param>
public record MediatorModel(
    EquatableArray<HandlerDefinition> CommandHandlers,
    EquatableArray<HandlerDefinition> QueryHandlers,
    EquatableArray<HandlerDefinition> NotificationHandlers,
    EquatableArray<NotificationDefinition> Notifications,
    EquatableArray<HandlerDefinition> StreamHandlers,
    EquatableArray<BehaviorDefinition> AllBehaviors,
    EquatableArray<INamedTypeSymbol> ResultResponseTypes
);
