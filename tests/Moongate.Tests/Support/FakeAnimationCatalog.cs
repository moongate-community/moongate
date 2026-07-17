using Moongate.Http.Plugin.Data.Mobiles;
using Moongate.Http.Plugin.Interfaces.Mobiles;
using Moongate.Ultima.Imaging;
using Moongate.Ultima.Types;

namespace Moongate.Tests.Support;

/// <summary>
/// Frames without client files: tests declare which (graphic, hue) pairs decode and to what size and
/// center. GetFrame clones per call, exactly like the real catalog, so double-dispose cannot happen.
/// </summary>
public sealed class FakeAnimationCatalog : IAnimationCatalog
{
    /// <summary>(graphic, hue) → (width, height, centerX, centerY). Direction/action/frame are ignored.</summary>
    public Dictionary<(int Graphic, int Hue), (int W, int H, int Cx, int Cy)> Frames { get; } = new();

    /// <summary>itemId → tiledata animation id.</summary>
    public Dictionary<int, int> ItemAnimations { get; } = new();

    /// <summary>(body, equipAnim) → conversion.</summary>
    public Dictionary<(int Body, int Anim), (int AnimId, int Hue)> Conversions { get; } = new();

    public List<(int Body, MobType Type)> Bodies { get; } = [];

    public bool IsReady { get; set; } = true;

    public IReadOnlyList<(int Body, MobType Type)> ClassifiedBodies => Bodies;

    public MobileFrame? GetFrame(int body, int action, int direction, int frame, int hue)
    {
        if (!Frames.TryGetValue((body, hue), out var spec))
        {
            return null;
        }

        return new MobileFrame(spec.Cx, spec.Cy, new UltimaBitmap(spec.W, spec.H));
    }

    public int? GetItemAnimation(int itemId)
        => ItemAnimations.TryGetValue(itemId, out var animation) ? animation : null;

    public bool TryConvertEquipment(int body, int equipmentAnim, out (int AnimId, int Hue) conversion)
        => Conversions.TryGetValue((body, equipmentAnim), out conversion);
}
