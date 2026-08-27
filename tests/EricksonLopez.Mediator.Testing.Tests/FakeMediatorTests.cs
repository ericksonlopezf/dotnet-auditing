// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using EricksonLopez.Mediator;
using EricksonLopez.Mediator.Testing;
using Xunit;

namespace EricksonLopez.Mediator.Testing.Tests;

public class FakeMediatorTests
{
    public sealed record DummyCommand : ICommand<string>;
    public sealed record DummyQuery : IQuery<string>;
    public sealed record DummyNotification : INotification;
    public sealed record AnotherNotification : INotification;

    public interface ISharedMessage
    {
        string Tag { get; }
    }

    public sealed record TaggedCommand(string Tag) : ICommand<string>, ISharedMessage;
    public sealed record TaggedNotification(string Tag) : INotification, ISharedMessage;

    public class DummyCommandHandler : ICommandHandler<DummyCommand, string>
    {
        public ValueTask<string> Handle(DummyCommand command, CancellationToken cancellationToken) => default;
    }

    public class DummyQueryHandler : IQueryHandler<DummyQuery, string>
    {
        public ValueTask<string> Handle(DummyQuery query, CancellationToken cancellationToken) => default;
    }

    public class DummyNotificationHandler : INotificationHandler<DummyNotification>
    {
        public ValueTask Handle(DummyNotification notification, CancellationToken cancellationToken) => default;
    }

    [Fact]
    public async Task SetupCommand_SyncDelegate_ReturnsExpectedValue_AndEnablesFluentChaining()
    {
        var fake = new FakeMediator();
        var fluent = fake.SetupCommand<DummyCommand, string>(c => "success");
        fluent.Should().BeSameAs(fake);

        var cmd = new DummyCommand();
        var result = await fake.Send(cmd);

        result.Should().Be("success");
        fake.ShouldHaveReceived<DummyCommand>();
        fake.ReceivedCount<DummyCommand>().Should().Be(1);
        fake.ReceivedRequests.Should().Contain(cmd);
    }

    [Fact]
    public async Task SetupCommand_AsyncDelegate_ReturnsExpectedValue_AndEnablesFluentChaining()
    {
        var fake = new FakeMediator();
        using var cts = new CancellationTokenSource();
        CancellationToken receivedCt = default;
        var fluent = fake.SetupCommand<DummyCommand, string>((c, ct) =>
        {
            receivedCt = ct;
            return new ValueTask<string>("success-async-" + c.GetType().Name);
        });
        fluent.Should().BeSameAs(fake);

        var cmd = new DummyCommand();
        var result1 = await fake.Send(cmd, cts.Token);
        var result2 = await fake.SendCommand<DummyCommand, string>(cmd, cts.Token);

        result1.Should().Be("success-async-DummyCommand");
        result2.Should().Be("success-async-DummyCommand");
        receivedCt.Should().Be(cts.Token);
    }

    [Fact]
    public async Task Send_CommandWithoutSetup_ThrowsInvalidOperationException()
    {
        var fake = new FakeMediator();

        var action = async () => await fake.Send(new DummyCommand());

        var ex = await action.Should().ThrowAsync<InvalidOperationException>();
        ex.Which.Message.Should().Be("FakeMediator: no handler for command 'DummyCommand'. Call SetupCommand<DummyCommand, TResponse>(...) in test setup.");
    }

    [Fact]
    public async Task SetupQuery_SyncDelegate_ReturnsExpectedValue_AndEnablesFluentChaining()
    {
        var fake = new FakeMediator();
        var fluent = fake.SetupQuery<DummyQuery, string>(q => "query-success");
        fluent.Should().BeSameAs(fake);

        var query = new DummyQuery();
        var result = await fake.Send(query);

        result.Should().Be("query-success");
        fake.ShouldHaveReceived<DummyQuery>();
        fake.ReceivedCount<DummyQuery>().Should().Be(1);
    }

    [Fact]
    public async Task SetupQuery_AsyncDelegate_ReturnsExpectedValue_AndPassesCancellationToken()
    {
        var fake = new FakeMediator();
        using var cts = new CancellationTokenSource();
        CancellationToken receivedCt = default;
        var fluent = fake.SetupQuery<DummyQuery, string>((q, ct) =>
        {
            receivedCt = ct;
            return new ValueTask<string>("query-async-" + q.GetType().Name);
        });
        fluent.Should().BeSameAs(fake);

        var query = new DummyQuery();
        var result1 = await fake.Send(query, cts.Token);
        var result2 = await fake.SendQuery<DummyQuery, string>(query, cts.Token);

        result1.Should().Be("query-async-DummyQuery");
        result2.Should().Be("query-async-DummyQuery");
        receivedCt.Should().Be(cts.Token);
    }

    [Fact]
    public async Task Send_QueryWithoutSetup_ThrowsInvalidOperationException()
    {
        var fake = new FakeMediator();

        var action = async () => await fake.Send(new DummyQuery());

        var ex = await action.Should().ThrowAsync<InvalidOperationException>();
        ex.Which.Message.Should().Be("FakeMediator: no handler for query 'DummyQuery'. Call SetupQuery<DummyQuery, TResponse>(...) in test setup.");
    }

    [Fact]
    public async Task Publish_ConfiguredNotificationHandlers_ExecutesAllHandlers_AndEnablesFluentChaining()
    {
        var fake = new FakeMediator();
        int count1 = 0;
        int count2 = 0;
        var fluent1 = fake.SetupNotification<DummyNotification>((n, ct) => { count1++; return default; });
        var fluent2 = fake.SetupNotification<DummyNotification>((n, ct) => { count2++; return default; });

        fluent1.Should().BeSameAs(fake);
        fluent2.Should().BeSameAs(fake);

        var notification = new DummyNotification();
        await fake.Publish(notification);

        count1.Should().Be(1);
        count2.Should().Be(1);
        fake.ShouldHaveReceived<DummyNotification>();
        fake.ReceivedCount<DummyNotification>().Should().Be(1);
        fake.ReceivedNotifications.Should().Contain(notification);
    }

    [Fact]
    public async Task Publish_NotificationWithoutSetup_DoesNotThrowAndRecordsInvocation()
    {
        var fake = new FakeMediator();
        var notification = new DummyNotification();

        await fake.Publish(notification);

        fake.ShouldHaveReceived<DummyNotification>();
        fake.ReceivedCount<DummyNotification>().Should().Be(1);
    }

    [Fact]
    public async Task ShouldHaveReceived_WhenMultipleDifferentNotificationsReceived_CorrectlyIdentifiesTargetNotification()
    {
        var fake = new FakeMediator();
        await fake.Publish(new DummyNotification());
        await fake.Publish(new AnotherNotification());

        fake.ShouldHaveReceived<DummyNotification>();
        fake.ShouldHaveReceived<AnotherNotification>();
        fake.ReceivedCount<DummyNotification>().Should().Be(1);
        fake.ReceivedCount<AnotherNotification>().Should().Be(1);
    }

    public sealed record DummyQueryWithProp(string Prop) : IQuery<string>;

    public class DummyQueryWithPropHandler : IQueryHandler<DummyQueryWithProp, string>
    {
        public ValueTask<string> Handle(DummyQueryWithProp query, CancellationToken cancellationToken) => default;
    }

    [Fact]
    public async Task ShouldHaveReceived_MatchingPredicate_PassesAndFailsOnNonMatch()
    {
        var fake = new FakeMediator();
        fake.SetupQuery<DummyQueryWithProp, string>(c => "success");
        await fake.Send(new DummyQueryWithProp("first"));
        await fake.Send(new DummyQueryWithProp("second"));

        fake.ShouldHaveReceived<DummyQueryWithProp>(c => c.Prop == "first");
        fake.ShouldHaveReceived<DummyQueryWithProp>(c => c.Prop == "second");

        var act = () => fake.ShouldHaveReceived<DummyQueryWithProp>(c => c.Prop == "non-existent");
        var ex = act.Should().Throw<FakeAssertionException>();
        ex.Which.Message.Should().Be("Expected to have received 'DummyQueryWithProp' matching the predicate, but no match found.");
    }

    [Fact]
    public async Task ShouldHaveReceived_WhenPredicateDoesNotMatch_ThrowsFakeAssertionExceptionWithExactMessage()
    {
        var fake = new FakeMediator();
        fake.SetupQuery<DummyQueryWithProp, string>(c => "val");
        await fake.Send(new DummyQueryWithProp("test-prop"));

        var act = () => fake.ShouldHaveReceived<DummyQueryWithProp>(q => q.Prop == "unmatched-value");
        var ex = act.Should().Throw<FakeAssertionException>();
        ex.Which.Message.Should().Be("Expected to have received 'DummyQueryWithProp' matching the predicate, but no match found.");
    }

    [Fact]
    public async Task ShouldHaveReceived_NotificationPredicate_VerifiesNotification()
    {
        var fake = new FakeMediator();
        await fake.Publish(new DummyNotification());

        fake.ShouldHaveReceived<DummyNotification>(n => true);
    }

    [Fact]
    public async Task ShouldHaveReceived_WithPredicate_SearchesBothRequestsAndNotifications()
    {
        var fake = new FakeMediator();
        fake.SetupCommand<TaggedCommand, string>(c => c.Tag);
        await fake.Send(new TaggedCommand("from-command"));
        await fake.Publish(new TaggedNotification("from-notification"));

        fake.ShouldHaveReceived<ISharedMessage>(m => m.Tag == "from-command");
        fake.ShouldHaveReceived<ISharedMessage>(m => m.Tag == "from-notification");
        fake.ReceivedCount<ISharedMessage>().Should().Be(2);
    }

    [Fact]
    public void ShouldHaveReceived_RequestNotReceived_ThrowsFakeAssertionException()
    {
        var fake = new FakeMediator();

        var act = () => fake.ShouldHaveReceived<DummyCommand>();
        var ex = act.Should().Throw<FakeAssertionException>();
        ex.Which.Message.Should().Be("Expected to have received 'DummyCommand', but none was received.");
    }

    [Fact]
    public void ShouldHaveReceived_WithPredicate_WhenNoRequestsReceived_ThrowsFakeAssertionException()
    {
        var fake = new FakeMediator();

        var act = () => fake.ShouldHaveReceived<DummyCommand>(c => true);
        var ex = act.Should().Throw<FakeAssertionException>();
        ex.Which.Message.Should().Be("Expected to have received 'DummyCommand', but none was received.");
    }

    [Fact]
    public async Task ShouldNotHaveReceived_RequestReceived_ThrowsFakeAssertionException()
    {
        var fake = new FakeMediator();
        fake.SetupCommand<DummyCommand, string>(c => "success");
        await fake.Send(new DummyCommand());

        var act = () => fake.ShouldNotHaveReceived<DummyCommand>();
        var ex = act.Should().Throw<FakeAssertionException>();
        ex.Which.Message.Should().Be("Expected NOT to have received 'DummyCommand', but one was received.");
    }

    [Fact]
    public void ShouldNotHaveReceived_RequestNotReceived_PassesSuccessfully()
    {
        var fake = new FakeMediator();
        fake.ShouldNotHaveReceived<DummyCommand>();
    }

    [Fact]
    public async Task ShouldNotHaveReceived_WhenMultipleDifferentRequestsReceived_IdentifiesPresentAndMissingTypes()
    {
        var fake = new FakeMediator();
        fake.SetupCommand<DummyCommand, string>(c => "cmd");
        fake.SetupQuery<DummyQuery, string>(q => "qry");
        await fake.Send(new DummyCommand());
        await fake.Send(new DummyQuery());

        // Present types should throw
        var act1 = () => fake.ShouldNotHaveReceived<DummyCommand>();
        var ex1 = act1.Should().Throw<FakeAssertionException>();
        ex1.Which.Message.Should().Be("Expected NOT to have received 'DummyCommand', but one was received.");

        var act2 = () => fake.ShouldNotHaveReceived<DummyQuery>();
        var ex2 = act2.Should().Throw<FakeAssertionException>();
        ex2.Which.Message.Should().Be("Expected NOT to have received 'DummyQuery', but one was received.");

        // Absent type should pass
        fake.ShouldNotHaveReceived<DummyNotification>();
    }

    [Fact]
    public async Task ShouldNotHaveReceived_WhenMultipleDifferentNotificationsReceived_IdentifiesPresentAndMissingTypes()
    {
        var fake = new FakeMediator();
        await fake.Publish(new DummyNotification());
        await fake.Publish(new AnotherNotification());

        // Present types should throw
        var act1 = () => fake.ShouldNotHaveReceived<DummyNotification>();
        var ex1 = act1.Should().Throw<FakeAssertionException>();
        ex1.Which.Message.Should().Be("Expected NOT to have received 'DummyNotification', but one was received.");

        var act2 = () => fake.ShouldNotHaveReceived<AnotherNotification>();
        var ex2 = act2.Should().Throw<FakeAssertionException>();
        ex2.Which.Message.Should().Be("Expected NOT to have received 'AnotherNotification', but one was received.");

        // Absent type should pass
        fake.ShouldNotHaveReceived<DummyCommand>();
    }

    public sealed record DummyStreamRequest : IStreamRequest<string>;

    public class DummyStreamRequestHandler : IStreamRequestHandler<DummyStreamRequest, string>
    {
        public async IAsyncEnumerable<string> Handle(DummyStreamRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.Yield();
            yield return "stream-success";
        }
    }

    [Fact]
    public async Task SetupStream_ConfiguredDelegate_YieldsExpectedStreamValues_AndEnablesFluentChaining()
    {
        var fake = new FakeMediator();
        using var cts = new CancellationTokenSource();
        CancellationToken receivedCt = default;

        async IAsyncEnumerable<string> DummyStreamFunc(DummyStreamRequest req, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
        {
            receivedCt = ct;
            await Task.Yield();
            yield return "stream-item-1";
            yield return "stream-item-2";
        }

        var fluent = fake.SetupStream<DummyStreamRequest, string>(DummyStreamFunc);
        fluent.Should().BeSameAs(fake);

        var req = new DummyStreamRequest();
        var items = new List<string>();
        await foreach (var item in fake.CreateStream(req, cts.Token))
        {
            items.Add(item);
        }

        items.Should().Equal("stream-item-1", "stream-item-2");
        receivedCt.Should().Be(cts.Token);
        fake.ShouldHaveReceived<DummyStreamRequest>();
        fake.ReceivedCount<DummyStreamRequest>().Should().Be(1);
        fake.ReceivedRequests.Should().Contain(req);
    }

    [Fact]
    public void CreateStream_StreamWithoutSetup_ThrowsInvalidOperationException()
    {
        var fake = new FakeMediator();

        var action = () => fake.CreateStream(new DummyStreamRequest());

        var ex = action.Should().Throw<InvalidOperationException>();
        ex.Which.Message.Should().Be("FakeMediator: no handler for stream request 'DummyStreamRequest'. Call SetupStream<DummyStreamRequest, TResponse>(...) in test setup.");
    }

    [Fact]
    public async Task CreateStream_WithCancellation_HonorsCancellationToken()
    {
        var fake = new FakeMediator();
        using var cts = new CancellationTokenSource();

        async IAsyncEnumerable<string> GenerateNumbers(DummyStreamRequest req, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
        {
            for (int i = 0; i < 100; i++)
            {
                ct.ThrowIfCancellationRequested();
                yield return i.ToString();
            }
            await Task.CompletedTask;
        }

        fake.SetupStream<DummyStreamRequest, string>(GenerateNumbers);

        var list = new List<string>();
        var act = async () =>
        {
            await foreach (var item in fake.CreateStream(new DummyStreamRequest(), cts.Token))
            {
                list.Add(item);
                if (list.Count == 2)
                    cts.Cancel();
            }
        };

        await act.Should().ThrowAsync<OperationCanceledException>();
        list.Should().HaveCount(2);
    }

    [Fact]
    public async Task SendCommand_Generic_ReturnsExpectedValue()
    {
        var fake = new FakeMediator();
        fake.SetupCommand<DummyCommand, string>(c => "cmd-success");
        var cmd = new DummyCommand();

        var result = await fake.SendCommand<DummyCommand, string>(cmd);

        result.Should().Be("cmd-success");
        fake.ShouldHaveReceived<DummyCommand>();
    }

    [Fact]
    public async Task SendQuery_Generic_ReturnsExpectedValue()
    {
        var fake = new FakeMediator();
        fake.SetupQuery<DummyQuery, string>(q => "qry-success");
        var query = new DummyQuery();

        var result = await fake.SendQuery<DummyQuery, string>(query);

        result.Should().Be("qry-success");
        fake.ShouldHaveReceived<DummyQuery>();
    }

    [Fact]
    public async Task ShouldNotHaveReceived_WhenNotificationReceived_ThrowsFakeAssertionException()
    {
        var fake = new FakeMediator();
        await fake.Publish(new DummyNotification());

        var act = () => fake.ShouldNotHaveReceived<DummyNotification>();
        var ex = act.Should().Throw<FakeAssertionException>();
        ex.Which.Message.Should().Be("Expected NOT to have received 'DummyNotification', but one was received.");
    }

    [Fact]
    public async Task ReceivedRequestsOf_And_ReceivedNotificationsOf_ReturnTypedCollections()
    {
        var fake = new FakeMediator();
        fake.SetupCommand<DummyCommand, string>(c => "cmd-res");
        fake.SetupQuery<DummyQuery, string>(q => "qry-res");
        fake.SetupNotification<DummyNotification>((n, ct) => ValueTask.CompletedTask);

        var cmd1 = new DummyCommand();
        var cmd2 = new DummyCommand();
        var qry = new DummyQuery();
        var notif = new DummyNotification();

        await fake.Send(cmd1);
        await fake.Send(qry);
        await fake.Send(cmd2);
        await fake.Publish(notif);

        var commands = fake.ReceivedRequestsOf<DummyCommand>();
        var queries = fake.ReceivedRequestsOf<DummyQuery>();
        var notifications = fake.ReceivedNotificationsOf<DummyNotification>();

        commands.Should().HaveCount(2);
        commands.Should().ContainInOrder(cmd1, cmd2);

        queries.Should().HaveCount(1);
        queries.Should().Contain(qry);

        notifications.Should().HaveCount(1);
        notifications.Should().Contain(notif);
    }

    [Fact]
    public async Task Reset_ClearsReceivedRequests()
    {
        var fake = new FakeMediator();
        fake.SetupCommand<DummyCommand, string>(c => "ok");
        await fake.Send(new DummyCommand());
        fake.ReceivedRequests.Should().HaveCount(1);

        fake.Reset();

        fake.ReceivedRequests.Should().BeEmpty();
        fake.ReceivedCount<DummyCommand>().Should().Be(0);
    }

    [Fact]
    public async Task Reset_ClearsReceivedNotifications()
    {
        var fake = new FakeMediator();
        await fake.Publish(new DummyNotification());
        fake.ReceivedNotifications.Should().HaveCount(1);

        fake.Reset();

        fake.ReceivedNotifications.Should().BeEmpty();
        fake.ReceivedCount<DummyNotification>().Should().Be(0);
    }

    [Fact]
    public async Task Reset_ClearsCommandHandlers()
    {
        var fake = new FakeMediator();
        fake.SetupCommand<DummyCommand, string>(c => "ok");
        var res = await fake.Send(new DummyCommand());
        res.Should().Be("ok");

        fake.Reset();

        var act = () => fake.Send(new DummyCommand());
        var ex = act.Should().Throw<InvalidOperationException>();
        ex.Which.Message.Should().Contain("no handler for command 'DummyCommand'");
    }

    [Fact]
    public async Task Reset_ClearsQueryHandlers()
    {
        var fake = new FakeMediator();
        fake.SetupQuery<DummyQuery, string>(q => "ok");
        var res = await fake.Send(new DummyQuery());
        res.Should().Be("ok");

        fake.Reset();

        var act = () => fake.Send(new DummyQuery());
        var ex = act.Should().Throw<InvalidOperationException>();
        ex.Which.Message.Should().Contain("no handler for query 'DummyQuery'");
    }

    [Fact]
    public async Task Reset_ClearsStreamHandlers()
    {
        var fake = new FakeMediator();
        async IAsyncEnumerable<string> StreamFunc(DummyStreamRequest req, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
        {
            await Task.Yield();
            yield return "ok";
        }
        fake.SetupStream<DummyStreamRequest, string>(StreamFunc);
        var streamEnum = fake.CreateStream(new DummyStreamRequest()).GetAsyncEnumerator();
        (await streamEnum.MoveNextAsync()).Should().BeTrue();

        fake.Reset();

        var act = () => fake.CreateStream(new DummyStreamRequest());
        var ex = act.Should().Throw<InvalidOperationException>();
        ex.Which.Message.Should().Contain("no handler for stream request 'DummyStreamRequest'");
    }

    [Fact]
    public async Task Reset_ClearsNotificationHandlers()
    {
        var fake = new FakeMediator();
        var invoked = false;
        fake.SetupNotification<DummyNotification>((n, ct) => { invoked = true; return default; });
        await fake.Publish(new DummyNotification());
        invoked.Should().BeTrue();

        fake.Reset();

        invoked = false;
        await fake.Publish(new DummyNotification());
        invoked.Should().BeFalse();
    }

    [Fact]
    public void FakeAssertionException_SetsMessageCorrectly()
    {
        var ex = new FakeAssertionException("Custom error message");
        ex.Message.Should().Be("Custom error message");
    }
}
