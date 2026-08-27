// Copyright © Erickson Lopez. MIT License.
using System;
using AwesomeAssertions;
using EricksonLopez.Mediator.Result;
using EricksonLopez.Result;
using Xunit;

namespace EricksonLopez.Mediator.Result.Tests;

public sealed class ResultFactoryTests
{
    private sealed class CustomResultFactory<TValue> : IResultFactory<Result<TValue>>
    {
        public Result<TValue> CreateFailure(Error error) => Result<TValue>.Failure(error);
    }

    private sealed class NonGenericResultFactory : IResultFactory<EricksonLopez.Result.Result>
    {
        public EricksonLopez.Result.Result CreateFailure(Error error) => EricksonLopez.Result.Result.Failure(error);
    }

    private sealed class CustomEnvelope
    {
        public bool Succeeded { get; init; }
        public Error Error { get; init; } = default!;
    }

    private sealed class CustomEnvelopeFactory : IResultFactory<CustomEnvelope>
    {
        public CustomEnvelope CreateFailure(Error error) => new()
        {
            Succeeded = false,
            Error = error
        };
    }

    [Fact]
    public void CreateFailure_WhenCalledOnGenericResultFactory_ReturnsFailureResultWithSpecifiedError()
    {
        // Arrange
        var factory = new CustomResultFactory<string>();
        var error = Error.Validation("User.InvalidName", "Name cannot be null or empty.");

        // Act
        Result<string> result = factory.CreateFailure(error);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(error);
        result.Error.Code.Should().Be("User.InvalidName");
        result.Error.Description.Should().Be("Name cannot be null or empty.");
    }

    [Fact]
    public void CreateFailure_WhenCalledOnNonGenericResultFactory_ReturnsNonGenericFailureResult()
    {
        // Arrange
        var factory = new NonGenericResultFactory();
        var error = Error.NotFound("Order.NotFound", "Order with ID 42 was not found.");

        // Act
        EricksonLopez.Result.Result result = factory.CreateFailure(error);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(error);
        result.Error.Code.Should().Be("Order.NotFound");
    }

    [Fact]
    public void CreateFailure_WhenCalledOnCustomEnvelopeFactory_ReturnsCustomEnvelopeWithFailureState()
    {
        // Arrange
        var factory = new CustomEnvelopeFactory();
        var error = Error.Conflict("Resource.Locked", "The resource is currently locked.");

        // Act
        CustomEnvelope envelope = factory.CreateFailure(error);

        // Assert
        envelope.Should().NotBeNull();
        envelope.Succeeded.Should().BeFalse();
        envelope.Error.Should().Be(error);
        envelope.Error.Code.Should().Be("Resource.Locked");
    }

    [Fact]
    public void InterfaceVariance_WhenAssignedToCovariantResultFactory_CompilesAndDispatchesCorrectly()
    {
        // Arrange
        var stringFactory = new CustomResultFactory<string>();

        // Act & Assert — IResultFactory<out TResponse> is covariant
        IResultFactory<object> objectFactory = new CovariantBridgeFactory();
        var error = Error.Failure("System.Error", "Unexpected failure.");
        object result = objectFactory.CreateFailure(error);

        result.Should().NotBeNull();
    }

    private sealed class CovariantBridgeFactory : IResultFactory<string>
    {
        public string CreateFailure(Error error) => $"Failure: {error.Code}";
    }
}
