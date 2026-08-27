// Copyright © Erickson Lopez. MIT License.
using System;
using AwesomeAssertions;
using EricksonLopez.Mediator.AspNetCore;
using EricksonLopez.Mediator.AspNetCore.Tests.Fixtures;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EricksonLopez.Mediator.AspNetCore.Tests;

public class AspNetCoreEndpointGuardTests
{
    private readonly IEndpointRouteBuilder _dummyBuilder;

    public AspNetCoreEndpointGuardTests()
    {
        var builder = WebApplication.CreateBuilder();
        _dummyBuilder = builder.Build();
    }

    [Fact]
    public void MapCommand_NullArguments_ThrowsArgumentNullException()
    {
        IEndpointRouteBuilder nullEndpoints = null!;
        var act1 = () => nullEndpoints.MapCommand<CreateOrderCommand, OrderCreatedResponse>("/api/orders");
        act1.Should().Throw<ArgumentNullException>().WithParameterName("endpoints");

        var act2 = () => _dummyBuilder.MapCommand<CreateOrderCommand, OrderCreatedResponse>(null!);
        act2.Should().Throw<ArgumentNullException>().WithParameterName("pattern");

        var act3 = () => nullEndpoints.MapCommand<CreateOrderCommand, OrderCreatedResponse>("/api/orders", "POST");
        act3.Should().Throw<ArgumentNullException>().WithParameterName("endpoints");

        var act4 = () => _dummyBuilder.MapCommand<CreateOrderCommand, OrderCreatedResponse>(null!, "POST");
        act4.Should().Throw<ArgumentNullException>().WithParameterName("pattern");

        var act5 = () => _dummyBuilder.MapCommand<CreateOrderCommand, OrderCreatedResponse>("/api/orders", null!);
        act5.Should().Throw<ArgumentNullException>().WithParameterName("httpMethod");
    }

    [Fact]
    public void MapQuery_NullArguments_ThrowsArgumentNullException()
    {
        IEndpointRouteBuilder nullEndpoints = null!;
        var act1 = () => nullEndpoints.MapQuery<GetWeatherQuery, WeatherResponse>("/api/weather");
        act1.Should().Throw<ArgumentNullException>().WithParameterName("endpoints");

        var act2 = () => _dummyBuilder.MapQuery<GetWeatherQuery, WeatherResponse>(null!);
        act2.Should().Throw<ArgumentNullException>().WithParameterName("pattern");
    }
}
