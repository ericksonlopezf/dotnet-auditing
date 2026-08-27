// Copyright © Erickson Lopez. MIT License.
using System;
using System.Threading;
using System.Threading.Tasks;

namespace EricksonLopez.Mediator;

/// <summary>
/// Defines a handler for processing queries of type <typeparamref name="TQuery"/>.
/// </summary>
/// <remarks>
/// Query handlers encapsulate read-only data retrieval logic and produce a response without mutating state.
/// The mediator framework guarantees that each query type routes to a single registered handler.
/// </remarks>
/// <typeparam name="TQuery">The type of query to process.</typeparam>
/// <typeparam name="TResponse">The type of response returned by the handler.</typeparam>
public interface IQueryHandler<in TQuery, TResponse> where TQuery : IQuery<TResponse>
{
    /// <summary>
    /// Processes the specified query.
    /// </summary>
    /// <param name="query">The query instance to process.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
    /// <returns>
    /// A value task representing the asynchronous operation that yields the response produced by the query.
    /// </returns>
    ValueTask<TResponse> Handle(TQuery query, CancellationToken cancellationToken);
}
