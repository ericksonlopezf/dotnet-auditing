// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;

namespace EricksonLopez.Mediator;

/// <summary>
/// Defines a handler for processing stream requests of type <typeparamref name="TRequest"/>.
/// </summary>
/// <typeparam name="TRequest">The type of stream request to process.</typeparam>
/// <typeparam name="TResponse">The type of elements yielded by the response stream.</typeparam>
public interface IStreamRequestHandler<in TRequest, out TResponse>
    where TRequest : IStreamRequest<TResponse>
{
    /// <summary>
    /// Processes the specified stream request and returns an asynchronous sequence of items.
    /// </summary>
    /// <param name="request">The stream request instance to process.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the streaming operation.</param>
    /// <returns>An asynchronous enumerable yielding elements of type <typeparamref name="TResponse"/>.</returns>
    IAsyncEnumerable<TResponse> Handle(TRequest request, CancellationToken cancellationToken);
}
