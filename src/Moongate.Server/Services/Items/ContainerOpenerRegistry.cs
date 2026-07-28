using Moongate.Core.Primitives;
using Moongate.Server.Abstractions.Interfaces.Items;

namespace Moongate.Server.Services.Items;

/// <summary>
/// In-memory <see cref="IContainerOpenerRegistry" />. Deliberately not persisted: an open gump is a
/// property of a live connection, and every session starts with nothing open.
/// </summary>
public sealed class ContainerOpenerRegistry : IContainerOpenerRegistry
{
    private readonly Dictionary<Serial, HashSet<Serial>> _openers = new();

    public void Closed(Serial container, Serial mobile)
    {
        if (_openers.TryGetValue(container, out var mobiles) && mobiles.Remove(mobile) && mobiles.Count == 0)
        {
            _openers.Remove(container);
        }
    }

    public void ForgetMobile(Serial mobile)
    {
        foreach (var container in _openers.Keys.ToList())
        {
            Closed(container, mobile);
        }
    }

    public void Opened(Serial container, Serial mobile)
    {
        if (!_openers.TryGetValue(container, out var mobiles))
        {
            mobiles = [];
            _openers[container] = mobiles;
        }

        mobiles.Add(mobile);
    }

    // Returns a copy on purpose: the refresh subscriber prunes while iterating it.
    public IReadOnlyCollection<Serial> OpenersOf(Serial container)
        => _openers.TryGetValue(container, out var mobiles) ? [.. mobiles] : [];
}
