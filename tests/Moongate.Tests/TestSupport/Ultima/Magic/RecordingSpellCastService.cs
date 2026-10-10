using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;

namespace Moongate.Tests.TestSupport.Ultima.Magic;

/// <summary>
///     Records the casts it is asked for; <see cref="Casting" /> and <see cref="Blocks" /> are what a test says.
/// </summary>
public sealed class RecordingSpellCastService : ISpellCastService
{
    public List<(MobileEntity Caster, int Spell, ItemEntity? Book)> FromBook { get; } = [];

    public List<(MobileEntity Caster, ItemEntity Scroll)> FromScroll { get; } = [];

    public List<MobileEntity> Hurts { get; } = [];

    public List<MobileEntity> Cancels { get; } = [];

    public bool Casting { get; set; }

    public bool Blocks { get; set; }

    public bool CastFromBook(MobileEntity caster, int spellId, ItemEntity? preferred = null)
    {
        FromBook.Add((caster, spellId, preferred));

        return true;
    }

    public bool CastFromScroll(MobileEntity caster, ItemEntity scroll)
    {
        FromScroll.Add((caster, scroll));

        return true;
    }

    public bool IsCasting(MobileEntity caster)
    {
        return Casting;
    }

    public bool BlocksMovement(MobileEntity caster)
    {
        return Blocks;
    }

    public void Hurt(MobileEntity caster)
    {
        Hurts.Add(caster);
    }

    public void Cancel(MobileEntity caster)
    {
        Cancels.Add(caster);
    }

    public void OnSessionClosed(GameSession session)
    {
    }
}
