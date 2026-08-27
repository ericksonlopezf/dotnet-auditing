// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.Mediator;

namespace EricksonLopez.Mediator.Testing;

/// <summary>
/// Provides an in-memory test double for <see cref="IMediator"/> to simplify unit testing.
/// </summary>
/// <remarks>
/// Handlers and expectations are configured programmatically without requiring code generation or dependency injection containers.
/// </remarks>
public sealed class FakeMediator : IMediator
{
    private readonly Dictionary<Type, Func<object, CancellationToken, ValueTask<object>>> _commandHandlers = new();
    private readonly Dictionary<Type, Func<object, CancellationToken, ValueTask<object>>> _queryHandlers = new();
    private readonly Dictionary<Type, List<Func<object, CancellationToken, ValueTask>>> _notificationHandlers = new();
    private readonly Dictionary<Type, Func<object, CancellationToken, object>> _streamHandlers = new();

    private readonly ConcurrentBag<object> _receivedRequests = new();
    private readonly ConcurrentBag<object> _receivedNotifications = new();

    // ─── Setup ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Configures a synchronous response delegate for the specified command type.
    /// </summary>
    /// <typeparam name="TCommand">The type of command to handle.</typeparam>
    /// <typeparam name="TResponse">The type of response returned by the command handler.</typeparam>
    /// <param name="handler">The delegate to execute when the command is sent.</param>
    /// <returns>The current <see cref="FakeMediator"/> instance for fluent chaining.</returns>
    public FakeMediator SetupCommand<TCommand, TResponse>(Func<TCommand, TResponse> handler)
        where TCommand : ICommand<TResponse>
    {
        _commandHandlers[typeof(TCommand)] = (req, _) => new ValueTask<object>(handler((TCommand)req)!);
        return this;
    }

    /// <summary>
    /// Configures an asynchronous response delegate for the specified command type.
    /// </summary>
    /// <typeparam name="TCommand">The type of command to handle.</typeparam>
    /// <typeparam name="TResponse">The type of response returned by the command handler.</typeparam>
    /// <param name="handler">The asynchronous delegate to execute when the command is sent.</param>
    /// <returns>The current <see cref="FakeMediator"/> instance for fluent chaining.</returns>
    public FakeMediator SetupCommand<TCommand, TResponse>(Func<TCommand, CancellationToken, ValueTask<TResponse>> handler)
        where TCommand : ICommand<TResponse>
    {
        _commandHandlers[typeof(TCommand)] = async (req, ct) => (object)(await handler((TCommand)req, ct))!;
        return this;
    }

    /// <summary>
    /// Configures a synchronous response delegate for the specified query type.
    /// </summary>
    /// <typeparam name="TQuery">The type of query to handle.</typeparam>
    /// <typeparam name="TResponse">The type of response returned by the query handler.</typeparam>
    /// <param name="handler">The delegate to execute when the query is sent.</param>
    /// <returns>The current <see cref="FakeMediator"/> instance for fluent chaining.</returns>
    public FakeMediator SetupQuery<TQuery, TResponse>(Func<TQuery, TResponse> handler)
        where TQuery : IQuery<TResponse>
    {
        _queryHandlers[typeof(TQuery)] = (req, _) => new ValueTask<object>(handler((TQuery)req)!);
        return this;
    }

    /// <summary>
    /// Configures an asynchronous response delegate for the specified query type.
    /// </summary>
    /// <typeparam name="TQuery">The type of query to handle.</typeparam>
    /// <typeparam name="TResponse">The type of response returned by the query handler.</typeparam>
    /// <param name="handler">The asynchronous delegate to execute when the query is sent.</param>
    /// <returns>The current <see cref="FakeMediator"/> instance for fluent chaining.</returns>
    public FakeMediator SetupQuery<TQuery, TResponse>(Func<TQuery, CancellationToken, ValueTask<TResponse>> handler)
        where TQuery : IQuery<TResponse>
    {
        _queryHandlers[typeof(TQuery)] = async (req, ct) => (object)(await handler((TQuery)req, ct))!;
        return this;
    }

    /// <summary>
    /// Configures an asynchronous callback delegate for the specified notification type.
    /// </summary>
    /// <typeparam name="TNotification">The type of notification to handle.</typeparam>
    /// <param name="handler">The asynchronous delegate to execute when the notification is published.</param>
    /// <returns>The current <see cref="FakeMediator"/> instance for fluent chaining.</returns>
    public FakeMediator SetupNotification<TNotification>(Func<TNotification, CancellationToken, ValueTask> handler)
        where TNotification : INotification
    {
        if (!_notificationHandlers.TryGetValue(typeof(TNotification), out var list))
        {
            list = new List<Func<object, CancellationToken, ValueTask>>();
            _notificationHandlers[typeof(TNotification)] = list;
        }
        list.Add((n, ct) => handler((TNotification)n, ct));
        return this;
    }

    /// <summary>
    /// Configures an asynchronous streaming delegate for the specified stream request type.
    /// </summary>
    /// <typeparam name="TRequest">The type of stream request to handle.</typeparam>
    /// <typeparam name="TResponse">The type of elements yielded by the stream.</typeparam>
    /// <param name="handler">The streaming delegate to execute when the stream is created.</param>
    /// <returns>The current <see cref="FakeMediator"/> instance for fluent chaining.</returns>
    public FakeMediator SetupStream<TRequest, TResponse>(Func<TRequest, CancellationToken, IAsyncEnumerable<TResponse>> handler)
        where TRequest : IStreamRequest<TResponse>
    {
        _streamHandlers[typeof(TRequest)] = (req, ct) => handler((TRequest)req, ct)!;
        return this;
    }

    // ─── Dispatch ────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public ValueTask<TResponse> Send<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken = default)
    {
        _receivedRequests.Add(command);
        var type = command.GetType();
        if (!_commandHandlers.TryGetValue(type, out var handler))
            throw new InvalidOperationException($"FakeMediator: no handler for command '{type.Name}'. Call SetupCommand<{type.Name}, TResponse>(...) in test setup.");
        return ExecuteTyped<TResponse>(handler, command, cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask<TResponse> SendCommand<TCommand, TResponse>(TCommand command, CancellationToken cancellationToken = default)
        where TCommand : ICommand<TResponse>
    {
        return Send((ICommand<TResponse>)command, cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask<TResponse> Send<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken = default)
    {
        _receivedRequests.Add(query);
        var type = query.GetType();
        if (!_queryHandlers.TryGetValue(type, out var handler))
            throw new InvalidOperationException($"FakeMediator: no handler for query '{type.Name}'. Call SetupQuery<{type.Name}, TResponse>(...) in test setup.");
        return ExecuteTyped<TResponse>(handler, query, cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask<TResponse> SendQuery<TQuery, TResponse>(TQuery query, CancellationToken cancellationToken = default)
        where TQuery : IQuery<TResponse>
    {
        return Send((IQuery<TResponse>)query, cancellationToken);
    }

    /// <inheritdoc/>
    public async ValueTask Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : INotification
    {
        _receivedNotifications.Add(notification!);
        if (!_notificationHandlers.TryGetValue(typeof(TNotification), out var handlers))
            return;
        foreach (var handler in handlers)
            await handler(notification!, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        _receivedRequests.Add(request!);
        var type = request.GetType();
        if (!_streamHandlers.TryGetValue(type, out var handler))
            throw new InvalidOperationException($"FakeMediator: no handler for stream request '{type.Name}'. Call SetupStream<{type.Name}, TResponse>(...) in test setup.");

        return (IAsyncEnumerable<TResponse>)handler(request, cancellationToken);
    }

    private static async ValueTask<TResponse> ExecuteTyped<TResponse>(
        Func<object, CancellationToken, ValueTask<object>> handler, object request, CancellationToken ct)
    {
        var result = await handler(request, ct).ConfigureAwait(false);
        return (TResponse)result;
    }

    // ─── Assertions ───────────────────────────────────────────────────────────

    /// <summary>
    /// Gets the collection of all command and query requests dispatched to this fake mediator.
    /// </summary>
    public IReadOnlyList<object> ReceivedRequests => _receivedRequests.ToList();

    /// <summary>
    /// Gets the collection of all notifications published to this fake mediator.
    /// </summary>
    public IReadOnlyList<object> ReceivedNotifications => _receivedNotifications.ToList();

    /// <summary>
    /// Retrieves all received requests matching the specified type <typeparamref name="TRequest"/>.
    /// </summary>
    /// <typeparam name="TRequest">The type of request to filter.</typeparam>
    /// <returns>A read-only collection of matching requests.</returns>
    public IReadOnlyList<TRequest> ReceivedRequestsOf<TRequest>() where TRequest : class
        => _receivedRequests.OfType<TRequest>().ToList().AsReadOnly();

    /// <summary>
    /// Retrieves all received notifications matching the specified type <typeparamref name="TNotification"/>.
    /// </summary>
    /// <typeparam name="TNotification">The type of notification to filter.</typeparam>
    /// <returns>A read-only collection of matching notifications.</returns>
    public IReadOnlyList<TNotification> ReceivedNotificationsOf<TNotification>() where TNotification : class
        => _receivedNotifications.OfType<TNotification>().ToList().AsReadOnly();

    /// <summary>
    /// Verifies that at least one message of type <typeparamref name="TRequest"/> was received.
    /// </summary>
    /// <typeparam name="TRequest">The type of request or notification expected.</typeparam>
    /// <exception cref="FakeAssertionException">No matching message was received.</exception>
    public void ShouldHaveReceived<TRequest>()
    {
        var found = _receivedRequests.Any(r => r is TRequest) || _receivedNotifications.Any(r => r is TRequest);
        if (!found)
            throw new FakeAssertionException($"Expected to have received '{typeof(TRequest).Name}', but none was received.");
    }

    /// <summary>
    /// Verifies that at least one message of type <typeparamref name="TRequest"/> matching the specified predicate was received.
    /// </summary>
    /// <typeparam name="TRequest">The type of request or notification expected.</typeparam>
    /// <param name="predicate">The condition to evaluate on received messages.</param>
    /// <exception cref="FakeAssertionException">No message matching the predicate was received.</exception>
    public void ShouldHaveReceived<TRequest>(Func<TRequest, bool> predicate)
    {
        var all = _receivedRequests.OfType<TRequest>().Concat(_receivedNotifications.OfType<TRequest>()).ToList();
        if (all.Count == 0)
            throw new FakeAssertionException($"Expected to have received '{typeof(TRequest).Name}', but none was received.");
        if (!all.Any(predicate))
            throw new FakeAssertionException($"Expected to have received '{typeof(TRequest).Name}' matching the predicate, but no match found.");
    }

    /// <summary>
    /// Verifies that no messages of type <typeparamref name="TRequest"/> were received.
    /// </summary>
    /// <typeparam name="TRequest">The type of request or notification that should not have been received.</typeparam>
    /// <exception cref="FakeAssertionException">At least one message of type <typeparamref name="TRequest"/> was received.</exception>
    public void ShouldNotHaveReceived<TRequest>()
    {
        var found = _receivedRequests.Any(r => r is TRequest) || _receivedNotifications.Any(r => r is TRequest);
        if (found)
            throw new FakeAssertionException($"Expected NOT to have received '{typeof(TRequest).Name}', but one was received.");
    }

    /// <summary>
    /// Calculates the number of times a message of type <typeparamref name="TRequest"/> was received.
    /// </summary>
    /// <typeparam name="TRequest">The type of request or notification to count.</typeparam>
    /// <returns>The total number of matching messages received.</returns>
    public int ReceivedCount<TRequest>()
        => _receivedRequests.Count(r => r is TRequest) + _receivedNotifications.Count(r => r is TRequest);

    /// <summary>
    /// Clears all registered handlers and received request history.
    /// </summary>
    public void Reset()
    {
        _commandHandlers.Clear();
        _queryHandlers.Clear();
        _notificationHandlers.Clear();
        _streamHandlers.Clear();
        _receivedRequests.Clear();
        _receivedNotifications.Clear();
    }
}
