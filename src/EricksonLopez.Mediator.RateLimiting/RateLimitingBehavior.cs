// Copyright © Erickson Lopez. MIT License.
using System;
using System.Threading;
using System.Threading.RateLimiting;
using System.Threading.Tasks;

namespace EricksonLopez.Mediator.RateLimiting;

/// <summary>
/// Represents a pipeline behavior that enforces throughput constraints using a <see cref="RateLimiter"/>.
/// </summary>
/// <typeparam name="TRequest">The type of request being processed.</typeparam>
/// <typeparam name="TResponse">The type of response returned by the pipeline.</typeparam>
public sealed class RateLimitingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
{
    private readonly RateLimiter? _rateLimiter;

    /// <summary>
    /// Initializes a new instance of the <see cref="RateLimitingBehavior{TRequest, TResponse}"/> class.
    /// </summary>
    /// <param name="rateLimiter">The optional rate limiter instance used to acquire execution leases.</param>
    public RateLimitingBehavior(RateLimiter? rateLimiter = null)
    {
        _rateLimiter = rateLimiter;
    }

    /// <inheritdoc/>
    public async ValueTask<TResponse> Handle<TNext>(
        TRequest request,
        TNext next,
        CancellationToken cancellationToken) where TNext : struct, INext<TResponse>
    {
        if (_rateLimiter is null)
        {
            return await next.InvokeAsync().ConfigureAwait(false);
        }

        using var lease = await _rateLimiter.AcquireAsync(1, cancellationToken).ConfigureAwait(false);
        if (!lease.IsAcquired)
        {
            TimeSpan? retryAfter = null;
            if (lease.TryGetMetadata(MetadataName.RetryAfter, out var delay))
            {
                retryAfter = delay;
            }

            throw new RateLimitExceededException(
                $"Rate limit exceeded for request of type '{typeof(TRequest).Name}'.",
                retryAfter);
        }

        return await next.InvokeAsync().ConfigureAwait(false);
    }
}
