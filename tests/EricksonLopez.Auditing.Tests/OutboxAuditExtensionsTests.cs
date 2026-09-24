// Copyright © Erickson Lopez. MIT License.
using System;
using System.Linq;
using AwesomeAssertions;
using EricksonLopez.Auditing;
using EricksonLopez.Auditing.Outbox;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace EricksonLopez.Auditing.Tests;

public sealed class OutboxAuditExtensionsTests
{
    [Fact]
    public void UseOutbox_NullBuilder_ThrowsArgumentNullException()
    {
        Action act = () => OutboxAuditExtensions.UseOutbox(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("builder");
    }

    [Fact]
    public void UseOutbox_ValidBuilder_RegistersOutboxAuditStoreAsSingleton()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = Substitute.For<IAuditBuilder>();
        builder.Services.Returns(services);

        // Act
        var result = builder.UseOutbox();

        // Assert
        result.Should().BeSameAs(builder);
        var descriptor = services.FirstOrDefault(s => s.ServiceType == typeof(IAuditStore));
        descriptor.Should().NotBeNull();
        descriptor!.ImplementationType.Should().Be<OutboxAuditStore>();
        descriptor.Lifetime.Should().Be(ServiceLifetime.Singleton);
    }
}
