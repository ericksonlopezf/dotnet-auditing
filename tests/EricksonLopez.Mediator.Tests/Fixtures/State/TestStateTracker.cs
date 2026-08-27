// Copyright © Erickson Lopez. MIT License.
using System.Collections.Concurrent;
using System.Threading;

namespace EricksonLopez.Mediator.Tests.Fixtures;

/// <summary>
/// Thread-safe invocation and cancellation token tracking container for tests.
/// </summary>
public class TestStateTracker
{
    private readonly ConcurrentDictionary<string, int> _invocations = new();
    private readonly ConcurrentDictionary<string, CancellationToken> _tokens = new();

    public void MarkInvoked(string name) => _invocations.AddOrUpdate(name, 1, (_, count) => count + 1);
    public bool WasInvoked(string name) => _invocations.TryGetValue(name, out var count) && count > 0;
    public void SetToken(string name, CancellationToken token) => _tokens[name] = token;
    public CancellationToken GetToken(string name) => _tokens.TryGetValue(name, out var token) ? token : default;
}

/// <summary>
/// Simple tracker for single behavior execution assertions.
/// </summary>
public class BehaviorTracker
{
    public bool WasInvoked { get; private set; }

    public void MarkInvoked() => WasInvoked = true;

    public void Reset() => WasInvoked = false;
}

