// Copyright © Erickson Lopez. MIT License.
using System;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using EricksonLopez.Mediator.OpenTelemetry;
using EricksonLopez.Mediator.Testing;
using Xunit;

namespace EricksonLopez.Mediator.OpenTelemetry.Tests;

public class OpenTelemetryBehaviorTests
{
    public sealed record DummyRequest : ICommand<string>;

    public class DummyRequestHandler : ICommandHandler<DummyRequest, string>
    {
        public ValueTask<string> Handle(DummyRequest command, CancellationToken cancellationToken) => default;
    }

    [Fact]
    public async Task Handle_Success_RecordsMetricsAndActivity()
    {
        long requestCount = 0;
        int durationCount = 0;

        using var meterListener = MeterCapture.Start(
            longCallback: (instrument, measurement, tags, state) =>
            {
                if (instrument.Name == "mediator.requests.total" && instrument.Meter.Name == "EricksonLopez.Mediator" && instrument.Meter.Version == "1.0.0" && instrument.Unit == "requests" && instrument.Description == "Total number of requests dispatched via IMediator.Send.")
                {
                    requestCount += measurement;
                    tags.ToArray().Should().Contain(t => t.Key == "request.type" && (string)t.Value! == "DummyRequest");
                }
            },
            doubleCallback: (instrument, measurement, tags, state) =>
            {
                if (instrument.Name == "mediator.request.duration" && instrument.Meter.Name == "EricksonLopez.Mediator" && instrument.Unit == "ms" && instrument.Description == "Duration of each request dispatched via IMediator.Send.")
                {
                    durationCount++;
                    tags.ToArray().Should().Contain(t => t.Key == "request.type" && (string)t.Value! == "DummyRequest");
                }
            });

        using var capture = ActivityCapture.Start();

        var behavior = new OpenTelemetryBehavior<DummyRequest, string>();

        // Act
        var result = await behavior.Handle(new DummyRequest(), new DelegateNext<string>("success"), CancellationToken.None);

        // Assert
        result.Should().Be("success");
        requestCount.Should().Be(1);
        durationCount.Should().Be(1);

        capture.Activity.Should().NotBeNull();
        capture.Activity!.DisplayName.Should().Be("Mediator DummyRequest");
        capture.Activity.Kind.Should().Be(ActivityKind.Internal);
        capture.Activity.Status.Should().Be(ActivityStatusCode.Ok);
        capture.Activity.GetTagItem("mediator.request.name").Should().Be("DummyRequest");
        capture.Activity.GetTagItem("mediator.request.type").Should().Be(typeof(DummyRequest).FullName);
        capture.Activity.GetTagItem("mediator.response.type").Should().Be(typeof(string).FullName);
    }

    [Fact]
    public async Task Handle_Failure_RecordsFailureMetricsAndErrorActivity()
    {
        long failureCount = 0;
        using var meterListener = MeterCapture.Start(
            longCallback: (instrument, measurement, tags, state) =>
            {
                if (instrument.Name == "mediator.requests.failures" && instrument.Meter.Name == "EricksonLopez.Mediator" && instrument.Unit == "requests" && instrument.Description == "Total number of requests that resulted in an unhandled exception.")
                {
                    failureCount += measurement;
                    tags.ToArray().Should().Contain(t => t.Key == "request.type" && (string)t.Value! == "DummyRequest");
                }
            });

        using var capture = ActivityCapture.Start();

        var behavior = new OpenTelemetryBehavior<DummyRequest, string>();

        // Act
        var action = async () => await behavior.Handle(
            new DummyRequest(),
            new DelegateNext<string>(() => throw new InvalidOperationException("forced-error")),
            CancellationToken.None);

        // Assert
        var ex = await action.Should().ThrowAsync<InvalidOperationException>();
        ex.WithMessage("forced-error");

        failureCount.Should().Be(1);

        capture.Activity.Should().NotBeNull();
        capture.Activity!.Status.Should().Be(ActivityStatusCode.Error);
        capture.Activity.StatusDescription.Should().Be("forced-error");
        capture.Activity.Events.Should().Contain(e => e.Name == "exception");
    }

    [Fact]
    public async Task Handle_CancelledOperation_RecordsFailureMetricAndErrorActivity()
    {
        long failureCount = 0;
        using var meterListener = MeterCapture.Start(
            longCallback: (instrument, measurement, tags, state) =>
            {
                if (instrument.Name == "mediator.requests.failures" && instrument.Meter.Name == "EricksonLopez.Mediator")
                {
                    failureCount += measurement;
                    tags.ToArray().Should().Contain(t => t.Key == "request.type" && (string)t.Value! == "DummyRequest");
                }
            });

        using var capture = ActivityCapture.Start();

        var behavior = new OpenTelemetryBehavior<DummyRequest, string>();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var action = async () => await behavior.Handle(
            new DummyRequest(),
            new DelegateNext<string>(() => throw new OperationCanceledException(cts.Token)),
            cts.Token);

        // Assert
        var ex = await action.Should().ThrowAsync<OperationCanceledException>();

        failureCount.Should().Be(1);

        capture.Activity.Should().NotBeNull();
        capture.Activity!.Status.Should().Be(ActivityStatusCode.Error);
        capture.Activity.Events.Should().Contain(e => e.Name == "exception");
    }

    [Fact]
    public void RecordNotification_WithNotificationType_RecordsMetrics()
    {
        long notificationCount = 0;
        using var meterListener = MeterCapture.Start(
            longCallback: (instrument, measurement, tags, state) =>
            {
                if (instrument.Name == "mediator.notifications.total" && instrument.Meter.Name == "EricksonLopez.Mediator" && instrument.Unit == "notifications" && instrument.Description == "Total number of notifications dispatched via IMediator.Publish.")
                {
                    notificationCount += measurement;
                    tags.ToArray().Should().Contain(t => t.Key == "notification.type" && (string)t.Value! == "DummyNotification");
                }
            });

        // Act
        MediatorMetrics.RecordNotification("DummyNotification");

        // Assert
        notificationCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ActivitySourceInactive_ExecutesNextWithoutError()
    {
        // No ActivityListener added, so activity will be null.
        var behavior = new OpenTelemetryBehavior<DummyRequest, string>();

        // Act
        var result = await behavior.Handle(new DummyRequest(), new DelegateNext<string>("success"), CancellationToken.None);

        // Assert
        result.Should().Be("success");
    }

    [Fact]
    public async Task Handle_FailureWithActivitySourceInactive_PropagatesExceptionWithoutError()
    {
        // No ActivityListener added, so activity will be null.
        var behavior = new OpenTelemetryBehavior<DummyRequest, string>();

        // Act
        var action = async () => await behavior.Handle(
            new DummyRequest(),
            new DelegateNext<string>(() => throw new InvalidOperationException("forced-error")),
            CancellationToken.None);

        // Assert
        var ex = await action.Should().ThrowAsync<InvalidOperationException>();
        ex.WithMessage("forced-error");
    }

    [Fact]
    public async Task Handle_CustomOptions_UsesCustomSourceAndEnrichesActivity()
    {
        // Arrange
        using var capture = ActivityCapture.Start("Custom.Service.Mediator");

        var enrichCalled = false;
        DummyRequest? receivedRequest = null;
        Activity? receivedActivity = null;

        var options = new MediatorOpenTelemetryOptions
        {
            ActivitySourceName = "Custom.Service.Mediator",
            EnrichActivity = (activity, request) =>
            {
                enrichCalled = true;
                receivedActivity = activity;
                receivedRequest = request as DummyRequest;
                activity.SetTag("custom.tenant_id", "tenant-123");
            }
        };

        var behavior = new OpenTelemetryBehavior<DummyRequest, string>(options);
        var expectedRequest = new DummyRequest();

        // Act
        var result = await behavior.Handle(expectedRequest, new DelegateNext<string>("custom-success"), CancellationToken.None);

        // Assert
        result.Should().Be("custom-success");
        enrichCalled.Should().BeTrue();
        receivedRequest.Should().BeSameAs(expectedRequest);
        receivedActivity.Should().NotBeNull();
        capture.Activity.Should().NotBeNull();
        capture.Activity!.Source.Name.Should().Be("Custom.Service.Mediator");
        capture.Activity.GetTagItem("custom.tenant_id").Should().Be("tenant-123");
    }

    [Fact]
    public void MediatorOpenTelemetryOptions_DefaultValues_AreCorrect()
    {
        var options = new MediatorOpenTelemetryOptions();
        options.ActivitySourceName.Should().Be("EricksonLopez.Mediator");
        options.EnrichActivity.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WithOptionsDefaultActivitySourceName_ReusesDefaultActivitySource()
    {
        using var capture = ActivityCapture.Start("EricksonLopez.Mediator");

        var options = new MediatorOpenTelemetryOptions { ActivitySourceName = "EricksonLopez.Mediator" };
        var behavior = new OpenTelemetryBehavior<DummyRequest, string>(options);

        var result = await behavior.Handle(new DummyRequest(), new DelegateNext<string>("default-source"), CancellationToken.None);

        result.Should().Be("default-source");
        capture.Activity.Should().NotBeNull();
        capture.Activity!.Source.Should().BeSameAs(OpenTelemetryBehavior<DummyRequest, string>.DefaultActivitySource);
    }

    [Fact]
    public async Task Handle_WithEmptyActivitySourceName_FallsBackToDefaultActivitySource()
    {
        using var capture = ActivityCapture.Start("EricksonLopez.Mediator");

        var options = new MediatorOpenTelemetryOptions { ActivitySourceName = "" };
        var behavior = new OpenTelemetryBehavior<DummyRequest, string>(options);

        var result = await behavior.Handle(new DummyRequest(), new DelegateNext<string>("empty-fallback"), CancellationToken.None);

        result.Should().Be("empty-fallback");
        capture.Activity.Should().NotBeNull();
        capture.Activity!.Source.Should().BeSameAs(OpenTelemetryBehavior<DummyRequest, string>.DefaultActivitySource);
    }

    [Fact]
    public async Task Handle_WithExplicitNullOptions_FallsBackToDefaultActivitySource()
    {
        using var capture = ActivityCapture.Start("EricksonLopez.Mediator");

        var behavior = new OpenTelemetryBehavior<DummyRequest, string>(options: null);

        var result = await behavior.Handle(new DummyRequest(), new DelegateNext<string>("null-options"), CancellationToken.None);

        result.Should().Be("null-options");
        capture.Activity.Should().NotBeNull();
        capture.Activity!.Source.Should().BeSameAs(OpenTelemetryBehavior<DummyRequest, string>.DefaultActivitySource);
    }

    [Fact]
    public async Task Handle_WithNullActivitySourceName_FallsBackToDefaultActivitySource()
    {
        using var capture = ActivityCapture.Start("EricksonLopez.Mediator");

        var options = new MediatorOpenTelemetryOptions { ActivitySourceName = null! };
        var behavior = new OpenTelemetryBehavior<DummyRequest, string>(options);

        var result = await behavior.Handle(new DummyRequest(), new DelegateNext<string>("null-name"), CancellationToken.None);

        result.Should().Be("null-name");
        capture.Activity.Should().NotBeNull();
        capture.Activity!.Source.Should().BeSameAs(OpenTelemetryBehavior<DummyRequest, string>.DefaultActivitySource);
    }

    [Fact]
    public async Task Handle_WithEnrichActivity_WhenRequestIsNull_DoesNotInvokeEnricher()
    {
        using var capture = ActivityCapture.Start("EricksonLopez.Mediator");

        var enrichCalled = false;
        var options = new MediatorOpenTelemetryOptions
        {
            EnrichActivity = (activity, request) => enrichCalled = true
        };

        var behavior = new OpenTelemetryBehavior<DummyRequest?, string>(options);

        var result = await behavior.Handle(null, new DelegateNext<string>("null-request"), CancellationToken.None);

        result.Should().Be("null-request");
        enrichCalled.Should().BeFalse();
        capture.Activity.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_WithEnrichActivity_WhenActivityIsNull_DoesNotInvokeEnricher()
    {
        // No listener active
        var enrichCalled = false;
        var options = new MediatorOpenTelemetryOptions
        {
            EnrichActivity = (activity, request) => enrichCalled = true
        };

        var behavior = new OpenTelemetryBehavior<DummyRequest, string>(options);

        var result = await behavior.Handle(new DummyRequest(), new DelegateNext<string>("no-listener"), CancellationToken.None);

        result.Should().Be("no-listener");
        enrichCalled.Should().BeFalse();
    }

    [Fact]
    public void AddMediatorOpenTelemetry_NullServices_ThrowsArgumentNullException()
    {
        Microsoft.Extensions.DependencyInjection.IServiceCollection services = null!;
        var act = () => services.AddMediatorOpenTelemetry();
        act.Should().Throw<ArgumentNullException>().WithParameterName("services");
    }

    [Fact]
    public void AddMediatorOpenTelemetry_WithoutConfigure_RegistersDefaultOptions()
    {
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        services.AddMediatorOpenTelemetry();

        var descriptor = services.FirstOrDefault(s => s.ServiceType == typeof(MediatorOpenTelemetryOptions));
        descriptor.Should().NotBeNull();
        var options = descriptor!.ImplementationInstance as MediatorOpenTelemetryOptions;
        options.Should().NotBeNull();
        options!.ActivitySourceName.Should().Be("EricksonLopez.Mediator");
    }

    [Fact]
    public void AddMediatorOpenTelemetry_WithConfigure_RegistersConfiguredOptions()
    {
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        services.AddMediatorOpenTelemetry(opt =>
        {
            opt.ActivitySourceName = "Custom.Service.Mediator";
        });

        var descriptor = services.FirstOrDefault(s => s.ServiceType == typeof(MediatorOpenTelemetryOptions));
        descriptor.Should().NotBeNull();
        var options = descriptor!.ImplementationInstance as MediatorOpenTelemetryOptions;
        options.Should().NotBeNull();
        options!.ActivitySourceName.Should().Be("Custom.Service.Mediator");
    }

    private sealed class ActivityCapture : IDisposable
    {
        private readonly ActivityListener _listener;
        public Activity? Activity { get; private set; }

        private ActivityCapture(string sourceName)
        {
            _listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == sourceName,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
                ActivityStopped = activity => Activity = activity
            };
            ActivitySource.AddActivityListener(_listener);
        }

        public static ActivityCapture Start(string sourceName = "EricksonLopez.Mediator") => new(sourceName);

        public void Dispose() => _listener.Dispose();
    }

    private delegate void MeterMeasurementCallback<T>(Instrument instrument, T measurement, ReadOnlySpan<System.Collections.Generic.KeyValuePair<string, object?>> tags, object? state);

    private sealed class MeterCapture : IDisposable
    {
        private readonly MeterListener _listener;
        private MeterMeasurementCallback<long>? _longCallback;
        private MeterMeasurementCallback<double>? _doubleCallback;

        private MeterCapture()
        {
            _listener = new MeterListener();
            _listener.InstrumentPublished = (instrument, listener) => listener.EnableMeasurementEvents(instrument);
            _listener.SetMeasurementEventCallback<long>((i, m, t, s) => _longCallback?.Invoke(i, m, t, s));
            _listener.SetMeasurementEventCallback<double>((i, m, t, s) => _doubleCallback?.Invoke(i, m, t, s));
        }

        public static MeterCapture Start(
            MeterMeasurementCallback<long>? longCallback = null,
            MeterMeasurementCallback<double>? doubleCallback = null)
        {
            var capture = new MeterCapture
            {
                _longCallback = longCallback,
                _doubleCallback = doubleCallback
            };
            capture._listener.Start();
            return capture;
        }

        public void Dispose() => _listener.Dispose();
    }
}
