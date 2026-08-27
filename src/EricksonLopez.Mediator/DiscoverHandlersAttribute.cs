// Copyright © Erickson Lopez. MIT License.
using System;

namespace EricksonLopez.Mediator;

/// <summary>
/// Specifies a marker type whose containing assembly is scanned at compile time for mediator handlers and behaviors.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class DiscoverHandlersAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DiscoverHandlersAttribute"/> class
    /// with the specified assembly marker type.
    /// </summary>
    /// <param name="assemblyMarkerType">A marker type located in the target assembly to scan for handlers.</param>
    public DiscoverHandlersAttribute(Type assemblyMarkerType)
    {
        AssemblyMarkerType = assemblyMarkerType;
    }

    /// <summary>
    /// Gets the marker type whose containing assembly is scanned.
    /// </summary>
    public Type AssemblyMarkerType { get; }
}
