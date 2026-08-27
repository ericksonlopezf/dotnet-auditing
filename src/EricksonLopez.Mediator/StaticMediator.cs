// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace EricksonLopez.Mediator;

/// <summary>
/// Provides static mediator dispatching capabilities without requiring a dependency injection container.
/// </summary>
/// <remarks>
/// This type is thread-safe, reflection-free, Native AOT compatible, and intended for high-performance, embedded, or constrained runtime environments.
/// </remarks>
public static class StaticMediator
{
    private static readonly ConcurrentDictionary<Type, object> CommandHandlers = new();
    private static readonly ConcurrentDictionary<Type, object> QueryHandlers = new();
    private static readonly ConcurrentDictionary<Type, ConcurrentBag<object>> NotificationHandlers = new();

    private interface ICommandInvoker<TResponse>
    {
        ValueTask<TResponse> Invoke(object command, CancellationToken cancellationToken);
    }

    private sealed class CommandInvoker<TCommand, TResponse> : ICommandInvoker<TResponse>
        where TCommand : ICommand<TResponse>
    {
        private readonly ICommandHandler<TCommand, TResponse> _handler;
        public CommandInvoker(ICommandHandler<TCommand, TResponse> handler) => _handler = handler;
        public ValueTask<TResponse> Invoke(object command, CancellationToken cancellationToken) =>
            _handler.Handle((TCommand)command, cancellationToken);
    }

    private interface IQueryInvoker<TResponse>
    {
        ValueTask<TResponse> Invoke(object query, CancellationToken cancellationToken);
    }

    private sealed class QueryInvoker<TQuery, TResponse> : IQueryInvoker<TResponse>
        where TQuery : IQuery<TResponse>
    {
        private readonly IQueryHandler<TQuery, TResponse> _handler;
        public QueryInvoker(IQueryHandler<TQuery, TResponse> handler) => _handler = handler;
        public ValueTask<TResponse> Invoke(object query, CancellationToken cancellationToken) =>
            _handler.Handle((TQuery)query, cancellationToken);
    }

    /// <summary>
    /// Registers a command handler instance for static dispatching.
    /// </summary>
    /// <typeparam name="TCommand">The type of command handled.</typeparam>
    /// <typeparam name="TResponse">The type of response produced by the command.</typeparam>
    /// <param name="handler">The command handler instance to register.</param>
    /// <exception cref="ArgumentNullException"><paramref name="handler"/> is <see langword="null"/>.</exception>
    public static void RegisterCommandHandler<TCommand, TResponse>(ICommandHandler<TCommand, TResponse> handler)
        where TCommand : ICommand<TResponse>
    {
        ArgumentNullException.ThrowIfNull(handler);
        CommandHandlers[typeof(TCommand)] = new CommandInvoker<TCommand, TResponse>(handler);
    }

    /// <summary>
    /// Registers a query handler instance for static dispatching.
    /// </summary>
    /// <typeparam name="TQuery">The type of query handled.</typeparam>
    /// <typeparam name="TResponse">The type of response produced by the query.</typeparam>
    /// <param name="handler">The query handler instance to register.</param>
    /// <exception cref="ArgumentNullException"><paramref name="handler"/> is <see langword="null"/>.</exception>
    public static void RegisterQueryHandler<TQuery, TResponse>(IQueryHandler<TQuery, TResponse> handler)
        where TQuery : IQuery<TResponse>
    {
        ArgumentNullException.ThrowIfNull(handler);
        QueryHandlers[typeof(TQuery)] = new QueryInvoker<TQuery, TResponse>(handler);
    }

    /// <summary>
    /// Registers a notification handler instance for static dispatching.
    /// </summary>
    /// <typeparam name="TNotification">The type of notification handled.</typeparam>
    /// <param name="handler">The notification handler instance to register.</param>
    /// <exception cref="ArgumentNullException"><paramref name="handler"/> is <see langword="null"/>.</exception>
    public static void RegisterNotificationHandler<TNotification>(INotificationHandler<TNotification> handler)
        where TNotification : INotification
    {
        ArgumentNullException.ThrowIfNull(handler);
        var bag = NotificationHandlers.GetOrAdd(typeof(TNotification), _ => new ConcurrentBag<object>());
        bag.Add(handler);
    }

    /// <summary>
    /// Dispatches a strongly typed command directly to its registered static handler without reflection.
    /// </summary>
    /// <typeparam name="TCommand">The concrete type of the command.</typeparam>
    /// <typeparam name="TResponse">The type of response returned by the command handler.</typeparam>
    /// <param name="command">The command instance to dispatch.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
    /// <returns>
    /// A value task representing the asynchronous operation that yields the response from the handler.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="command"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">No static command handler is registered for <typeparamref name="TCommand"/>.</exception>
    public static ValueTask<TResponse> SendCommand<TCommand, TResponse>(TCommand command, CancellationToken cancellationToken = default)
        where TCommand : ICommand<TResponse>
    {
        ArgumentNullException.ThrowIfNull(command);

        if (CommandHandlers.TryGetValue(typeof(TCommand), out var handlerObj) &&
            handlerObj is ICommandInvoker<TResponse> invoker)
        {
            return invoker.Invoke(command, cancellationToken);
        }

        throw new InvalidOperationException($"No static command handler registered for {typeof(TCommand).FullName}");
    }

    /// <summary>
    /// Dispatches a strongly typed query directly to its registered static handler without reflection.
    /// </summary>
    /// <typeparam name="TQuery">The concrete type of the query.</typeparam>
    /// <typeparam name="TResponse">The type of response returned by the query handler.</typeparam>
    /// <param name="query">The query instance to dispatch.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
    /// <returns>
    /// A value task representing the asynchronous operation that yields the response from the handler.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="query"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">No static query handler is registered for <typeparamref name="TQuery"/>.</exception>
    public static ValueTask<TResponse> SendQuery<TQuery, TResponse>(TQuery query, CancellationToken cancellationToken = default)
        where TQuery : IQuery<TResponse>
    {
        ArgumentNullException.ThrowIfNull(query);

        if (QueryHandlers.TryGetValue(typeof(TQuery), out var handlerObj) &&
            handlerObj is IQueryInvoker<TResponse> invoker)
        {
            return invoker.Invoke(query, cancellationToken);
        }

        throw new InvalidOperationException($"No static query handler registered for {typeof(TQuery).FullName}");
    }

    /// <summary>
    /// Publishes a notification to all registered static handlers.
    /// </summary>
    /// <typeparam name="TNotification">The type of notification being published.</typeparam>
    /// <param name="notification">The notification instance to publish.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
    /// <returns>A value task representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="notification"/> is <see langword="null"/>.</exception>
    public static async ValueTask Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : INotification
    {
        ArgumentNullException.ThrowIfNull(notification);

        if (NotificationHandlers.TryGetValue(typeof(TNotification), out var bag))
        {
            foreach (var item in bag)
            {
                if (item is INotificationHandler<TNotification> handler)
                {
                    await handler.Handle(notification, cancellationToken).ConfigureAwait(false);
                }
            }
        }
    }

    /// <summary>
    /// Removes all statically registered command, query, and notification handlers.
    /// </summary>
    public static void Reset()
    {
        CommandHandlers.Clear();
        QueryHandlers.Clear();
        NotificationHandlers.Clear();
    }
}
