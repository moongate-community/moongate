using Moongate.Server.Types.Magic;
using Moongate.UO.Data.Ids;

namespace Moongate.Server.Data.Magic;

public sealed record SpellCastContext
{
    public SpellCastContext(Serial casterId, int spellId, SpellStateType state, string timerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(timerId);
        CasterId = casterId;
        SpellId = spellId;
        State = state;
        TimerId = timerId;
    }

    public Serial CasterId { get; init; }

    public int SpellId { get; init; }

    public SpellStateType State { get; set; }

    public string TimerId { get; init; }
}
