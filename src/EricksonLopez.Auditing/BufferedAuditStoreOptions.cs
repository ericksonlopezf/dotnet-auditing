// Copyright © Erickson Lopez. MIT License.
using System;
using System.Threading.Channels;

namespace EricksonLopez.Auditing;

/// <summary>
/// Represents configuration options for <see cref="BufferedAuditStoreDecorator"/>.
/// </summary>
public sealed class BufferedAuditStoreOptions
{
    private int _capacity = 10_000;
    private int _batchSize = 100;
    private TimeSpan _flushInterval = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// Gets or sets the maximum number of audit records buffered in memory before applying backpressure.
    /// </summary>
    /// <remarks>Defaults to 10,000.</remarks>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is less than or equal to zero</exception>
    public int Capacity
    {
        get => _capacity;
        set
        {
            if (value <= 0)
                throw new ArgumentOutOfRangeException(nameof(value), "Capacity must be greater than zero.");
            _capacity = value;
        }
    }

    /// <summary>
    /// Gets or sets the maximum number of audit records dispatched in a single batch to the underlying store.
    /// </summary>
    /// <remarks>Defaults to 100.</remarks>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is less than or equal to zero</exception>
    public int BatchSize
    {
        get => _batchSize;
        set
        {
            if (value <= 0)
                throw new ArgumentOutOfRangeException(nameof(value), "BatchSize must be greater than zero.");
            _batchSize = value;
        }
    }

    /// <summary>
    /// Gets or sets the maximum duration to wait before flushing accumulated records even if <see cref="BatchSize"/> has not been reached.
    /// </summary>
    /// <remarks>Defaults to 500 milliseconds.</remarks>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is less than or equal to <see cref="TimeSpan.Zero"/></exception>
    public TimeSpan FlushInterval
    {
        get => _flushInterval;
        set
        {
            if (value <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(value), "FlushInterval must be greater than zero.");
            _flushInterval = value;
        }
    }

    /// <summary>
    /// Gets or sets the behavior when writing to a full buffer.
    /// </summary>
    /// <remarks>Defaults to <see cref="BoundedChannelFullMode.Wait"/>.</remarks>
    public BoundedChannelFullMode FullMode { get; set; } = BoundedChannelFullMode.Wait;

    private int _workerCount = 1;

    /// <summary>
    /// Gets or sets the number of concurrent background workers draining the buffer.
    /// </summary>
    /// <remarks>
    /// Defaults to 1. When greater than 1, records are drained and dispatched concurrently to avoid head-of-line blocking.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is less than or equal to zero</exception>
    public int WorkerCount
    {
        get => _workerCount;
        set
        {
            if (value <= 0)
                throw new ArgumentOutOfRangeException(nameof(value), "WorkerCount must be greater than zero.");
            _workerCount = value;
        }
    }
}
