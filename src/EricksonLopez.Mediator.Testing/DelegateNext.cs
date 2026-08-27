// Copyright © Erickson Lopez. MIT License.
using System;
using System.Threading.Tasks;

namespace EricksonLopez.Mediator.Testing;

/// <summary>
/// Represents a test continuation delegate for evaluating <see cref="INotificationBehavior{TNotification}"/> implementations.
/// </summary>
public readonly struct DelegateNext : INext
{
    private readonly Func<ValueTask> _continuation;

    /// <summary>
    /// Initializes a new instance of the <see cref="DelegateNext"/> struct with an asynchronous callback delegate.
    /// </summary>
    /// <param name="continuation">The asynchronous delegate to invoke.</param>
    /// <exception cref="ArgumentNullException"><paramref name="continuation"/> is <see langword="null"/>.</exception>
    public DelegateNext(Func<ValueTask> continuation)
    {
        _continuation = continuation ?? throw new ArgumentNullException(nameof(continuation));
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DelegateNext"/> struct with a completed default continuation.
    /// </summary>
    public DelegateNext()
    {
        _continuation = () => default;
    }

    /// <inheritdoc/>
    public ValueTask InvokeAsync() => _continuation();
}
