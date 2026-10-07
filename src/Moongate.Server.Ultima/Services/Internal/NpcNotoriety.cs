using Moongate.Server.Ultima.Data.Death;
using Moongate.Server.Ultima.Data.Templates.Mobiles;
using Moongate.Server.Ultima.Types.Mobiles;

namespace Moongate.Server.Ultima.Services.Internal;

/// <summary>
///     The notoriety an NPC is given from its template: the template's own, else grey (attackable) for anything that is
///     not a human, as ModernUO's animals and monsters; a human with none reads as innocent.
/// </summary>
internal static class NpcNotoriety
{
    /// <summary>
    ///     Gets the notoriety to give the NPC; null for none, which reads as innocent.
    /// </summary>
    public static NotorietyType? Of(MobileTemplate? template, int body)
    {
        if (template is null)
        {
            return null;
        }

        return template.Notoriety ?? (CorpseProps.IsHumanBody(body) ? null : NotorietyType.Attackable);
    }
}
