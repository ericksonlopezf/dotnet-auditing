// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;

namespace EricksonLopez.Mediator;

/// <summary>
/// Represents an aggregate exception containing exceptions thrown by notification handlers during a publish operation.
/// </summary>
public sealed class NotificationHandlerAggregateException : Exception
{
    /// <summary>
    /// Gets the collection of exceptions thrown by individual notification handlers.
    /// </summary>
    public IReadOnlyList<Exception> HandlerExceptions { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="NotificationHandlerAggregateException"/> class
    /// with the specified handler exceptions.
    /// </summary>
    /// <param name="exceptions">The collection of exceptions thrown during notification publishing.</param>
    public NotificationHandlerAggregateException(IReadOnlyList<Exception> exceptions)
        : base($"{exceptions.Count} notification handler(s) threw an exception. See HandlerExceptions for details.")
    {
        HandlerExceptions = exceptions;
    }
}
