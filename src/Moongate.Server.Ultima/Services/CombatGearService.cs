using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Data.Combat;
using Moongate.Server.Ultima.Data.Mobiles;
using Moongate.Server.Ultima.Data.Templates.Items;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Extensions;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Services.Internal;
using Moongate.Server.Ultima.Types.Combat;
using Moongate.Server.Ultima.Types.Items;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     Reads the weapon and the armor of a fight from the templates of the items a mobile has on. A piece without a
///     template, or a template without combat fields, counts for nothing.
/// </summary>
public sealed class CombatGearService : ICombatGearService
{
    private static readonly LayerType[] HandLayers = [LayerType.OneHanded, LayerType.TwoHanded];

    private readonly IItemService _items;
    private readonly IItemTemplateService _templates;

    public CombatGearService(IItemService items, IItemTemplateService templates)
    {
        _items = items;
        _templates = templates;
    }

    public WeaponInfo? WeaponOf(MobileEntity mobile)
    {
        return HeldWeapon(mobile, shoots: false);
    }

    public WeaponInfo? RangedWeaponOf(MobileEntity mobile)
    {
        return HeldWeapon(mobile, shoots: true);
    }

    public ItemEntity? AmmoOf(MobileEntity mobile, WeaponInfo weapon, Func<ItemEntity, bool>? accept = null)
    {
        var graphic = weapon.Type?.Ammo ?? 0;

        if (graphic == 0)
        {
            return null;
        }

        var pack = _items.GetWorn(mobile.Id).FirstOrDefault(item => item.Layer == LayerType.Backpack);

        return pack is null ? null : FindAmmo(pack.Id, graphic, accept ?? (_ => true));
    }

    // The first stack of the graphic in the container, or in a container inside it.
    private ItemEntity? FindAmmo(Serial container, int graphic, Func<ItemEntity, bool> accept)
    {
        var inside = _items.GetContents(container);

        return inside.FirstOrDefault(item => item.ItemId == graphic && item.Amount > 0 && accept(item)) ??
               inside.Select(item => FindAmmo(item.Id, graphic, accept)).FirstOrDefault(found => found is not null);
    }

    // What the mobile holds in its hands that is fought in melee, or that shoots: a bow or a crossbow.
    private WeaponInfo? HeldWeapon(MobileEntity mobile, bool shoots)
    {
        foreach (var item in _items.GetWorn(mobile.Id))
        {
            if (item.Layer is not { } layer ||
                Array.IndexOf(HandLayers, layer) < 0 ||
                !_templates.TryGet(item.TemplateId, out var template) ||
                template is not { DamageMax: > 0 } ||
                (shoots ? template.WeaponType is not (WeaponType.Bow or WeaponType.Crossbow) : template.WeaponType is { IsRanged: true }))
            {
                continue;
            }

            return new(
                template.WeaponType?.Skill ?? SkillType.Wrestling,
                template.WeaponType,
                template.TwoHandedWeapon == true,
                template.DamageMin ?? 0,
                template.DamageMax.Value,
                template.Speed ?? CombatService.FistsSpeed
            );
        }

        return null;
    }

    public int ArmorAt(MobileEntity mobile, ArmorZoneType zone)
    {
        var best = 0;

        foreach (var item in _items.GetWorn(mobile.Id))
        {
            if (item.Layer is { } layer &&
                CombatZones.ZoneOf(layer) == zone &&
                _templates.TryGet(item.TemplateId, out var template) &&
                template.ArmorRating is { } rating)
            {
                best = Math.Max(best, rating);
            }
        }

        return best;
    }

    public int ArmorRatingOf(MobileEntity mobile)
    {
        var rating = 0.0;

        foreach (var zone in Enum.GetValues<ArmorZoneType>())
        {
            rating += ArmorAt(mobile, zone) * CombatFormulas.ShareOf(zone);
        }

        // Rounded, as ModernUO's status.
        return (int)(rating + 0.5);
    }

    public MobileStatusInfo WithGear(MobileStatusInfo status, MobileEntity mobile)
    {
        if (mobile.IsNpc)
        {
            return status;
        }

        var weapon = RangedWeaponOf(mobile) ?? WeaponOf(mobile);
        var (min, max) = weapon is null
                             ? (CombatFormulas.FistsMinimumDamage, CombatFormulas.FistsMaximumDamage)
                             : (weapon.DamageMin, weapon.DamageMax);
        var tactics = Points(mobile, SkillType.Tactics);
        var anatomy = Points(mobile, SkillType.Anatomy);

        return status with
        {
            DamageMin = Math.Max(CombatFormulas.ScaleDamage(min, tactics, mobile.Strength, anatomy), 1),
            DamageMax = Math.Max(CombatFormulas.ScaleDamage(max, tactics, mobile.Strength, anatomy), 1),
            // Before AOS the client shows the armor rating where the physical resistance goes.
            PhysicalResistance = ArmorRatingOf(mobile)
        };
    }

    private static double Points(MobileEntity mobile, SkillType skill)
    {
        return (mobile.Skills.FirstOrDefault(known => known.Skill == skill)?.Base ?? 0) / 10.0;
    }
}
