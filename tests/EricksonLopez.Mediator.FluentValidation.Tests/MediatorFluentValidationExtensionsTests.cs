// Copyright © Erickson Lopez. MIT License.
using System;
using System.Linq;
using AwesomeAssertions;
using EricksonLopez.Mediator;
using EricksonLopez.Mediator.FluentValidation;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EricksonLopez.Mediator.FluentValidation.Tests;

public record TestFluentRequest(string Value) : ICommand<string>;

public class TestFluentValidator : AbstractValidator<TestFluentRequest>
{
    public TestFluentValidator()
    {
        RuleFor(x => x.Value).NotEmpty();
    }
}

public sealed class MediatorFluentValidationExtensionsTests
{
    [Fact]
    public void AddMediatorFluentValidationValidator_WhenGivenGenericTypes_RegistersValidatorAndBehavior()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddMediatorFluentValidationValidator<TestFluentValidator, TestFluentRequest>();
        using var sp = services.BuildServiceProvider();

        // Assert
        var validator = sp.GetService<IValidator<TestFluentRequest>>();
        validator.Should().NotBeNull();
        validator.Should().BeOfType<TestFluentValidator>();

        var behavior = sp.GetService<ValidationPipelineBehavior<TestFluentRequest, string>>();
        behavior.Should().NotBeNull();
    }

    [Fact]
    public void AddMediatorFluentValidationValidator_WithDifferentLifetimes_RegistersMatchingLifetime()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddMediatorFluentValidationValidator<TestFluentValidator, TestFluentRequest>(ServiceLifetime.Scoped);

        // Assert
        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IValidator<TestFluentRequest>));
        descriptor.Should().NotBeNull();
        descriptor!.Lifetime.Should().Be(ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddMediatorFluentValidationValidator_NullServices_ThrowsArgumentNullException()
    {
        // Arrange
        IServiceCollection services = null!;

        // Act
        var act = () => services.AddMediatorFluentValidationValidator<TestFluentValidator, TestFluentRequest>();

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("services");
    }
}
