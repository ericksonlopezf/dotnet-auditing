// Copyright © Erickson Lopez. MIT License.
using System;
using System.Threading.Tasks;

namespace EricksonLopez.Mediator.Testing;

/// <summary>
/// Represents a test continuation delegate for evaluating <see cref="IPipelineBehavior{TRequest, TResponse}"/> implementations.
/// </summary>
/// <typeparam name="TResponse">The type of response returned by the continuation.</typeparam>
public readonly struct DelegateNext<TResponse> : INext<TResponse>
{
    private readonly Func<ValueTask<TResponse>> _continuation;

    /// <summary>
    /// Initializes a new instance of the <see cref="DelegateNext{TResponse}"/> struct with an asynchronous callback delegate.
    /// </summary>
    /// <param name="continuation">The asynchronous delegate to invoke.</param>
    /// <exception cref="ArgumentNullException"><paramref name="continuation"/> is <see langword="null"/>.</exception>
    public DelegateNext(Func<ValueTask<TResponse>> continuation)
    {
        _continuation = continuation ?? throw new ArgumentNullException(nameof(continuation));
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DelegateNext{TResponse}"/> struct that returns a constant synchronous value.
    /// </summary>
    /// <param name="constantResult">The constant result to return.</param>
    public DelegateNext(TResponse constantResult)
    {
        _continuation = () => new ValueTask<TResponse>(constantResult);
    }

    /// <inheritdoc/>
    public ValueTask<TResponse> InvokeAsync() => _continuation();
}
