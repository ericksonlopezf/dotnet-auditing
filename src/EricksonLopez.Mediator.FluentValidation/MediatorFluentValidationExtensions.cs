// Copyright © Erickson Lopez. MIT License.
using System;
using System.Reflection;
using EricksonLopez.Mediator;
using EricksonLopez.Mediator.FluentValidation;
using global::FluentValidation;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Provides extension methods for configuring <see cref="ValidationPipelineBehavior{TRequest, TResponse}"/>
/// and FluentValidation validators in dependency injection.
/// </summary>
/// <remarks>
/// This class is the successor to the deprecated <c>MediatorValidationExtensions</c> in
/// <c>EricksonLopez.Mediator.Validation</c> (deprecated via ADR-033).
/// </remarks>
public static class MediatorFluentValidationExtensions
{
    /// <summary>
    /// Registers the open generic <see cref="ValidationPipelineBehavior{TRequest, TResponse}"/>
    /// as a pipeline behavior in the service collection.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// Call this method once during application startup (e.g., in <c>Program.cs</c>).
    /// It registers <see cref="ValidationPipelineBehavior{TRequest, TResponse}"/> as an open generic
    /// <see cref="IPipelineBehavior{TRequest, TResponse}"/> using <c>TryAddTransient</c>, so
    /// calling it more than once is safe.
    /// </para>
    /// <para>
    /// Validators (<see cref="IValidator{T}"/>) must be registered separately using
    /// <see cref="AddMediatorFluentValidatorsFromAssembly"/> or
    /// <see cref="AddMediatorFluentValidationValidator{TValidator,TRequest}"/>.
    /// </para>
    /// <example>
    /// <code>
    /// builder.Services.AddMediatorFluentValidation();
    /// builder.Services.AddMediatorFluentValidatorsFromAssembly(typeof(Program).Assembly);
    /// </code>
    /// </example>
    /// </remarks>
    public static IServiceCollection AddMediatorFluentValidation(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationPipelineBehavior<,>));
        services.TryAddTransient(typeof(ValidationPipelineBehavior<,>));
        return services;
    }

    /// <summary>
    /// Discovers and registers all FluentValidation validators found in the specified assembly into the service collection.
    /// </summary>
    /// <param name="services">The service collection to register validators into.</param>
    /// <param name="assembly">The assembly to scan for <see cref="IValidator{T}"/> implementations.</param>
    /// <param name="lifetime">
    /// The service lifetime applied to discovered validators. Defaults to <see cref="ServiceLifetime.Scoped"/>.
    /// </param>
    /// <returns>The same <see cref="IServiceCollection"/> instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="services"/> or <paramref name="assembly"/> is <see langword="null"/>.
    /// </exception>
    /// <remarks>
    /// <para>
    /// <strong>AOT / Trimming:</strong> This method uses <see cref="AssemblyScanner"/> from FluentValidation,
    /// which relies on <see cref="System.Reflection"/> to enumerate types. It is annotated with
    /// <see cref="System.Diagnostics.CodeAnalysis.RequiresUnreferencedCodeAttribute"/> and is NOT compatible
    /// with Native AOT or aggressive trimming. Use
    /// <see cref="AddMediatorFluentValidationValidator{TValidator,TRequest}"/> for an AOT-safe alternative.
    /// </para>
    /// <example>
    /// <code>
    /// // Not AOT-safe — use in standard .NET apps only
    /// builder.Services.AddMediatorFluentValidation();
    /// builder.Services.AddMediatorFluentValidatorsFromAssembly(typeof(Program).Assembly);
    /// </code>
    /// </example>
    /// </remarks>
    [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Assembly scanning relies on dynamic reflection. Use AddMediatorFluentValidationValidator<TValidator, TRequest>() for Native AOT compatibility.")]
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "Validators discovered via AssemblyScanner are registered into DI; callers must ensure types are preserved.")]
    public static IServiceCollection AddMediatorFluentValidatorsFromAssembly(
        this IServiceCollection services,
        Assembly assembly,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(assembly);

        AssemblyScanner.FindValidatorsInAssembly(assembly).ForEach(scanResult =>
        {
            services.Add(new ServiceDescriptor(scanResult.InterfaceType, scanResult.ValidatorType, lifetime));
        });

        services.AddMediatorFluentValidation();
        return services;
    }

    /// <summary>
    /// Registers a single FluentValidation validator explicitly, without assembly scanning.
    /// </summary>
    /// <typeparam name="TValidator">The concrete validator type to register.</typeparam>
    /// <typeparam name="TRequest">The request type that <typeparamref name="TValidator"/> validates.</typeparam>
    /// <param name="services">The service collection to register the validator into.</param>
    /// <param name="lifetime">
    /// The service lifetime applied to the validator. Defaults to <see cref="ServiceLifetime.Scoped"/>.
    /// </param>
    /// <returns>The same <see cref="IServiceCollection"/> instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// <para>
    /// This overload is the <strong>AOT-safe</strong> alternative to
    /// <see cref="AddMediatorFluentValidatorsFromAssembly"/>. Because the validator type is
    /// specified as a generic parameter, the compiler preserves the type and its metadata under trimming.
    /// </para>
    /// <example>
    /// <code>
    /// // AOT-safe registration
    /// builder.Services.AddMediatorFluentValidation();
    /// builder.Services.AddMediatorFluentValidationValidator&lt;CreateUserCommandValidator, CreateUserCommand&gt;();
    /// </code>
    /// </example>
    /// </remarks>
    public static IServiceCollection AddMediatorFluentValidationValidator<[System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicConstructors)] TValidator, TRequest>(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where TValidator : class, IValidator<TRequest>
    {
        ArgumentNullException.ThrowIfNull(services);

        services.Add(new ServiceDescriptor(typeof(IValidator<TRequest>), typeof(TValidator), lifetime));
        services.AddMediatorFluentValidation();
        return services;
    }
}
