// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using AwesomeAssertions;
using EricksonLopez.Mediator;
using EricksonLopez.Mediator.RateLimiting;
using EricksonLopez.Mediator.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EricksonLopez.Mediator.RateLimiting.Tests;

public record RateLimitedPingCommand(string Message) : ICommand<string>;
public class RateLimitedPingCommandHandler : ICommandHandler<RateLimitedPingCommand, string>
{
    public ValueTask<string> Handle(RateLimitedPingCommand command, CancellationToken cancellationToken)
        => ValueTask.FromResult("Pong: " + command.Message);
}

public class RateLimitingBehaviorTests
{
    [Fact]
    public async Task Handle_WithoutRateLimiter_ExecutesNextDirectly()
    {
        var behavior = new RateLimitingBehavior<RateLimitedPingCommand, string>(rateLimiter: null);
        var command = new RateLimitedPingCommand("Hello");
        var next = new DelegateNext<string>(() => ValueTask.FromResult("Pong: " + command.Message));

        var result = await behavior.Handle(command, next, CancellationToken.None);

        result.Should().Be("Pong: Hello");
    }

    [Fact]
    public async Task Handle_WithAcquiredLease_ExecutesNextSuccessfully()
    {
        using var limiter = new ConcurrencyLimiter(new ConcurrencyLimiterOptions
        {
            PermitLimit = 1,
            QueueLimit = 0
        });

        var behavior = new RateLimitingBehavior<RateLimitedPingCommand, string>(limiter);
        var command = new RateLimitedPingCommand("Permitted");
        var next = new DelegateNext<string>(() => ValueTask.FromResult("Processed: " + command.Message));

        var result = await behavior.Handle(command, next, CancellationToken.None);

        result.Should().Be("Processed: Permitted");
    }

    [Fact]
    public async Task Handle_WhenLeaseNotAcquired_ThrowsRateLimitExceededException()
    {
        using var limiter = new ConcurrencyLimiter(new ConcurrencyLimiterOptions
        {
            PermitLimit = 1,
            QueueLimit = 0
        });

        // Exhaust the only permit
        using var initialLease = limiter.AttemptAcquire(1);
        initialLease.IsAcquired.Should().BeTrue();

        var behavior = new RateLimitingBehavior<RateLimitedPingCommand, string>(limiter);
        var command = new RateLimitedPingCommand("Blocked");
        var next = new DelegateNext<string>(() => ValueTask.FromResult("Should not run"));

        var act = async () => await behavior.Handle(command, next, CancellationToken.None).AsTask();

        var ex = await act.Should().ThrowAsync<RateLimitExceededException>();
        ex.Which.Message.Should().Contain(nameof(RateLimitedPingCommand));
        ex.Which.Message.Should().Contain("Rate limit exceeded for request of type");
        ex.Which.RetryAfter.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenLeaseNotAcquired_WithRetryAfterMetadata_PopulatesRetryAfter()
    {
        var retryAfter = TimeSpan.FromSeconds(30);
        using var customLimiter = new CustomRejectingRateLimiter(retryAfter);

        var behavior = new RateLimitingBehavior<RateLimitedPingCommand, string>(customLimiter);
        var command = new RateLimitedPingCommand("RetryTest");
        var next = new DelegateNext<string>(() => ValueTask.FromResult("Should not reach"));

        var act = async () => await behavior.Handle(command, next, CancellationToken.None).AsTask();

        var ex = await act.Should().ThrowAsync<RateLimitExceededException>();
        ex.Which.RetryAfter.Should().Be(retryAfter);
    }

    [Fact]
    public async Task Handle_WithCancellationToken_PropagatesCancellationTokenToAcquireAsync()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        using var limiter = new ConcurrencyLimiter(new ConcurrencyLimiterOptions
        {
            PermitLimit = 1,
            QueueLimit = 0
        });

        var behavior = new RateLimitingBehavior<RateLimitedPingCommand, string>(limiter);
        var command = new RateLimitedPingCommand("CancelTest");
        var next = new DelegateNext<string>(() => ValueTask.FromResult("OK"));

        var act = async () => await behavior.Handle(command, next, cts.Token).AsTask();

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Handle_WithPartitionedRateLimiter_IsolatesPermitsAcrossPartitions()
    {
        using var partitionedLimiter = PartitionedRateLimiter.Create<RateLimitedPingCommand, string>(cmd =>
        {
            var partitionKey = cmd.Message.StartsWith("TenantA") ? "TenantA" : "TenantB";
            return RateLimitPartition.GetConcurrencyLimiter(
                partitionKey,
                _ => new ConcurrencyLimiterOptions
                {
                    PermitLimit = 1,
                    QueueLimit = 0
                });
        });

        var cmdA1 = new RateLimitedPingCommand("TenantA_Msg1");
        var cmdA2 = new RateLimitedPingCommand("TenantA_Msg2");
        var cmdB1 = new RateLimitedPingCommand("TenantB_Msg1");

        // Wrap the partitioned limiter for command A
        using var limiterA = new PartitionedRateLimiterAdapter<RateLimitedPingCommand>(partitionedLimiter, cmdA1);
        var behaviorA = new RateLimitingBehavior<RateLimitedPingCommand, string>(limiterA);

        var nextA1 = new DelegateNext<string>(() => ValueTask.FromResult("Processed: " + cmdA1.Message));
        var resultA1 = await behaviorA.Handle(cmdA1, nextA1, CancellationToken.None);
        resultA1.Should().Be("Processed: TenantA_Msg1");

        // Exhaust the only permit for Tenant A
        using var leaseA1 = limiterA.AttemptAcquire(1);
        leaseA1.IsAcquired.Should().BeTrue();

        var nextA2 = new DelegateNext<string>(() => ValueTask.FromResult("Should not run"));
        var actA2 = async () => await behaviorA.Handle(cmdA2, nextA2, CancellationToken.None).AsTask();
        await actA2.Should().ThrowAsync<RateLimitExceededException>();

        // Tenant B remains permitted (isolated partition)
        using var limiterB = new PartitionedRateLimiterAdapter<RateLimitedPingCommand>(partitionedLimiter, cmdB1);
        var behaviorB = new RateLimitingBehavior<RateLimitedPingCommand, string>(limiterB);
        var nextB1 = new DelegateNext<string>(() => ValueTask.FromResult("Processed: " + cmdB1.Message));
        var resultB1 = await behaviorB.Handle(cmdB1, nextB1, CancellationToken.None);
        resultB1.Should().Be("Processed: TenantB_Msg1");
    }

    private sealed class PartitionedRateLimiterAdapter<T>(PartitionedRateLimiter<T> partitionedLimiter, T key) : RateLimiter
    {
        public override TimeSpan? IdleDuration => null;
        public override RateLimiterStatistics? GetStatistics() => partitionedLimiter.GetStatistics(key);

        protected override RateLimitLease AttemptAcquireCore(int permitCount)
            => partitionedLimiter.AttemptAcquire(key, permitCount);

        protected override ValueTask<RateLimitLease> AcquireAsyncCore(int permitCount, CancellationToken cancellationToken)
            => partitionedLimiter.AcquireAsync(key, permitCount, cancellationToken);
    }

    [Fact]
    public async Task Handle_WithServiceCollectionPipeline_DispatchesThroughRateLimiterSuccessfully()
    {
        var services = new ServiceCollection();
        using var limiter = new ConcurrencyLimiter(new ConcurrencyLimiterOptions
        {
            PermitLimit = 10,
            QueueLimit = 0
        });

        services.AddSingleton<RateLimiter>(limiter);
        services.AddMediatorRateLimiting();

        var sp = services.BuildServiceProvider();
        var behavior = sp.GetRequiredService<IPipelineBehavior<RateLimitedPingCommand, string>>();

        var command = new RateLimitedPingCommand("DI-RateLimited");
        var next = new DelegateNext<string>(() => ValueTask.FromResult("Handled: " + command.Message));

        var result = await behavior.Handle(command, next, CancellationToken.None);
        result.Should().Be("Handled: DI-RateLimited");
    }

    [Fact]
    public void AddMediatorRateLimiting_ValidServiceCollection_RegistersTransientBehavior()
    {
        var services = new ServiceCollection();
        services.AddMediatorRateLimiting();

        var descriptor = services.FirstOrDefault(s =>
            s.ServiceType == typeof(IPipelineBehavior<,>) &&
            s.ImplementationType == typeof(RateLimitingBehavior<,>));

        descriptor.Should().NotBeNull();
        descriptor!.Lifetime.Should().Be(ServiceLifetime.Transient);
    }

    [Fact]
    public void AddMediatorRateLimiting_NullServiceCollection_ThrowsArgumentNullException()
    {
        IServiceCollection services = null!;
        var act = () => services.AddMediatorRateLimiting();

        act.Should().Throw<ArgumentNullException>().WithParameterName("services");
    }

    [Fact]
    public void RateLimitExceededException_Constructors_SetProperties()
    {
        var exWithoutRetry = new RateLimitExceededException("Limit exceeded");
        exWithoutRetry.Message.Should().Be("Limit exceeded");
        exWithoutRetry.RetryAfter.Should().BeNull();

        var retry = TimeSpan.FromMinutes(2);
        var exWithRetry = new RateLimitExceededException("Limit exceeded with retry", retry);
        exWithRetry.Message.Should().Be("Limit exceeded with retry");
        exWithRetry.RetryAfter.Should().Be(retry);
    }

    [Fact]
    public async Task Handle_WithAcquiredLease_DisposesLeaseAfterSuccessfulExecution()
    {
        using var limiter = new TrackingRateLimiter(isAcquired: true);
        var behavior = new RateLimitingBehavior<RateLimitedPingCommand, string>(limiter);
        var command = new RateLimitedPingCommand("DisposeSuccess");
        var next = new DelegateNext<string>(() => ValueTask.FromResult("OK"));

        var result = await behavior.Handle(command, next, CancellationToken.None);

        result.Should().Be("OK");
        limiter.LastLease.Should().NotBeNull();
        limiter.LastLease!.DisposeCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WhenHandlerThrowsException_DisposesLeaseInFinally()
    {
        using var limiter = new TrackingRateLimiter(isAcquired: true);
        var behavior = new RateLimitingBehavior<RateLimitedPingCommand, string>(limiter);
        var command = new RateLimitedPingCommand("DisposeOnError");
        var next = new DelegateNext<string>(() => throw new InvalidOperationException("Handler error"));

        var act = async () => await behavior.Handle(command, next, CancellationToken.None).AsTask();

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Handler error");
        limiter.LastLease.Should().NotBeNull();
        limiter.LastLease!.DisposeCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WhenLeaseNotAcquired_DisposesRejectedLease()
    {
        using var limiter = new TrackingRateLimiter(isAcquired: false);
        var behavior = new RateLimitingBehavior<RateLimitedPingCommand, string>(limiter);
        var command = new RateLimitedPingCommand("RejectDispose");
        var next = new DelegateNext<string>(() => ValueTask.FromResult("Should not run"));

        var act = async () => await behavior.Handle(command, next, CancellationToken.None).AsTask();

        await act.Should().ThrowAsync<RateLimitExceededException>();
        limiter.LastLease.Should().NotBeNull();
        limiter.LastLease!.DisposeCount.Should().Be(1);
    }

    private sealed class TrackingRateLimiter : RateLimiter
    {
        private readonly bool _isAcquired;
        private readonly TimeSpan? _retryAfter;

        public TrackingRateLimitLease? LastLease { get; private set; }

        public TrackingRateLimiter(bool isAcquired = true, TimeSpan? retryAfter = null)
        {
            _isAcquired = isAcquired;
            _retryAfter = retryAfter;
        }

        public override TimeSpan? IdleDuration => null;
        public override RateLimiterStatistics? GetStatistics() => null;

        protected override RateLimitLease AttemptAcquireCore(int permitCount)
        {
            var lease = new TrackingRateLimitLease(_isAcquired, _retryAfter);
            LastLease = lease;
            return lease;
        }

        protected override ValueTask<RateLimitLease> AcquireAsyncCore(int permitCount, CancellationToken cancellationToken)
        {
            var lease = new TrackingRateLimitLease(_isAcquired, _retryAfter);
            LastLease = lease;
            return new ValueTask<RateLimitLease>(lease);
        }
    }

    private sealed class TrackingRateLimitLease : RateLimitLease
    {
        private readonly bool _isAcquired;
        private readonly TimeSpan? _retryAfter;
        private int _disposeCount;

        public TrackingRateLimitLease(bool isAcquired, TimeSpan? retryAfter = null)
        {
            _isAcquired = isAcquired;
            _retryAfter = retryAfter;
        }

        public override bool IsAcquired => _isAcquired;

        public int DisposeCount => _disposeCount;

        public override IEnumerable<string> MetadataNames
        {
            get
            {
                if (_retryAfter.HasValue)
                    yield return MetadataName.RetryAfter.Name;
            }
        }

        public override bool TryGetMetadata(string metadataName, out object? metadata)
        {
            if (metadataName == MetadataName.RetryAfter.Name && _retryAfter.HasValue)
            {
                metadata = _retryAfter.Value;
                return true;
            }

            metadata = null;
            return false;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Interlocked.Increment(ref _disposeCount);
            }
            base.Dispose(disposing);
        }
    }

    private sealed class CustomRejectingRateLimiter : RateLimiter
    {
        private readonly TimeSpan _retryAfter;

        public CustomRejectingRateLimiter(TimeSpan retryAfter)
        {
            _retryAfter = retryAfter;
        }

        public override TimeSpan? IdleDuration => null;
        public override RateLimiterStatistics? GetStatistics() => null;

        protected override RateLimitLease AttemptAcquireCore(int permitCount)
            => new CustomRejectingLease(_retryAfter);

        protected override ValueTask<RateLimitLease> AcquireAsyncCore(int permitCount, CancellationToken cancellationToken)
            => new(new CustomRejectingLease(_retryAfter));
    }

    private sealed class CustomRejectingLease : RateLimitLease
    {
        private readonly TimeSpan _retryAfter;

        public CustomRejectingLease(TimeSpan retryAfter)
        {
            _retryAfter = retryAfter;
        }

        public override bool IsAcquired => false;

        public override IEnumerable<string> MetadataNames
        {
            get { yield return MetadataName.RetryAfter.Name; }
        }

        public override bool TryGetMetadata(string metadataName, out object? metadata)
        {
            if (metadataName == MetadataName.RetryAfter.Name)
            {
                metadata = _retryAfter;
                return true;
            }

            metadata = null;
            return false;
        }
    }
}
