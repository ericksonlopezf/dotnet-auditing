// Copyright © Erickson Lopez. MIT License.
using System;

namespace EricksonLopez.Mediator.Polly;

/// <summary>
/// Specifies the key of the Polly resilience pipeline to apply to the decorated request type.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class UseResiliencePipelineAttribute : Attribute
{
    /// <summary>
    /// Gets the key of the resilience pipeline configured in the Polly registry.
    /// </summary>
    public string PipelineKey { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="UseResiliencePipelineAttribute"/> class with the specified pipeline key.
    /// </summary>
    /// <param name="pipelineKey">The unique key identifying the resilience pipeline.</param>
    /// <exception cref="ArgumentNullException"><paramref name="pipelineKey"/> is <see langword="null"/>.</exception>
    public UseResiliencePipelineAttribute(string pipelineKey)
    {
        PipelineKey = pipelineKey ?? throw new ArgumentNullException(nameof(pipelineKey));
    }
}
