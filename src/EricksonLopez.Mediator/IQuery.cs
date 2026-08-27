// Copyright © Erickson Lopez. MIT License.
using System;

namespace EricksonLopez.Mediator;

/// <summary>
/// Defines a query that produces a response of type <typeparamref name="TResponse"/>.
/// </summary>
/// <remarks>
/// Queries represent read-only operations that do not mutate system state and must be handled by a single <see cref="IQueryHandler{TQuery, TResponse}"/>.
/// </remarks>
/// <typeparam name="TResponse">The type of the result produced by evaluating the query.</typeparam>
public interface IQuery<TResponse>
{
}
