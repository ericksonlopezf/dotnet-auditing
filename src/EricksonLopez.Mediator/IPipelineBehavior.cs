// Copyright © Erickson Lopez. MIT License.
using System;
using System.Threading;
using System.Threading.Tasks;

namespace EricksonLopez.Mediator;

/// <summary>
/// Defines a cross-cutting behavior that intercepts request processing within the mediator pipeline.
/// </summary>
/// <remarks>
/// Behaviors can be chained to form an execution pipeline surrounding the target handler.
/// </remarks>
/// <typeparam name="TRequest">The type of request being intercepted.</typeparam>
/// <typeparam name="TResponse">The type of response returned by the pipeline.</typeparam>
public interface IPipelineBehavior<in TRequest, TResponse>
{
    /// <summary>
    /// Intercepts the request execution pipeline and invokes the next step.
    /// </summary>
    /// <typeparam name="TNext">The type of the struct-based pipeline continuation delegate.</typeparam>
    /// <param name="request">The request instance being processed.</param>
    /// <param name="next">The pipeline continuation delegate to invoke downstream behaviors or the handler.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
    /// <returns>
    /// A value task representing the asynchronous operation that yields the response from the pipeline.
    /// </returns>
    ValueTask<TResponse> Handle<TNext>(TRequest request, TNext next, CancellationToken cancellationToken)
        where TNext : struct, INext<TResponse>;
}
