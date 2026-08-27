// Copyright © Erickson Lopez. MIT License.
using System;
using System.Threading;
using System.Threading.Tasks;

namespace EricksonLopez.Mediator;

/// <summary>
/// Defines a callback delegate representing the next step in a notification processing pipeline.
/// </summary>
/// <remarks>
/// This abstraction enables zero-allocation pipeline execution across notification behaviors and handlers.
/// </remarks>
public interface INext
{
    /// <summary>
    /// Invokes the next behavior or handler in the pipeline.
    /// </summary>
    /// <returns>A value task representing the asynchronous operation.</returns>
    ValueTask InvokeAsync();
}
