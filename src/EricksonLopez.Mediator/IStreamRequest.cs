// Copyright © Erickson Lopez. MIT License.
using System;
using System.Diagnostics.CodeAnalysis;

namespace EricksonLopez.Mediator;

/// <summary>
/// Defines a request that yields an asynchronous stream of items of type <typeparamref name="TResponse"/>.
/// </summary>
/// <remarks>
/// Stream requests must be handled by a single <see cref="IStreamRequestHandler{TRequest, TResponse}"/>.
/// </remarks>
/// <typeparam name="TResponse">The type of elements yielded by the stream.</typeparam>
public interface IStreamRequest<out TResponse>
{
}
