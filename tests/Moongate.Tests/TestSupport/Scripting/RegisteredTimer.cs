namespace Moongate.Tests.TestSupport.Scripting;

public sealed record RegisteredTimer(
    string Id,
    string Name,
    TimeSpan Interval,
    TimeSpan? Delay,
    bool Repeat,
    Action Callback
);
