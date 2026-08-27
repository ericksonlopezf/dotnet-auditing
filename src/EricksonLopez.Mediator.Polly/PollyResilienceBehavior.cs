// Copyright © Erickson Lopez. MIT License.
using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using global::Polly;
using global::Polly.Registry;

namespace EricksonLopez.Mediator.Polly;

/// <summary>
/// Represents a pipeline behavior that executes requests within a Polly resilience pipeline.
/// </summary>
/// <remarks>
/// <para>
/// If <typeparamref name="TRequest"/> is decorated with <see cref="UseResiliencePipelineAttribute"/>,
/// this behavior resolves the named resilience pipeline from <see cref="ResiliencePipelineProvider{TKey}"/> and
/// wraps handler execution with the configured Polly strategy (retry, circuit breaker, timeout, etc.).
/// </para>
/// <para>
/// <strong>AOT / Trimming:</strong> This type uses <c>GetCustomAttribute</c> in a closed-generic
/// static initializer to cache the pipeline key. The <see cref="System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembersAttribute"/>
/// on <typeparamref name="TRequest"/> ensures the <see cref="UseResiliencePipelineAttribute"/> metadata is preserved
/// under trimming when applied by the Source Generator or manually.
/// </para>
/// </remarks>
/// <typeparam name="TRequest">The type of the request being handled.</typeparam>
/// <typeparam name="TResponse">The type of the response expected from the pipeline.</typeparam>
public sealed class PollyResilienceBehavior<[System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.All)] TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
{
    private static readonly string? ConfiguredPipelineKey =
        typeof(TRequest).GetCustomAttribute<UseResiliencePipelineAttribute>()?.PipelineKey;

    private readonly ResiliencePipelineProvider<string>? _pipelineProvider;
    private readonly ResiliencePipeline? _defaultPipeline;

    /// <summary>
    /// Initializes a new instance of the <see cref="PollyResilienceBehavior{TRequest, TResponse}"/> class.
    /// </summary>
    /// <param name="pipelineProvider">The optional resilience pipeline provider used to resolve keyed pipelines.</param>
    /// <param name="defaultPipeline">The optional fallback resilience pipeline.</param>
    public PollyResilienceBehavior(
        ResiliencePipelineProvider<string>? pipelineProvider = null,
        ResiliencePipeline? defaultPipeline = null)
    {
        _pipelineProvider = pipelineProvider;
        _defaultPipeline = defaultPipeline;
    }

    /// <inheritdoc/>
    public async ValueTask<TResponse> Handle<TNext>(
        TRequest request,
        TNext next,
        CancellationToken cancellationToken)
        where TNext : struct, INext<TResponse>
    {
        var pipeline = ResolvePipeline();
        if (pipeline == null)
        {
            return await next.InvokeAsync().ConfigureAwait(false);
        }

        return await pipeline.ExecuteAsync(
            async (state, ct) =>
            {
                var result = await state.InvokeAsync().ConfigureAwait(false);
                ct.ThrowIfCancellationRequested();
                return result;
            },
            next,
            cancellationToken).ConfigureAwait(false);
    }

    private ResiliencePipeline? ResolvePipeline()
    {
        if (ConfiguredPipelineKey != null && _pipelineProvider != null)
        {
            if (_pipelineProvider.TryGetPipeline(ConfiguredPipelineKey, out var pipeline))
            {
                return pipeline;
            }
        }

        if (_pipelineProvider != null && _pipelineProvider.TryGetPipeline("Default", out var defaultRegistryPipeline))
        {
            return defaultRegistryPipeline;
        }

        return _defaultPipeline;
    }
}
