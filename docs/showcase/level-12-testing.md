# Level 12: Testing & Test Doubles (`FakeMediator`)

Testing in `EricksonLopez.Mediator` is streamlined through dedicated test doubles in `EricksonLopez.Mediator.Testing`.

---

## 1. Unit Testing Handlers Directly

Because handlers implement `ICommandHandler<TCommand, TResponse>` or `IQueryHandler<TQuery, TResponse>`, you can instantiate them directly in unit tests without involving the mediator or service provider:

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using NSubstitute;
using Xunit;

public class CreateUserCommandHandlerTests
{
    [Fact]
    public async Task Handle_GivenValidCommand_ShouldPersistUserAndReturnId()
    {
        // Arrange
        var repo = Substitute.For<IUserRepository>();
        var uow = Substitute.For<IUnitOfWork>();
        var handler = new CreateUserCommandHandler(repo, uow);
        
        var command = new CreateUserCommand("alice", "alice@example.com");

        // Act
        var resultId = await handler.Handle(command, CancellationToken.None);

        // Assert
        resultId.Should().NotBeEmpty();
        await repo.Received(1).AddAsync(Arg.Is<User>(u => u.Username == "alice"), Arg.Any<CancellationToken>());
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
```

---

## 2. Using `FakeMediator` for Controller / Endpoint Testing

The `EricksonLopez.Mediator.Testing` package provides `FakeMediator` — a high-performance in-memory test double for testing caller components without reflection or complex mocking setups:

```csharp
using System;
using System.Threading.Tasks;
using AwesomeAssertions;
using EricksonLopez.Mediator.Testing;
using Xunit;

public class UserControllerTests
{
    [Fact]
    public async Task Post_GivenValidRequest_ShouldDispatchCommandAndReturnCreated()
    {
        // Arrange
        var fakeMediator = new FakeMediator();
        var expectedId = Guid.NewGuid();
        
        // Configure stub response
        fakeMediator.SetupCommand<CreateUserCommand, Guid>(expectedId);

        var controller = new UserController(fakeMediator);
        var requestDto = new CreateUserRequestDto("alice", "alice@example.com");

        // Act
        var result = await controller.Post(requestDto);

        // Assert
        result.Should().BeOfType<CreatedResult>();
        
        // Assert dispatched requests using FakeMediator fluent verification:
        fakeMediator.ShouldHaveReceived<CreateUserCommand>(cmd => cmd.Username == "alice");
    }
}
```

---

## 3. Testing Custom Pipeline Behaviors with `DelegateNext`

Test pipeline behaviors in total isolation using `DelegateNext<TResponse>`:

```csharp
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using EricksonLopez.Mediator.Testing;
using Xunit;

public class LoggingBehaviorTests
{
    [Fact]
    public async Task Handle_ShouldWrapInvocationAndReturnInnerResult()
    {
        // Arrange
        var logger = Substitute.For<ILogger<LoggingBehavior<PingCommand, string>>>();
        var behavior = new LoggingBehavior<PingCommand, string>(logger);
        var command = new PingCommand();
        var next = new DelegateNext<string>(() => ValueTask.FromResult("PONG"));

        // Act
        var result = await behavior.Handle(command, next, CancellationToken.None);

        // Assert
        result.Should().Be("PONG");
    }
}
```
