// Copyright © Erickson Lopez. MIT License.
using System;

namespace EricksonLopez.Mediator;

/// <summary>
/// Specifies the publishing strategy applied when dispatching the decorated notification type.
/// </summary>
/// <remarks>
/// This attribute must be applied to the class or struct representing the notification.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class PublishStrategyAttribute : Attribute
{
    /// <summary>
    /// Gets the configured publishing strategy.
    /// </summary>
    public PublishStrategy Strategy { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PublishStrategyAttribute"/> class
    /// with the specified execution strategy.
    /// </summary>
    /// <param name="strategy">The publishing strategy to apply to the notification.</param>
    public PublishStrategyAttribute(PublishStrategy strategy)
    {
        Strategy = strategy;
    }
}
