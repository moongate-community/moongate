using Moongate.Core.Primitives;
using Moongate.Server.Abstractions.Data.World;
using Moongate.Server.Abstractions.Interfaces.World;

namespace Moongate.Tests.Support;

/// <summary>
/// Answers every serial with one property entry carrying a fixed cliloc and argument — enough for a
/// route test, which is about who may read a tooltip rather than about what it says.
/// </summary>
public sealed class StubOplService : IOplService
{
    private readonly int _cliloc;
    private readonly string _arguments;

    public StubOplService(int cliloc, string arguments = "3")
    {
        _cliloc = cliloc;
        _arguments = arguments;
    }

    public OplSnapshot GetOrBuild(Serial serial)
        => new(1, [new(_cliloc, _arguments)]);

    public void Invalidate(Serial serial) { }
}
