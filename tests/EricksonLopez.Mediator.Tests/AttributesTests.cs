// Copyright © Erickson Lopez. MIT License.
using System;
using System.Threading;
using System.Threading.Tasks;
using AutoFixture.Xunit2;
using AwesomeAssertions;
using EricksonLopez.Mediator;
using Xunit;

namespace EricksonLopez.Mediator.Tests;

public class AttributesTests
{
    [Theory, AutoData]
    public void Constructor_UseGlobalBehaviorWithCustomOrder_SetsBehaviorTypeAndOrder(Type behaviorType, int order)
    {
        var attribute = new UseGlobalBehaviorAttribute(behaviorType, order);
        attribute.BehaviorType.Should().Be(behaviorType);
        attribute.Order.Should().Be(order);
    }

    [Theory, AutoData]
    public void Constructor_UseGlobalBehaviorWithDefaultOrder_SetsOrderZero(Type behaviorType)
    {
        var attribute = new UseGlobalBehaviorAttribute(behaviorType);
        attribute.Order.Should().Be(0);
    }

    [Theory, AutoData]
    public void Constructor_UseBehaviorWithCustomOrder_SetsBehaviorTypeAndOrder(Type behaviorType, int order)
    {
        var attribute = new UseBehaviorAttribute(behaviorType, order);
        attribute.BehaviorType.Should().Be(behaviorType);
        attribute.Order.Should().Be(order);
    }

    [Theory, AutoData]
    public void Constructor_UseBehaviorWithDefaultOrder_SetsOrderZero(Type behaviorType)
    {
        var attribute = new UseBehaviorAttribute(behaviorType);
        attribute.Order.Should().Be(0);
    }

    [Theory]
    [InlineData(PublishStrategy.Sequential)]
    [InlineData(PublishStrategy.Parallel)]
    [InlineData(PublishStrategy.SequentialAggregateExceptions)]
    public void Constructor_PublishStrategyAttribute_SetsStrategy(PublishStrategy strategy)
    {
        var attribute = new PublishStrategyAttribute(strategy);
        attribute.Strategy.Should().Be(strategy);
    }

    [Theory]
    [InlineData(HandlerLifetime.Singleton)]
    [InlineData(HandlerLifetime.Scoped)]
    [InlineData(HandlerLifetime.Transient)]
    public void Constructor_ServiceLifetimeAttribute_SetsLifetime(HandlerLifetime lifetime)
    {
        var attribute = new ServiceLifetimeAttribute(lifetime);
        attribute.Lifetime.Should().Be(lifetime);
    }

    [Theory, AutoData]
    public void Constructor_DiscoverHandlersAttribute_SetsAssemblyMarkerType(Type markerType)
    {
        var attribute = new DiscoverHandlersAttribute(markerType);
        attribute.AssemblyMarkerType.Should().Be(markerType);
    }

    [Fact]
    public void Constructor_ValidateRequestAttribute_InstantiatesSuccessfully()
    {
        var attribute = new ValidateRequestAttribute();
        attribute.Should().NotBeNull();

        var usage = (AttributeUsageAttribute?)Attribute.GetCustomAttribute(typeof(ValidateRequestAttribute), typeof(AttributeUsageAttribute));
        usage.Should().NotBeNull();
        usage!.ValidOn.Should().Be(AttributeTargets.Class | AttributeTargets.Struct);
        usage.Inherited.Should().BeTrue();
    }

    [Theory, AutoData]
    public void Property_ValidateNotNullErrorMessage_SetsAndGetsExpectedValue(string errorMessage)
    {
        var attrDefault = new ValidateNotNullAttribute();
        attrDefault.ErrorMessage.Should().BeNull();

        var attrCustom = new ValidateNotNullAttribute(errorMessage);
        attrCustom.ErrorMessage.Should().Be(errorMessage);

        attrCustom.ErrorMessage = "Updated";
        attrCustom.ErrorMessage.Should().Be("Updated");
    }

    [Theory, AutoData]
    public void Property_ValidateNotEmptyErrorMessage_SetsAndGetsExpectedValue(string errorMessage)
    {
        var attrDefault = new ValidateNotEmptyAttribute();
        attrDefault.ErrorMessage.Should().BeNull();

        var attrCustom = new ValidateNotEmptyAttribute(errorMessage);
        attrCustom.ErrorMessage.Should().Be(errorMessage);

        attrCustom.ErrorMessage = "Updated";
        attrCustom.ErrorMessage.Should().Be("Updated");
    }

    [Theory, AutoData]
    public void Constructor_ValidateRangeAttribute_SetsPropertiesCorrectly(double min, double max, string errorMessage)
    {
        var attrDefault = new ValidateRangeAttribute(min, max);
        attrDefault.Minimum.Should().Be(min);
        attrDefault.Maximum.Should().Be(max);
        attrDefault.ErrorMessage.Should().BeNull();

        var attrCustom = new ValidateRangeAttribute(min, max, errorMessage);
        attrCustom.Minimum.Should().Be(min);
        attrCustom.Maximum.Should().Be(max);
        attrCustom.ErrorMessage.Should().Be(errorMessage);

        attrCustom.ErrorMessage = "Range updated";
        attrCustom.ErrorMessage.Should().Be("Range updated");
    }

    [Theory, AutoData]
    public void Constructor_ValidateLengthAttribute_SetsPropertiesCorrectly(int min, int max, string errorMessage)
    {
        var attrDefault = new ValidateLengthAttribute(min, max);
        attrDefault.MinimumLength.Should().Be(min);
        attrDefault.MaximumLength.Should().Be(max);
        attrDefault.ErrorMessage.Should().BeNull();

        var attrCustom = new ValidateLengthAttribute(min, max, errorMessage);
        attrCustom.MinimumLength.Should().Be(min);
        attrCustom.MaximumLength.Should().Be(max);
        attrCustom.ErrorMessage.Should().Be(errorMessage);

        attrCustom.ErrorMessage = "Length updated";
        attrCustom.ErrorMessage.Should().Be("Length updated");
    }

    [Theory, AutoData]
    public void Constructor_ValidateRegexAttribute_SetsPropertiesCorrectly(string pattern, string errorMessage)
    {
        var attrDefault = new ValidateRegexAttribute(pattern);
        attrDefault.Pattern.Should().Be(pattern);
        attrDefault.ErrorMessage.Should().BeNull();

        var attrCustom = new ValidateRegexAttribute(pattern, errorMessage);
        attrCustom.Pattern.Should().Be(pattern);
        attrCustom.ErrorMessage.Should().Be(errorMessage);

        attrCustom.ErrorMessage = "Regex updated";
        attrCustom.ErrorMessage.Should().Be("Regex updated");
    }
}



