// Copyright © Erickson Lopez. MIT License.
using System;
using System.Threading;
using System.Threading.Tasks;

namespace EricksonLopez.Mediator;

/// <summary>
/// Defines a mediator that combines message dispatching and notification publishing capabilities.
/// </summary>
/// <remarks>
/// This interface unifies <see cref="ISender"/> and <see cref="IPublisher"/> into a single contract.
/// </remarks>
public interface IMediator : ISender, IPublisher
{
}
