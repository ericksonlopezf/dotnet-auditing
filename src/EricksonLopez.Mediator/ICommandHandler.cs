// Copyright © Erickson Lopez. MIT License.
using System;
using System.Threading;
using System.Threading.Tasks;

namespace EricksonLopez.Mediator;

/// <summary>
/// Defines a handler for processing a command of type <typeparamref name="TCommand"/>.
/// </summary>
/// <remarks>
/// Command handlers encapsulate business logic that mutates state and produce a response.
/// The mediator framework guarantees that each command type routes to a single registered handler.
/// </remarks>
/// <typeparam name="TCommand">The type of command to process.</typeparam>
/// <typeparam name="TResponse">The type of response returned by the handler.</typeparam>
public interface ICommandHandler<in TCommand, TResponse> where TCommand : ICommand<TResponse>
{
    /// <summary>
    /// Processes the specified command.
    /// </summary>
    /// <param name="command">The command instance to process.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
    /// <returns>
    /// A value task representing the asynchronous operation that yields the response produced by the command.
    /// </returns>
    ValueTask<TResponse> Handle(TCommand command, CancellationToken cancellationToken);
}
