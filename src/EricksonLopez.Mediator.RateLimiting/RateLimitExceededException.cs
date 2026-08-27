// Copyright © Erickson Lopez. MIT License.
using System;

namespace EricksonLopez.Mediator.RateLimiting;

/// <summary>
/// Represents an exception thrown when a request is rejected due to rate limits being exceeded.
/// </summary>
public sealed class RateLimitExceededException : Exception
{
    /// <summary>
    /// Gets the suggested duration to wait before retrying the operation, if provided by the rate limiter.
    /// </summary>
    public TimeSpan? RetryAfter { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="RateLimitExceededException"/> class with the specified error message and optional retry duration.
    /// </summary>
    /// <param name="message">The message that describes the rate limit failure.</param>
    /// <param name="retryAfter">The optional wait duration before retrying the operation.</param>
    public RateLimitExceededException(string message, TimeSpan? retryAfter = null)
        : base(message)
    {
        RetryAfter = retryAfter;
    }
}
