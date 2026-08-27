// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using EricksonLopez.Mediator;
using EricksonLopez.Mediator.AspNetCore;
using EricksonLopez.Mediator.Result;
using EricksonLopez.Result;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace EricksonLopez.Mediator.IntegrationTests;

using EricksonLopez.Mediator.IntegrationTests.Fixtures;

// ─── Integration Tests ────────────────────────────────────────────────────────

public class MinimalApiIntegrationTests : IClassFixture<MediatorApplicationFactory>
{
    private readonly MediatorApplicationFactory _factory;

    public MinimalApiIntegrationTests(MediatorApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Get_WeatherEndpoint_ReturnsSuccessStatusCodeAndPayload()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/weather/Madrid");

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<WeatherResponse>();
        result.Should().NotBeNull();
        result!.City.Should().Be("Madrid");
        result.Temperature.Should().Be(25);
    }

    [Fact]
    public async Task Post_CreateOrderEndpoint_ReturnsCreatedStatusCodeAndResponse()
    {
        var client = _factory.CreateClient();
        var command = new CreateOrderCommand("Mechanical Keyboard", 149.99m);

        var response = await client.PostAsJsonAsync("/orders", command);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<OrderCreatedResponse>();
        result.Should().NotBeNull();
        result!.ProductName.Should().Be("Mechanical Keyboard");
        result.Price.Should().Be(149.99m);
        result.OrderId.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task Post_CreateOrderEndpointWithInvalidInput_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();
        var invalidCommand = new CreateOrderCommand("", 100m);

        var response = await client.PostAsJsonAsync("/orders", invalidCommand);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Product name cannot be empty");
    }

    [Fact]
    public async Task Post_OrderNotifyEndpoint_ExecutesRegisteredNotificationHandler()
    {
        var client = _factory.CreateClient();
        var orderId = Guid.NewGuid();
        var notification = new OrderNotification(orderId);

        var response = await client.PostAsJsonAsync("/orders/notify", notification);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var auditLog = _factory.Services.GetRequiredService<NotificationAuditLog>();
        auditLog.Received.Should().Contain(orderId);
    }

    [Fact]
    public async Task Get_FaultyEndpoint_PropagatesInternalServerError500()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/faulty");

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task Get_ScopedIdEndpoint_MaintainsScopedLifetimeInHttpRequest()
    {
        var client = _factory.CreateClient();

        var res1 = await client.GetFromJsonAsync<ScopedResponse>("/scoped-id");
        var res2 = await client.GetFromJsonAsync<ScopedResponse>("/scoped-id");

        res1.Should().NotBeNull();
        res2.Should().NotBeNull();

        // In same request, both resolutions should get same scoped instance
        res1!.id1.Should().Be(res1.id2);
        res2!.id1.Should().Be(res2.id2);
    }

    [Fact]
    public async Task Get_StreamEndpoint_ReturnsAsyncEnumerableStream()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/stream/3");

        response.EnsureSuccessStatusCode();
        var items = await response.Content.ReadFromJsonAsync<List<int>>();
        items.Should().NotBeNull();
        items.Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task MapCommand_ExecutesPostEndpointSuccessfully()
    {
        var client = _factory.CreateClient();
        var command = new CreateOrderCommand("Book", 29.99m);

        var response = await client.PostAsJsonAsync("/api/orders", command);

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<OrderCreatedResponse>();
        result.Should().NotBeNull();
        result!.ProductName.Should().Be("Book");
        result.Price.Should().Be(29.99m);
        result.OrderId.Should().NotBeEmpty();
    }

    [Fact]
    public async Task MapQuery_ExecutesGetEndpointSuccessfully()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/weather?City=Seattle");

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<WeatherResponse>();
        result.Should().NotBeNull();
        result!.City.Should().Be("Seattle");
        result.Temperature.Should().Be(25);
    }

    [Fact]
    public async Task Post_ValidatedOrder_WhenValid_ReturnsOkAndValue()
    {
        var client = _factory.CreateClient();
        var command = new ValidateOrderCommand("Mechanical Keyboard", 2);

        var response = await client.PostAsJsonAsync("/orders/validated", command);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ValidatedOrderResponse>();
        result.Should().NotBeNull();
        result!.orderId.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Post_ValidatedOrder_WhenInvalid_ReturnsBadRequestWithResultError()
    {
        var client = _factory.CreateClient();
        var invalidCommand = new ValidateOrderCommand("", 0);

        var response = await client.PostAsJsonAsync("/orders/validated", invalidCommand);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var error = await response.Content.ReadFromJsonAsync<ValidationErrorResponse>();
        error.Should().NotBeNull();
        error!.code.Should().Be("Validation.Failed");
        error.description.Should().Be("2 validation error(s) occurred.");
    }

    [Fact]
    public async Task Post_CancellableCommand_WhenTokenCancelled_CancelsRequest()
    {
        var client = _factory.CreateClient();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var command = new CancellableCommand(1000);
        var act = async () => await client.PostAsJsonAsync("/api/cancellable", command, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Post_CancellableCommand_WhenNotCancelled_CompletesSuccessfully()
    {
        var client = _factory.CreateClient();
        var command = new CancellableCommand(0);

        var response = await client.PostAsJsonAsync("/api/cancellable", command);

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadAsStringAsync();
        result.Should().Contain("CancellableCompleted");
    }

    [Fact]
    public async Task Put_MapCommandEndpoint_ReturnsSuccessStatusCodeAndResponse()
    {
        var client = _factory.CreateClient();
        var command = new CreateOrderCommand("Gaming Mouse", 79.99m);

        var response = await client.PutAsJsonAsync("/api/orders/put", command);

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<OrderCreatedResponse>();
        result.Should().NotBeNull();
        result!.ProductName.Should().Be("Gaming Mouse");
        result.Price.Should().Be(79.99m);
        result.OrderId.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task Isolated_MapCommand_RegistersAndExecutesCorrectly()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddEricksonLopezMediator();
        await using var app = builder.Build();

        var builderResult = app.MapCommand<CreateOrderCommand, OrderCreatedResponse>("/isolated/orders");
        builderResult.Should().NotBeNull();
        await app.StartAsync();

        using var client = app.GetTestClient();
        var command = new CreateOrderCommand("Monitor", 299m);
        var response = await client.PostAsJsonAsync("/isolated/orders", command);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<OrderCreatedResponse>();
        result.Should().NotBeNull();
        result!.ProductName.Should().Be("Monitor");
        result.Price.Should().Be(299m);
    }

    [Fact]
    public async Task Isolated_MapCommand_CustomMethod_RegistersAndExecutesCorrectly()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddEricksonLopezMediator();
        await using var app = builder.Build();

        var builderResult = app.MapCommand<CreateOrderCommand, OrderCreatedResponse>("/isolated/orders/delete", "DELETE");
        builderResult.Should().NotBeNull();
        await app.StartAsync();

        using var client = app.GetTestClient();
        var command = new CreateOrderCommand("DeleteMe", 10m);
        var request = new HttpRequestMessage(HttpMethod.Delete, "/isolated/orders/delete")
        {
            Content = JsonContent.Create(command)
        };
        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<OrderCreatedResponse>();
        result.Should().NotBeNull();
        result!.ProductName.Should().Be("DeleteMe");
    }

    [Fact]
    public async Task Isolated_MapQuery_RegistersAndExecutesCorrectly()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddEricksonLopezMediator();
        await using var app = builder.Build();

        var builderResult = app.MapQuery<GetWeatherQuery, WeatherResponse>("/isolated/weather");
        builderResult.Should().NotBeNull();
        await app.StartAsync();

        using var client = app.GetTestClient();
        var response = await client.GetAsync("/isolated/weather?City=London");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<WeatherResponse>();
        result.Should().NotBeNull();
        result!.City.Should().Be("London");
        result.Temperature.Should().Be(25);
    }

    private record ScopedResponse(Guid id1, Guid id2);
    private record ValidatedOrderResponse(Guid orderId);
    private record ValidationErrorResponse(string code, string description);
}






