// Copyright © Erickson Lopez. MIT License.
using System;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using EricksonLopez.Mediator;
using EricksonLopez.Mediator.Testing;
using Xunit;

namespace EricksonLopez.Mediator.Tests;

public record GenericCommand(string Text) : ICommand<string>;
public class GenericCommandHandler : ICommandHandler<GenericCommand, string>
{
    public ValueTask<string> Handle(GenericCommand command, CancellationToken cancellationToken)
        => ValueTask.FromResult("Generic: " + command.Text);
}

public record GenericQuery(int Number) : IQuery<int>;
public class GenericQueryHandler : IQueryHandler<GenericQuery, int>
{
    public ValueTask<int> Handle(GenericQuery query, CancellationToken cancellationToken)
        => ValueTask.FromResult(query.Number);
}

public class GenericStaticDispatchTests
{
    [Fact]
    public async Task SendCommand_TypedCommand_DispatchesSuccessfully()
    {
        var fake = new FakeMediator();
        fake.SetupCommand<GenericCommand, string>(cmd => "Echo: " + cmd.Text);

        var result = await fake.SendCommand<GenericCommand, string>(new GenericCommand("Testing"), CancellationToken.None);

        result.Should().Be("Echo: Testing");
        fake.ShouldHaveReceived<GenericCommand>();
    }

    [Fact]
    public async Task SendQuery_TypedQuery_DispatchesSuccessfully()
    {
        var fake = new FakeMediator();
        fake.SetupQuery<GenericQuery, int>(q => q.Number * 2);

        var result = await fake.SendQuery<GenericQuery, int>(new GenericQuery(21), CancellationToken.None);

        result.Should().Be(42);
        fake.ShouldHaveReceived<GenericQuery>();
    }
}




