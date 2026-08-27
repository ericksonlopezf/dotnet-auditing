// Copyright © Erickson Lopez. MIT License.
using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace EricksonLopez.Mediator.AspNetCore;

/// <summary>
/// Provides extension methods on <see cref="IEndpointRouteBuilder"/> to map CQRS commands and queries as Minimal API endpoints.
/// </summary>
public static class MediatorEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps an HTTP POST endpoint for the specified command type <typeparamref name="TCommand"/>.
    /// </summary>
    /// <typeparam name="TCommand">The command type implementing <see cref="ICommand{TResponse}"/>.</typeparam>
    /// <typeparam name="TResponse">The response type returned by the command handler.</typeparam>
    /// <param name="endpoints">The route builder to map the endpoint into.</param>
    /// <param name="pattern">The URL route pattern.</param>
    /// <returns>A <see cref="RouteHandlerBuilder"/> that can be used to further customize the endpoint.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="endpoints"/> or <paramref name="pattern"/> is <see langword="null"/>.</exception>
    [RequiresUnreferencedCode("Minimal API route delegate binding uses reflection in ASP.NET Core.")]
    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode", Justification = "ASP.NET Core Minimal APIs route mapping delegation")]
    public static RouteHandlerBuilder MapCommand<TCommand, TResponse>(
        this IEndpointRouteBuilder endpoints,
        string pattern)
        where TCommand : ICommand<TResponse>
    {
        return endpoints.MapCommand<TCommand, TResponse>(pattern, "POST");
    }

    /// <summary>
    /// Maps an HTTP endpoint with the specified HTTP method for the specified command type <typeparamref name="TCommand"/>.
    /// </summary>
    /// <typeparam name="TCommand">The command type implementing <see cref="ICommand{TResponse}"/>.</typeparam>
    /// <typeparam name="TResponse">The response type returned by the command handler.</typeparam>
    /// <param name="endpoints">The route builder to map the endpoint into.</param>
    /// <param name="pattern">The URL route pattern.</param>
    /// <param name="httpMethod">The HTTP method to map (for example, "POST", "PUT", "DELETE").</param>
    /// <returns>A <see cref="RouteHandlerBuilder"/> that can be used to further customize the endpoint.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="endpoints"/>, <paramref name="pattern"/>, or <paramref name="httpMethod"/> is <see langword="null"/>.</exception>
    [RequiresUnreferencedCode("Minimal API route delegate binding uses reflection in ASP.NET Core.")]
    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode", Justification = "ASP.NET Core Minimal APIs route mapping delegation")]
    public static RouteHandlerBuilder MapCommand<TCommand, TResponse>(
        this IEndpointRouteBuilder endpoints,
        string pattern,
        string httpMethod)
        where TCommand : ICommand<TResponse>
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(pattern);
        ArgumentNullException.ThrowIfNull(httpMethod);
        _ = endpoints.DataSources;
        _ = pattern.Length;

        return endpoints.MapMethods(pattern, new[] { httpMethod }, async ([FromBody] TCommand command, ISender sender, CancellationToken cancellationToken) =>
        {
            var response = await sender.SendCommand<TCommand, TResponse>(command, cancellationToken).ConfigureAwait(false);
            return Results.Ok(response);
        });
    }

    /// <summary>
    /// Maps an HTTP GET endpoint for the specified query type <typeparamref name="TQuery"/>.
    /// </summary>
    /// <typeparam name="TQuery">The query type implementing <see cref="IQuery{TResponse}"/>.</typeparam>
    /// <typeparam name="TResponse">The response type returned by the query handler.</typeparam>
    /// <param name="endpoints">The route builder to map the endpoint into.</param>
    /// <param name="pattern">The URL route pattern.</param>
    /// <returns>A <see cref="RouteHandlerBuilder"/> that can be used to further customize the endpoint.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="endpoints"/> or <paramref name="pattern"/> is <see langword="null"/>.</exception>
    [RequiresUnreferencedCode("Minimal API route delegate binding uses reflection in ASP.NET Core.")]
    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode", Justification = "ASP.NET Core Minimal APIs route mapping delegation")]
    public static RouteHandlerBuilder MapQuery<TQuery, TResponse>(
        this IEndpointRouteBuilder endpoints,
        string pattern)
        where TQuery : IQuery<TResponse>
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(pattern);
        _ = endpoints.DataSources;
        _ = pattern.Length;

        return endpoints.MapGet(pattern, async ([AsParameters] TQuery query, ISender sender, CancellationToken cancellationToken) =>
        {
            var response = await sender.SendQuery<TQuery, TResponse>(query, cancellationToken).ConfigureAwait(false);
            return Results.Ok(response);
        });
    }
}
