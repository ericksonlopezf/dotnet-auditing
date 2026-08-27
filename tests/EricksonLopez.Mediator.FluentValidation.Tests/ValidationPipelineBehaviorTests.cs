// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using EricksonLopez.Mediator;
using EricksonLopez.Mediator.FluentValidation;
using EricksonLopez.Mediator.Result;
using EricksonLopez.Mediator.Testing;
using EricksonLopez.Result;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EricksonLopez.Mediator.FluentValidation.Tests;

public record CustomerRegisterCommand(string Name, int Age) : ICommand<string>;
public class CustomerRegisterCommandHandler : ICommandHandler<CustomerRegisterCommand, string>
{
    public ValueTask<string> Handle(CustomerRegisterCommand command, CancellationToken cancellationToken)
        => ValueTask.FromResult("Registered: " + command.Name);
}

public class CustomerRegisterValidator : AbstractValidator<CustomerRegisterCommand>
{
    public CustomerRegisterValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Name is required.");
        RuleFor(x => x.Age).GreaterThanOrEqualTo(18).WithMessage("Customer must be at least 18.");
    }
}

public record CustomerRegisterResultCommand(string Name, int Age) : ICommand<Result<string>>;
public class CustomerRegisterResultCommandHandler : ICommandHandler<CustomerRegisterResultCommand, Result<string>>
{
    public ValueTask<Result<string>> Handle(CustomerRegisterResultCommand command, CancellationToken cancellationToken)
        => ValueTask.FromResult(Result<string>.Success("Registered: " + command.Name));
}

public class CustomerResultFactory : IResultFactory<Result<string>>
{
    public Result<string> CreateFailure(Error error) => Result<string>.Failure(error);
}

public class ValidationPipelineBehaviorTests
{
    [Fact]
    public async Task Handle_ValidRequest_CallsNextSuccessfully()
    {
        var validator = new CustomerRegisterValidator();
        var behavior = new ValidationPipelineBehavior<CustomerRegisterCommand, string>(new[] { validator });
        var command = new CustomerRegisterCommand("Alice", 25);
        var next = new DelegateNext<string>(() => ValueTask.FromResult("Registered: Alice"));

        var result = await behavior.Handle(command, next, CancellationToken.None);

        result.Should().Be("Registered: Alice");
    }

    [Fact]
    public async Task Handle_InvalidRequestWithoutResultFactory_ThrowsValidationException()
    {
        var validator = new CustomerRegisterValidator();
        var behavior = new ValidationPipelineBehavior<CustomerRegisterCommand, string>(new[] { validator });
        var command = new CustomerRegisterCommand("", 15);
        var next = new DelegateNext<string>(() => ValueTask.FromResult("Should not reach"));

        var act = async () => await behavior.Handle(command, next, CancellationToken.None).AsTask();
        var ex = await act.Should().ThrowAsync<global::FluentValidation.ValidationException>();

        ex.Which.Errors.Should().Contain(e => e.ErrorMessage == "Name is required.");
        ex.Which.Errors.Should().Contain(e => e.ErrorMessage == "Customer must be at least 18.");
    }

    [Fact]
    public async Task Handle_InvalidRequestWithResultFactory_ReturnsFailureWithoutException()
    {
        var resultValidator = new InlineValidator<CustomerRegisterResultCommand>();
        resultValidator.RuleFor(x => x.Name).NotEmpty().WithMessage("Name is required.");
        resultValidator.RuleFor(x => x.Age).GreaterThanOrEqualTo(18).WithMessage("Customer must be at least 18.");

        var factory = new CustomerResultFactory();
        var behavior = new ValidationPipelineBehavior<CustomerRegisterResultCommand, Result<string>>(
            new[] { resultValidator }, factory);

        var command = new CustomerRegisterResultCommand("", 15);
        var next = new DelegateNext<Result<string>>(() => ValueTask.FromResult(Result<string>.Success("Success")));

        var result = await behavior.Handle(command, next, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Validation.Failed");
    }

    [Fact]
    public void AddMediatorFluentValidation_ValidServiceCollection_RegistersServicesCorrectly()
    {
        var services = new ServiceCollection();
        services.AddMediatorFluentValidation();

        var sp = services.BuildServiceProvider();
        var behavior = sp.GetService<ValidationPipelineBehavior<CustomerRegisterCommand, string>>();

        behavior.Should().NotBeNull();
    }

    [Fact]
    public void AddMediatorFluentValidation_NullServiceCollection_ThrowsArgumentNullException()
    {
        IServiceCollection services = null!;
        var act = () => services.AddMediatorFluentValidation();
        act.Should().Throw<ArgumentNullException>().WithParameterName("services");
    }

    [Fact]
    public void AddMediatorFluentValidatorsFromAssembly_ValidAssembly_RegistersValidatorsAndBehavior()
    {
        var services = new ServiceCollection();
        services.AddMediatorFluentValidatorsFromAssembly(typeof(CustomerRegisterValidator).Assembly, ServiceLifetime.Transient);
        using var provider = services.BuildServiceProvider();

        var validator = provider.GetService<IValidator<CustomerRegisterCommand>>();
        validator.Should().NotBeNull();
        validator.Should().BeOfType<CustomerRegisterValidator>();

        var behavior = provider.GetService<IPipelineBehavior<CustomerRegisterCommand, string>>();
        behavior.Should().NotBeNull();
        behavior.Should().BeOfType<ValidationPipelineBehavior<CustomerRegisterCommand, string>>();
    }

    [Fact]
    public void AddMediatorFluentValidatorsFromAssembly_NullArguments_ThrowsArgumentNullException()
    {
        IServiceCollection services = null!;
        var act1 = () => services.AddMediatorFluentValidatorsFromAssembly(typeof(CustomerRegisterValidator).Assembly);
        act1.Should().Throw<ArgumentNullException>().WithParameterName("services");

        var validServices = new ServiceCollection();
        var act2 = () => validServices.AddMediatorFluentValidatorsFromAssembly(null!);
        act2.Should().Throw<ArgumentNullException>().WithParameterName("assembly");
    }

    [Fact]
    public async Task Handle_NullOrEmptyValidators_CallsNextDirectly()
    {
        var behavior1 = new ValidationPipelineBehavior<CustomerRegisterCommand, string>(null);
        var command = new CustomerRegisterCommand("Alice", 25);
        var next1 = new DelegateNext<string>(() => ValueTask.FromResult("Registered: Alice"));

        var result1 = await behavior1.Handle(command, next1, CancellationToken.None);
        result1.Should().Be("Registered: Alice");

        var behavior2 = new ValidationPipelineBehavior<CustomerRegisterCommand, string>(Array.Empty<IValidator<CustomerRegisterCommand>>());
        var next2 = new DelegateNext<string>(() => ValueTask.FromResult("Registered: Alice"));

        var result2 = await behavior2.Handle(command, next2, CancellationToken.None);
        result2.Should().Be("Registered: Alice");
    }

    [Fact]
    public async Task Handle_ValidatorWithNullErrors_FiltersNullsAndThrows()
    {
        var customValidator = new ValidationStubWithNullErrors<CustomerRegisterCommand>(
            null,
            new global::FluentValidation.Results.ValidationFailure("Name", "Name is invalid"),
            null);

        var behavior = new ValidationPipelineBehavior<CustomerRegisterCommand, string>(new[] { customValidator });
        var command = new CustomerRegisterCommand("Alice", 25);
        var next = new DelegateNext<string>(() => ValueTask.FromResult("OK"));

        var act = async () => await behavior.Handle(command, next, CancellationToken.None);
        var ex = await act.Should().ThrowAsync<global::FluentValidation.ValidationException>();
        ex.Which.Errors.Should().HaveCount(1);
        ex.Which.Errors.First().ErrorMessage.Should().Be("Name is invalid");
    }

    [Fact]
    public async Task Handle_ValidatorWithNullErrors_WithResultFactory_FiltersNullsAndReturnsFailure()
    {
        var customValidator = new ValidationStubWithNullErrors<CustomerRegisterResultCommand>(
            null,
            new global::FluentValidation.Results.ValidationFailure("Name", "Name is invalid"),
            null);

        var factory = new CustomerResultFactory();
        var behavior = new ValidationPipelineBehavior<CustomerRegisterResultCommand, Result<string>>(
            new[] { customValidator }, factory);
        var command = new CustomerRegisterResultCommand("Alice", 25);
        var next = new DelegateNext<Result<string>>(() => ValueTask.FromResult(Result<string>.Success("OK")));

        var result = await behavior.Handle(command, next, CancellationToken.None);
        result.IsFailure.Should().BeTrue();
        result.Error.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_PropagatesCancellationTokenToValidators()
    {
        var validator = new ValidationCancellationTracking<CustomerRegisterCommand>();
        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        var behavior = new ValidationPipelineBehavior<CustomerRegisterCommand, string>(new[] { validator });
        var command = new CustomerRegisterCommand("Alice", 25);
        var next = new DelegateNext<string>(() => ValueTask.FromResult("OK"));

        var result = await behavior.Handle(command, next, token);
        result.Should().Be("OK");
        validator.ReceivedToken.Should().Be(token);
    }

    [Fact]
    public async Task Handle_MultipleValidators_AggregatesAllFailuresIntoSingleValidationException()
    {
        var validator1 = new MultiNameValidator();
        var validator2 = new MultiAgeValidator();

        var behavior = new ValidationPipelineBehavior<MultiValidatorCommand, string>(new IValidator<MultiValidatorCommand>[] { validator1, validator2 });
        var command = new MultiValidatorCommand("", 12);
        var next = new DelegateNext<string>(() => ValueTask.FromResult("Should not reach"));

        var act = async () => await behavior.Handle(command, next, CancellationToken.None).AsTask();
        var ex = await act.Should().ThrowAsync<global::FluentValidation.ValidationException>();

        ex.Which.Errors.Should().Contain(e => e.ErrorMessage == "MultiName is required.");
        ex.Which.Errors.Should().Contain(e => e.ErrorMessage == "MultiAge must be at least 18.");
    }
}

public record MultiValidatorCommand(string Name, int Age) : ICommand<string>;

public class MultiValidatorCommandHandler : ICommandHandler<MultiValidatorCommand, string>
{
    public ValueTask<string> Handle(MultiValidatorCommand command, CancellationToken cancellationToken)
        => ValueTask.FromResult("OK: " + command.Name);
}

public class MultiNameValidator : AbstractValidator<MultiValidatorCommand>
{
    public MultiNameValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("MultiName is required.");
    }
}

public class MultiAgeValidator : AbstractValidator<MultiValidatorCommand>
{
    public MultiAgeValidator()
    {
        RuleFor(x => x.Age).GreaterThanOrEqualTo(18).WithMessage("MultiAge must be at least 18.");
    }
}

public sealed class ValidationStubWithNullErrors<T> : AbstractValidator<T>
{
    private readonly global::FluentValidation.Results.ValidationFailure?[] _failures;

    public ValidationStubWithNullErrors(params global::FluentValidation.Results.ValidationFailure?[] failures)
    {
        _failures = failures;
    }

    public override Task<global::FluentValidation.Results.ValidationResult> ValidateAsync(
        ValidationContext<T> context,
        CancellationToken cancellation = default)
    {
        return Task.FromResult(new global::FluentValidation.Results.ValidationResult(_failures!));
    }
}

public sealed class ValidationCancellationTracking<T> : AbstractValidator<T>
{
    public CancellationToken ReceivedToken { get; private set; }

    public override Task<global::FluentValidation.Results.ValidationResult> ValidateAsync(
        ValidationContext<T> context,
        CancellationToken cancellation = default)
    {
        ReceivedToken = cancellation;
        return Task.FromResult(new global::FluentValidation.Results.ValidationResult());
    }
}
