// Copyright © Erickson Lopez. MIT License.
using System;
using System.Threading.Tasks;
using AwesomeAssertions;
using EricksonLopez.Mediator.Testing;
using Xunit;

namespace EricksonLopez.Mediator.Testing.Tests;

public sealed class DelegateNextTests
{
    [Fact]
    public async Task DelegateNextGeneric_WithAsyncFunc_InvokesDelegateAndReturnsResult()
    {
        // Arrange
        var called = false;
        var next = new DelegateNext<string>(() =>
        {
            called = true;
            return ValueTask.FromResult("invoked");
        });

        // Act
        var result = await next.InvokeAsync();

        // Assert
        called.Should().BeTrue();
        result.Should().Be("invoked");
    }

    [Fact]
    public async Task DelegateNextGeneric_WithConstantValue_ReturnsValueWithoutAllocations()
    {
        // Arrange
        var next = new DelegateNext<int>(42);

        // Act
        var result = await next.InvokeAsync();

        // Assert
        result.Should().Be(42);
    }

    [Fact]
    public void DelegateNextGeneric_WithNullContinuation_ThrowsArgumentNullException()
    {
        // Arrange & Act
        var act = () => new DelegateNext<string>((Func<ValueTask<string>>)null!);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("continuation");
    }

    [Fact]
    public async Task DelegateNextNonGeneric_DefaultConstructor_CompletesSuccessfully()
    {
        // Arrange
        var next = new DelegateNext();

        // Act & Assert
        await next.InvokeAsync();
    }

    [Fact]
    public async Task DelegateNextNonGeneric_WithAsyncFunc_InvokesDelegate()
    {
        // Arrange
        var called = false;
        var next = new DelegateNext(() =>
        {
            called = true;
            return ValueTask.CompletedTask;
        });

        // Act
        await next.InvokeAsync();

        // Assert
        called.Should().BeTrue();
    }

    [Fact]
    public void DelegateNextNonGeneric_WithNullContinuation_ThrowsArgumentNullException()
    {
        // Arrange & Act
        var act = () => new DelegateNext((Func<ValueTask>)null!);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("continuation");
    }
}
