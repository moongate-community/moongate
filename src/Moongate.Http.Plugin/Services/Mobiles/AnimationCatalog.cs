using Moongate.Http.Plugin.Data.Mobiles;
using Moongate.Http.Plugin.Interfaces.Mobiles;
using Moongate.Ultima.Animation;
using Moongate.Ultima.Io;
using Moongate.Ultima.Tiles;
using Moongate.Ultima.Types;

namespace Moongate.Http.Plugin.Services.Mobiles;

/// <summary>
/// The real catalog over Moongate.Ultima's statics. Frames are cloned out of the animation LRU cache:
/// callers dispose what they get, and the cache keeps what it owns. Callers serialize access through
/// the Ultima read gate — this class does not take it itself.
/// </summary>
public sealed class AnimationCatalog : IAnimationCatalog
{
    private readonly Lock _equipConvSync = new();

    private EquipConvTable? _equipConv;

    public bool IsReady
        => TileData.ItemTable is not null && MobTypes.IsLoaded;

    public IReadOnlyList<(int Body, MobType Type)> ClassifiedBodies
        => [.. MobTypes.GetDefinedBodies().Order().Select(body => (body, MobTypes.GetTypeOrDefault(body)))];

    public MobileFrame? GetFrame(int body, int action, int direction, int frame, int hue)
    {
        var resolvedHue = hue;
        var frames = Animations.GetAnimation(body, action, direction, ref resolvedHue, false, frame == 0);

        if (frames is null || frames.Length == 0)
        {
            return null;
        }

        var index = frame < frames.Length ? frame : 0;
        var decoded = frames[index];

        if (decoded.Bitmap is null || (decoded.Bitmap.Width <= 1 && decoded.Bitmap.Height <= 1))
        {
            return null;
        }

        return new MobileFrame(decoded.Center.X, decoded.Center.Y, decoded.Bitmap.Clone());
    }

    public int? GetItemAnimation(int itemId)
    {
        var table = TileData.ItemTable;

        if (table is null || itemId < 0 || itemId >= table.Length)
        {
            return null;
        }

        var animation = table[itemId].Animation;

        return animation > 0 ? animation : null;
    }

    public bool TryConvertEquipment(int body, int equipmentAnim, out (int AnimId, int Hue) conversion)
    {
        // Lazy: equipconv.def is resolvable only after the client files are located, which happens
        // during startup, before any request reaches us.
        if (_equipConv is null)
        {
            lock (_equipConvSync)
            {
                _equipConv ??= new EquipConvTable(
                    Files.GetFilePath("equipconv.def") ?? Path.Combine(Path.GetTempPath(), "equipconv.def.missing")
                );
            }
        }

        return _equipConv.TryConvert(body, equipmentAnim, out conversion);
    }
}
