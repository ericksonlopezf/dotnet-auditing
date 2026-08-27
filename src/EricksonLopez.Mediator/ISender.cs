// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

namespace EricksonLopez.Mediator;

/// <summary>
/// Defines a mechanism for dispatching commands, queries, and stream requests to their respective handlers.
/// </summary>
#pragma warning disable RS0026 // Do not add multiple public overloads with optional parameters
public interface ISender
{
    /// <summary>
    /// Dispatches a command to its registered handler.
    /// </summary>
    /// <typeparam name="TResponse">The type of response produced by the command handler.</typeparam>
    /// <param name="command">The command instance to dispatch.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
    /// <returns>
    /// A value task representing the asynchronous operation that yields the response from the handler.
    /// </returns>
    ValueTask<TResponse> Send<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Dispatches a strongly typed command directly via generic static dispatch without boxing or runtime reflection.
    /// </summary>
    /// <typeparam name="TCommand">The concrete type of the command.</typeparam>
    /// <typeparam name="TResponse">The type of response produced by the command handler.</typeparam>
    /// <param name="command">The command instance to dispatch.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
    /// <returns>
    /// A value task representing the asynchronous operation that yields the response from the handler.
    /// </returns>
    ValueTask<TResponse> SendCommand<TCommand, TResponse>(TCommand command, CancellationToken cancellationToken = default)
        where TCommand : ICommand<TResponse>;

    /// <summary>
    /// Dispatches a query to its registered handler.
    /// </summary>
    /// <typeparam name="TResponse">The type of response produced by the query handler.</typeparam>
    /// <param name="query">The query instance to dispatch.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
    /// <returns>
    /// A value task representing the asynchronous operation that yields the response from the handler.
    /// </returns>
    ValueTask<TResponse> Send<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Dispatches a strongly typed query directly via generic static dispatch without boxing or runtime reflection.
    /// </summary>
    /// <typeparam name="TQuery">The concrete type of the query.</typeparam>
    /// <typeparam name="TResponse">The type of response produced by the query handler.</typeparam>
    /// <param name="query">The query instance to dispatch.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
    /// <returns>
    /// A value task representing the asynchronous operation that yields the response from the handler.
    /// </returns>
    ValueTask<TResponse> SendQuery<TQuery, TResponse>(TQuery query, CancellationToken cancellationToken = default)
        where TQuery : IQuery<TResponse>;

    /// <summary>
    /// Dispatches a stream request to its registered handler and returns an asynchronous sequence of items.
    /// </summary>
    /// <typeparam name="TResponse">The type of elements yielded by the response stream.</typeparam>
    /// <param name="request">The stream request instance to dispatch.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the streaming operation.</param>
    /// <returns>An asynchronous stream yielding items of type <typeparamref name="TResponse"/>.</returns>
    IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default);
}
