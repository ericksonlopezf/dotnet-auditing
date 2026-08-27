// Copyright © Erickson Lopez. MIT License.
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using EricksonLopez.Mediator;
using EricksonLopez.Mediator.Tests.Fixtures;
using Xunit;

namespace EricksonLopez.Mediator.Tests;

[Collection("StaticMediator")]
public class StaticMediatorTests
{
    [Fact]
    public async Task SendCommand_RegisteredCommandWithoutDI_DispatchesSuccessfully()
    {
        StaticMediator.Reset();
        StaticMediator.RegisterCommandHandler(new StaticPingCommandHandler());

        var resultTyped = await StaticMediator.SendCommand<StaticPingCommand, string>(
            new StaticPingCommand("World"), CancellationToken.None);
        resultTyped.Should().Be("StaticPong: World");
    }

    [Fact]
    public async Task SendQuery_RegisteredQueryWithoutDI_DispatchesSuccessfully()
    {
        StaticMediator.Reset();
        StaticMediator.RegisterQueryHandler(new StaticGetQueryHandler());

        var resultTyped = await StaticMediator.SendQuery<StaticGetQuery, int>(
            new StaticGetQuery(7), CancellationToken.None);
        resultTyped.Should().Be(70);
    }

    [Fact]
    public async Task Publish_RegisteredNotificationWithoutDI_DispatchesSuccessfully()
    {
        StaticMediator.Reset();
        var handler = new StaticNotificationHandler();
        StaticMediator.RegisterNotificationHandler(handler);

        await StaticMediator.Publish(new StaticNotification("OrderCreated"), CancellationToken.None);
        handler.HandledCount.Should().Be(1);
    }

    [Fact]
    public async Task Publish_MultipleNotificationHandlers_DispatchesToAll()
    {
        StaticMediator.Reset();
        var handler1 = new StaticNotificationHandler();
        var handler2 = new StaticNotificationHandler();

        StaticMediator.RegisterNotificationHandler(handler1);
        StaticMediator.RegisterNotificationHandler(handler2);

        await StaticMediator.Publish(new StaticNotification("MultiEvent"), CancellationToken.None);
        handler1.HandledCount.Should().Be(1);
        handler2.HandledCount.Should().Be(1);
    }

    [Fact]
    public async Task Publish_NoHandlersRegistered_DoesNotThrow()
    {
        StaticMediator.Reset();
        var act = async () => await StaticMediator.Publish(new StaticNotification("NoHandlerEvent"), CancellationToken.None);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public void RegisterCommandHandler_NullHandler_ThrowsArgumentNullException()
    {
        var act = () => StaticMediator.RegisterCommandHandler<StaticPingCommand, string>(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("handler");
    }

    [Fact]
    public void RegisterQueryHandler_NullHandler_ThrowsArgumentNullException()
    {
        var act = () => StaticMediator.RegisterQueryHandler<StaticGetQuery, int>(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("handler");
    }

    [Fact]
    public void RegisterNotificationHandler_NullHandler_ThrowsArgumentNullException()
    {
        var act = () => StaticMediator.RegisterNotificationHandler<StaticNotification>(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("handler");
    }

    [Fact]
    public async Task Publish_WithCancellationToken_PropagatesTokenToHandler()
    {
        StaticMediator.Reset();
        var handler = new StaticCancellableNotificationHandler();
        StaticMediator.RegisterNotificationHandler(handler);
        using var cts = new CancellationTokenSource();

        await StaticMediator.Publish(new StaticNotification("CancelCheck"), cts.Token);

        handler.ReceivedToken.Should().Be(cts.Token);
    }

    [Fact]
    public async Task Publish_WithPreCancelledCancellationToken_PassesCancelledTokenToHandler()
    {
        StaticMediator.Reset();
        var handler = new StaticCancellableNotificationHandler();
        StaticMediator.RegisterNotificationHandler(handler);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await StaticMediator.Publish(new StaticNotification("PreCancelledCheck"), cts.Token);

        handler.ReceivedToken.IsCancellationRequested.Should().BeTrue();
    }

    [Fact]
    public async Task Send_NullRequests_ThrowsArgumentNullException()
    {
        var act1 = async () => await StaticMediator.SendCommand<StaticPingCommand, string>(null!).AsTask();
        await act1.Should().ThrowAsync<ArgumentNullException>();

        var act2 = async () => await StaticMediator.SendQuery<StaticGetQuery, int>(null!).AsTask();
        await act2.Should().ThrowAsync<ArgumentNullException>();

        var act3 = async () => await StaticMediator.Publish<StaticNotification>(null!).AsTask();
        await act3.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task SendCommand_UnregisteredCommand_ThrowsInvalidOperationException()
    {
        StaticMediator.Reset();
        var act = async () => await StaticMediator.SendCommand<StaticPingCommand, string>(new StaticPingCommand("Fail")).AsTask();
        var ex = await act.Should().ThrowAsync<InvalidOperationException>();
        ex.Which.Message.Should().Contain(typeof(StaticPingCommand).FullName!);
        ex.Which.Message.Should().Contain("No static command handler registered for");
    }

    [Fact]
    public async Task SendQuery_UnregisteredQuery_ThrowsInvalidOperationException()
    {
        StaticMediator.Reset();
        var act = async () => await StaticMediator.SendQuery<StaticGetQuery, int>(new StaticGetQuery(99)).AsTask();
        var ex = await act.Should().ThrowAsync<InvalidOperationException>();
        ex.Which.Message.Should().Contain(typeof(StaticGetQuery).FullName!);
        ex.Which.Message.Should().Contain("No static query handler registered for");
    }

    [Fact]
    public async Task Reset_WhenInvoked_ClearsAllHandlers()
    {
        var handler = new StaticNotificationHandler();
        StaticMediator.RegisterCommandHandler(new StaticPingCommandHandler());
        StaticMediator.RegisterQueryHandler(new StaticGetQueryHandler());
        StaticMediator.RegisterNotificationHandler(handler);

        // Reset
        StaticMediator.Reset();

        // Verify cleared
        var actCmd = async () => await StaticMediator.SendCommand<StaticPingCommand, string>(new StaticPingCommand("PostReset")).AsTask();
        await actCmd.Should().ThrowAsync<InvalidOperationException>();

        var actQry = async () => await StaticMediator.SendQuery<StaticGetQuery, int>(new StaticGetQuery(3)).AsTask();
        await actQry.Should().ThrowAsync<InvalidOperationException>();

        await StaticMediator.Publish(new StaticNotification("PostReset"));
        handler.HandledCount.Should().Be(0); // not incremented
    }

    [Fact]
    public async Task Send_ConcurrentDispatch_IsThreadSafe()
    {
        StaticMediator.Reset();
        StaticMediator.RegisterCommandHandler(new StaticPingCommandHandler());
        StaticMediator.RegisterQueryHandler(new StaticGetQueryHandler());

        var timeout = TimeSpan.FromSeconds(10);
        using var cts = new CancellationTokenSource(timeout);

        var tasks = Enumerable.Range(0, 100).Select(async i =>
        {
            var cmdResult = await StaticMediator.SendCommand<StaticPingCommand, string>(
                new StaticPingCommand($"Msg_{i}"), cts.Token);
            var qryResult = await StaticMediator.SendQuery<StaticGetQuery, int>(
                new StaticGetQuery(i), cts.Token);

            cmdResult.Should().Be($"StaticPong: Msg_{i}");
            qryResult.Should().Be(i * 10);
        });

        var allTasks = Task.WhenAll(tasks);
        var completedTask = await Task.WhenAny(allTasks, Task.Delay(timeout, CancellationToken.None));

        completedTask.Should().Be(allTasks, "concurrent dispatch should complete within 10 seconds without deadlocks");
        await allTasks; // propagate any exceptions
    }
}
