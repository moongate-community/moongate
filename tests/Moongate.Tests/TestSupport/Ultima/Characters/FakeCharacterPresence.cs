using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Interfaces;

namespace Moongate.Tests.TestSupport.Ultima.Characters;

/// <summary>
///     Says a character is in the world when its serial was added to <see cref="InWorld" />.
/// </summary>
public sealed class FakeCharacterPresence : ICharacterPresence
{
    public HashSet<Serial> InWorld { get; } = [];

    public bool IsInWorld(Serial characterId)
    {
        return InWorld.Contains(characterId);
    }
}
