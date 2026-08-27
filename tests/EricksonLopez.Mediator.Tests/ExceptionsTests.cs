// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using AwesomeAssertions;
using EricksonLopez.Mediator;
using Xunit;

namespace EricksonLopez.Mediator.Tests;

public class ExceptionsTests
{
    [Fact]
    public void Constructor_SingleErrorMessage_SetsErrorsAndMessage()
    {
        var ex = new MediatorValidationException("Invalid field");

        ex.Message.Should().Be("Invalid field");
        ex.Errors.Should().ContainSingle().Which.Should().Be("Invalid field");
    }

    [Fact]
    public void Constructor_MultipleErrors_SetsErrorsAndJoinsMessage()
    {
        var errors = new[] { "Field 1 required", "Field 2 invalid" };
        var ex = new MediatorValidationException(errors);

        ex.Message.Should().Be("Field 1 required; Field 2 invalid");
        ex.Errors.Should().BeEquivalentTo(errors);
    }

    [Fact]
    public void Constructor_NullErrors_FallsBackToEmptyList()
    {
        var ex = new MediatorValidationException((IReadOnlyList<string>)null!);

        ex.Message.Should().Be(string.Empty);
        ex.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_NotificationHandlerAggregateException_SetsHandlerExceptionsAndMessage()
    {
        var inner1 = new InvalidOperationException("First error");
        var inner2 = new ArgumentException("Second error");
        var list = new Exception[] { inner1, inner2 };

        var ex = new NotificationHandlerAggregateException(list);

        ex.HandlerExceptions.Should().BeSameAs(list);
        ex.Message.Should().Be("2 notification handler(s) threw an exception. See HandlerExceptions for details.");
    }
}
