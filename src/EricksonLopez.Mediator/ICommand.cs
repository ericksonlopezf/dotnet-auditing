// Copyright © Erickson Lopez. MIT License.
using System;

namespace EricksonLopez.Mediator;

/// <summary>
/// Defines a command that produces a response of type <typeparamref name="TResponse"/>.
/// </summary>
/// <remarks>
/// Commands represent operations that mutate system state and must be handled by a single <see cref="ICommandHandler{TCommand, TResponse}"/>.
/// </remarks>
/// <typeparam name="TResponse">The type of the result produced by executing the command.</typeparam>
public interface ICommand<TResponse>
{
}
