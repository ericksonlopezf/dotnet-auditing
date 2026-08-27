// Copyright © Erickson Lopez. MIT License.
using System;

namespace EricksonLopez.Mediator;

/// <summary>
/// Enables compile-time validation code generation for the decorated request type.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class ValidateRequestAttribute : Attribute
{
}
