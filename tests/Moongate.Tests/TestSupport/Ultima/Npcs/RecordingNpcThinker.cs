using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;

namespace Moongate.Tests.TestSupport.Ultima.Npcs;

/// <summary>
///     Records the NPCs it is asked to think for; throws when <see cref="Throw" /> is set.
/// </summary>
public sealed class RecordingNpcThinker : INpcThinker
{
    public List<MobileEntity> Thought { get; } = [];

    public bool Throw { get; set; }

    public void Think(MobileEntity npc)
    {
        Thought.Add(npc);

        if (Throw)
        {
            throw new InvalidOperationException("broken brain");
        }
    }
}
