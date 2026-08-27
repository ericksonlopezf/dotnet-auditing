// Copyright © Erickson Lopez. MIT License.
using System;

namespace EricksonLopez.Mediator.Testing;

/// <summary>
/// Represents an exception thrown when an assertion in <see cref="FakeMediator"/> fails.
/// </summary>
public sealed class FakeAssertionException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FakeAssertionException"/> class
    /// with the specified error message.
    /// </summary>
    /// <param name="message">The message that describes the assertion failure.</param>
    public FakeAssertionException(string message) : base(message) { }
}
