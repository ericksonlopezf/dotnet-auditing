// Copyright © Erickson Lopez. MIT License.
using System;
using System.Threading;
using System.Threading.Tasks;

namespace EricksonLopez.Mediator;

/// <summary>
/// Defines a callback delegate representing the next step in a request processing pipeline.
/// </summary>
/// <remarks>
/// This abstraction enables zero-allocation pipeline execution across behaviors and handlers.
/// </remarks>
/// <typeparam name="TResponse">The type of response returned by the downstream pipeline.</typeparam>
public interface INext<TResponse>
{
    /// <summary>
    /// Invokes the next behavior or handler in the pipeline.
    /// </summary>
    /// <returns>
    /// A value task representing the asynchronous operation that yields the response from subsequent pipeline components.
    /// </returns>
    ValueTask<TResponse> InvokeAsync();
}
