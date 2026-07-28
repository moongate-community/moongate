using Moongate.Core.Primitives;
using Moongate.Core.Types;
using Moongate.Server.Abstractions.Data.AI;
using Moongate.Server.Abstractions.Interfaces.AI;

namespace Moongate.Tests.Support;

/// <summary>No-op AI action service for scheduler tests, where brain effects are simulated by the runtime double.</summary>
public sealed class StubAiActionService : IAiActionService
{
    private sealed class NoopScope : IDisposable
    {
        public void Dispose() { }
    }

    public IDisposable Begin(BrainContext context)
        => new NoopScope();

    public bool ClearTarget()
        => true;

    public bool Engage(Serial targetId)
        => true;

    public bool MoveAway(Serial targetId)
        => true;

    public bool MoveTo(int x, int y)
        => true;

    public bool MoveToward(Serial targetId)
        => true;

    public bool Patrol()
        => true;

    public bool ReturnHome()
        => true;

    public bool Say(string text)
        => true;

    public bool Step(DirectionType direction)
        => true;
}
