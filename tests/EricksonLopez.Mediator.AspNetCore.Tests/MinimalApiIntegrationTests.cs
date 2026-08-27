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
using EricksonLopez.Mediator.AspNetCore.Tests.Fixtures;
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

namespace EricksonLopez.Mediator.AspNetCore.Tests;

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
        var command = new CreateOrderCommand("Mechanical Keyboard", 129.99m);

        var response = await client.PostAsJsonAsync("/orders", command);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<OrderCreatedResponse>();
        result.Should().NotBeNull();
        result!.ProductName.Should().Be("Mechanical Keyboard");
        result.Price.Should().Be(129.99m);
        result.OrderId.Should().NotBeEmpty();
        response.Headers.Location.Should().Be($"/orders/{result.OrderId}");
    }

    [Fact]
    public async Task Post_CreateOrderEndpoint_InvalidInput_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();
        var command = new CreateOrderCommand("", 129.99m);

        var response = await client.PostAsJsonAsync("/orders", command);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Product name cannot be empty.");
    }

    [Fact]
    public async Task Post_ValidatedOrderEndpoint_ValidInput_ReturnsOk()
    {
        var client = _factory.CreateClient();
        var command = new ValidateOrderCommand("Mouse", 2);

        var response = await client.PostAsJsonAsync("/orders/validated", command);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Post_ValidatedOrderEndpoint_InvalidInput_ReturnsBadRequestWithValidationErrors()
    {
        var client = _factory.CreateClient();
        var command = new ValidateOrderCommand("", 0);

        var response = await client.PostAsJsonAsync("/orders/validated", command);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Validation.Failed");
    }

    [Fact]
    public async Task MapCommand_DirectEndpointMapping_HandlesPostRequest()
    {
        var client = _factory.CreateClient();
        var command = new CreateOrderCommand("Headphones", 89.99m);

        var response = await client.PostAsJsonAsync("/api/orders", command);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<OrderCreatedResponse>();
        result.Should().NotBeNull();
        result!.ProductName.Should().Be("Headphones");
        result.Price.Should().Be(89.99m);
    }

    [Fact]
    public async Task MapCommand_WithCustomHttpMethod_HandlesPutRequest()
    {
        var client = _factory.CreateClient();
        var command = new CreateOrderCommand("Monitor", 299.99m);

        var response = await client.PutAsJsonAsync("/api/orders/put", command);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<OrderCreatedResponse>();
        result.Should().NotBeNull();
        result!.ProductName.Should().Be("Monitor");
    }

    [Fact]
    public async Task MapQuery_DirectEndpointMapping_HandlesGetRequest()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/weather?city=Barcelona");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<WeatherResponse>();
        result.Should().NotBeNull();
        result!.City.Should().Be("Barcelona");
        result.Temperature.Should().Be(25);
    }

    [Fact]
    public async Task Post_NotifyEndpoint_PublishesNotificationAcrossHandlers()
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
    public async Task Get_FaultyEndpoint_PropagatesExceptionAsInternalServerError()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/faulty");

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Simulated internal handler exception");
    }

    [Fact]
    public async Task Get_ScopedHandlerEndpoint_ResolvesSameInstanceWithinSingleRequestScope()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/scoped-id");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var id1 = json.GetProperty("id1").GetGuid();
        var id2 = json.GetProperty("id2").GetGuid();

        id1.Should().Be(id2);
    }

    [Fact]
    public async Task Get_StreamEndpoint_ReturnsNdjsonStreamOfResults()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/stream/3");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var numbers = await response.Content.ReadFromJsonAsync<List<int>>();
        numbers.Should().NotBeNull();
        numbers.Should().HaveCount(3);
        numbers.Should().ContainInOrder(1, 2, 3);
    }

    [Fact]
    public async Task MapCommand_WithCancellationToken_PassesCancellationTokenToHandler()
    {
        var client = _factory.CreateClient();
        var command = new CancellableCommand(0);

        var response = await client.PostAsJsonAsync("/api/cancellable", command);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<string>();
        result.Should().Be("CancellableCompleted");
    }
}
