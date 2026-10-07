using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Extensions;
using Moongate.Server.Ultima.Interfaces;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     Leaves the blood of a hit on the ground as ground items of the <c>blood_splash_*</c> templates, whose
///     <c>DecayAt</c> is set a few seconds ahead so the item decay service takes them away.
/// </summary>
public sealed class BloodService : IBloodService
{
    /// <summary>
    ///     The item templates a piece of blood is one of: the graphics ModernUO, ServUO and Source-X pick from.
    /// </summary>
    public static readonly IReadOnlyList<string> Templates =
    [
        "blood_splash_0x1645",
        "blood_splash_0x122a",
        "blood_splash_0x122b",
        "blood_splash_0x122c",
        "blood_splash_0x122d",
        "blood_splash_0x122e",
        "blood_splash_0x122f"
    ];

    private readonly CombatConfig _config;
    private readonly IMobileTemplateService _templates;
    private readonly IItemHandlingService _handling;
    private readonly IItemService _items;
    private readonly IWorldViewService _view;
    private readonly TimeProvider _time;
    private readonly Random _random;

    public BloodService(
        CombatConfig config,
        IMobileTemplateService templates,
        IItemHandlingService handling,
        IItemService items,
        IWorldViewService view,
        TimeProvider time,
        Random? random = null
    )
    {
        _config = config;
        _templates = templates;
        _handling = handling;
        _items = items;
        _view = view;
        _time = time;
        _random = random ?? Random.Shared;
    }

    public void Splash(MobileEntity target)
    {
        if (!_config.BloodEnabled || HueOf(target) is not { } hue)
        {
            return;
        }

        Piece(target, target.Location, hue);

        if (_config.BloodPieces == 0)
        {
            return;
        }

        var around = 1 + _random.Next(_config.BloodPieces);

        for (var i = 0; i < around; i++)
        {
            var spot = new Point3D(target.Location.X + _random.Next(3) - 1, target.Location.Y + _random.Next(3) - 1, target.Location.Z);
            Piece(target, spot, hue);
        }
    }

    // The hue of the blood, or null for a mobile that does not bleed. A player and an unknown template bleed red.
    private ushort? HueOf(MobileEntity target)
    {
        var hue = target.TemplateId is { } id && _templates.TryGet(id, out var template) ? template.BloodHue ?? 0 : 0;

        return hue < 0 ? null : (ushort)hue;
    }

    private void Piece(MobileEntity target, Point3D spot, ushort hue)
    {
        if (_handling.Make(Templates[_random.Next(Templates.Count)]) is not { } piece)
        {
            return;
        }

        piece.Hue = new Hue(hue);
        piece.PlaceOnGround(target.Map, spot);
        piece.DecayAt = _time.GetUtcNow().UtcDateTime.AddSeconds(_config.BloodSeconds);
        _items.Add([piece]);
        _view.ItemAppeared(piece);
    }
}
