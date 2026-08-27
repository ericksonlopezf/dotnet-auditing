// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;

namespace EricksonLopez.Mediator;

/// <summary>
/// Represents an exception thrown when request validation fails during pipeline execution.
/// </summary>
public sealed class MediatorValidationException : Exception
{
    /// <summary>
    /// Gets the collection of validation failure error messages.
    /// </summary>
    public IReadOnlyList<string> Errors { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="MediatorValidationException"/> class with a single validation error message.
    /// </summary>
    /// <param name="message">The message that describes the validation error.</param>
    public MediatorValidationException(string message)
        : base(message)
    {
        Errors = new[] { message };
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MediatorValidationException"/> class with a collection of validation error messages.
    /// </summary>
    /// <param name="errors">The collection of validation error messages.</param>
    public MediatorValidationException(IReadOnlyList<string> errors)
        : base(errors != null ? string.Join("; ", errors) : string.Empty)
    {
        Errors = errors ?? Array.Empty<string>();
    }
}
