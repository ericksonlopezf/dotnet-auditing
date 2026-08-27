// Copyright © Erickson Lopez. MIT License.
using System;
using EricksonLopez.Result;

namespace EricksonLopez.Mediator.Result;

/// <summary>
/// Defines a factory for creating strongly typed failure responses without runtime reflection.
/// </summary>
/// <remarks>
/// This abstraction allows pipeline behaviors to short-circuit execution by producing typed failure results.
/// </remarks>
/// <typeparam name="TResponse">The type of the failure response to create.</typeparam>
public interface IResultFactory<out TResponse>
{
    /// <summary>
    /// Creates a failure response containing the specified error metadata.
    /// </summary>
    /// <param name="error">The error metadata detailing why the operation failed.</param>
    /// <returns>A failure response of type <typeparamref name="TResponse"/>.</returns>
    TResponse CreateFailure(Error error);
}
