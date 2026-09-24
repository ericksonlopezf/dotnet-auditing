// Copyright © Erickson Lopez. MIT License.
using System.Threading;
using System.Threading.Tasks;

namespace EricksonLopez.Auditing.Outbox;

/// <summary>Defines a contract for appending messages to a transactional outbox.</summary>
public interface IOutboxMessageService
{
    /// <summary>Appends an outbox message to the same transaction as the current unit of work.</summary>
    /// <param name="eventType">The type or name of the event represented by the message.</param>
    /// <param name="payload">The serialized message payload content.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    ValueTask AppendMessageAsync(string eventType, string payload, CancellationToken cancellationToken = default);
}
