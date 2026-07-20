using Moongate.Http.Plugin.Data.Mobiles;
using Moongate.Ultima.Types;

namespace Moongate.Http.Plugin.Interfaces.Mobiles;

/// <summary>
/// Decoded animation frames and body classification, behind a seam the tests can fake: the real
/// implementation reads Moongate.Ultima's process-wide statics, which tests do not have.
/// </summary>
public interface IAnimationCatalog
{
    /// <summary>True once the client files behind the statics are loaded.</summary>
    bool IsReady { get; }

    /// <summary>Classified bodies from mobtypes.txt, with their type.</summary>
    IReadOnlyList<(int Body, MobType Type)> ClassifiedBodies { get; }

    /// <summary>A decoded, hued frame the caller owns, or null when the graphic has none.</summary>
    MobileFrame? GetFrame(int body, int action, int direction, int frame, int hue);

    /// <summary>The animation id tiledata assigns to an item graphic (equipment, hair), or null.</summary>
    int? GetItemAnimation(int itemId);

    /// <summary>Equipconv.def lookup: fits an equipment animation to a body, optionally re-hued.</summary>
    bool TryConvertEquipment(int body, int equipmentAnim, out (int AnimId, int Hue) conversion);
}
