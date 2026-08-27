// Copyright © Erickson Lopez. MIT License.
using System;

namespace EricksonLopez.Mediator;

/// <summary>
/// Specifies that a pipeline behavior applies to the decorated request type.
/// </summary>
/// <remarks>
/// This attribute must be applied to the class or struct representing the command or query.
/// The behavior type must implement <see cref="IPipelineBehavior{TRequest, TResponse}"/>.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true)]
public sealed class UseBehaviorAttribute : Attribute
{
    /// <summary>
    /// Gets the type of the pipeline behavior to execute.
    /// </summary>
    public Type BehaviorType { get; }

    /// <summary>
    /// Gets the execution order of the behavior. Lower numbers run first.
    /// </summary>
    public int Order { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="UseBehaviorAttribute"/> class
    /// with the specified behavior type and execution order.
    /// </summary>
    /// <param name="behaviorType">The type of the pipeline behavior.</param>
    /// <param name="order">The execution order relative to other behaviors. Lower values execute first.</param>
    public UseBehaviorAttribute(Type behaviorType, int order = 0)
    {
        BehaviorType = behaviorType;
        Order = order;
    }
}
