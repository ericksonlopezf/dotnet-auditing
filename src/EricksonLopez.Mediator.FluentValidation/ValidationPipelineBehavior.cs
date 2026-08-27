// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.Mediator;
using EricksonLopez.Mediator.Result;
using EricksonLopez.Result;
using EricksonLopez.Result.FluentValidation;
using global::FluentValidation;

namespace EricksonLopez.Mediator.FluentValidation;

/// <summary>
/// A pipeline behavior that executes registered <see cref="IValidator{T}"/> instances prior to handler execution.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Behavior:</strong>
/// All registered validators for <typeparamref name="TRequest"/> are executed concurrently via <see cref="Task.WhenAll"/>.
/// If any validator produces failures, the behavior short-circuits in one of two ways:
/// <list type="bullet">
///   <item>
///     <description>
///       If an <see cref="IResultFactory{TResponse}"/> is registered for <typeparamref name="TResponse"/>,
///       returns a failure result constructed by the factory without throwing.
///     </description>
///   </item>
///   <item>
///     <description>
///       Otherwise, throws a <see cref="ValidationException"/> containing all validation failures.
///     </description>
///   </item>
/// </list>
/// </para>
/// <para>
/// <strong>Registration:</strong> Use <c>AddMediatorFluentValidation()</c> to register this behavior
/// and explicitly register validators via <c>AddMediatorFluentValidationValidator&lt;TValidator&gt;()</c>
/// or assembly scanning via <c>AddMediatorFluentValidatorsFromAssembly()</c>.
/// </para>
/// <para>
/// <strong>AOT / Trimming:</strong>
/// <c>ValidationPipelineBehavior&lt;TRequest, TResponse&gt;</c> itself is AOT-safe when validators are
/// registered explicitly. Assembly scanning (<c>AddMediatorFluentValidatorsFromAssembly</c>) uses reflection
/// and is annotated with <see cref="System.Diagnostics.CodeAnalysis.RequiresUnreferencedCodeAttribute"/>.
/// </para>
/// <para>
/// <strong>CancellationToken:</strong> The <c>cancellationToken</c> is forwarded to each
/// <see cref="IValidator{T}.ValidateAsync"/> call. Cancellation before all validators complete causes
/// a <see cref="TaskCanceledException"/>.
/// </para>
/// <para>
/// <strong>Replaces:</strong> <c>EricksonLopez.Mediator.Validation.ValidationBehavior&lt;TRequest, TResponse&gt;</c>
/// which was deprecated in ADR-033.
/// </para>
/// </remarks>
/// <typeparam name="TRequest">The type of request being validated. Must implement <see cref="ICommand{TResponse}"/> or <see cref="IQuery{TResponse}"/>.</typeparam>
/// <typeparam name="TResponse">The type of response returned by the pipeline.</typeparam>
public sealed class ValidationPipelineBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;
    private readonly IResultFactory<TResponse>? _resultFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationPipelineBehavior{TRequest, TResponse}"/> class.
    /// </summary>
    /// <param name="validators">
    /// The collection of <see cref="IValidator{T}"/> instances applied to <typeparamref name="TRequest"/>.
    /// When empty or <see langword="null"/>, no validation is performed and the next delegate is invoked directly.
    /// </param>
    /// <param name="resultFactory">
    /// An optional factory that constructs a failure response of type <typeparamref name="TResponse"/>
    /// from a validation error. When <see langword="null"/>, a <see cref="ValidationException"/> is thrown instead.
    /// </param>
    public ValidationPipelineBehavior(
        IEnumerable<IValidator<TRequest>>? validators = null,
        IResultFactory<TResponse>? resultFactory = null)
    {
        _validators = validators ?? Enumerable.Empty<IValidator<TRequest>>();
        _resultFactory = resultFactory;
    }

    /// <inheritdoc/>
    /// <exception cref="ValidationException">
    /// Thrown when validation fails and no <see cref="IResultFactory{TResponse}"/> is registered.
    /// </exception>
    /// <exception cref="TaskCanceledException">
    /// Thrown when <paramref name="cancellationToken"/> is cancelled before all validators complete.
    /// </exception>
    public async ValueTask<TResponse> Handle<TNext>(
        TRequest request,
        TNext next,
        CancellationToken cancellationToken)
        where TNext : struct, INext<TResponse>
    {
        if (_validators.Any())
        {
            var context = new ValidationContext<TRequest>(request);
            var validationResults = await Task.WhenAll(
                _validators.Select(v => v.ValidateAsync(context, cancellationToken))).ConfigureAwait(false);

            var failures = validationResults
                .SelectMany(r => r.Errors)
                .Where(f => f is not null)
                .ToList();

            if (failures.Count > 0)
            {
                if (_resultFactory is not null)
                {
                    var combinedResult = new global::FluentValidation.Results.ValidationResult(failures);
                    var result = combinedResult.ToValidationResult();
                    return _resultFactory.CreateFailure(result.Error);
                }

                throw new ValidationException(failures);
            }
        }

        return await next.InvokeAsync().ConfigureAwait(false);
    }
}
