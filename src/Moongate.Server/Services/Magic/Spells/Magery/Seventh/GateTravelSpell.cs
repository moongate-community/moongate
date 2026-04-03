using System.Globalization;
using Moongate.Server.Data.Internal.Scripting;
using Moongate.Server.Data.Magic;
using Moongate.Server.Services.Magic.Base;
using Moongate.Server.Types.Magic;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Maps;
using Moongate.UO.Data.Persistence.Entities;

namespace Moongate.Server.Services.Magic.Spells.Magery.Seventh;

/// <summary>
/// Gate Travel (Vas Rel Por) opens a temporary linked pair of moongates from a marked recall rune.
/// </summary>
public sealed class GateTravelSpell : MagerySpellBase
{
    private const string MoongateTemplateId = "moongate";
    private const string RecallRuneTemplateId = "recall_rune";
    private const string MarkedKey = "marked";
    private const string PointDestinationKey = "point_dest";
    private const string MapDestinationKey = "map_dest";
    private const string ImportedPointDestinationKey = "target";
    private const string ImportedMapDestinationKey = "target_map";
    private const string DescriptionKey = "description";
    private const string LinkedGateSerialKey = "linked_gate_serial";
    private const string GateDescriptionKey = "gate_description";
    private const string SourceEffectKey = "source_effect";
    private const string DestinationEffectKey = "dest_effect";
    private const string SoundIdKey = "sound_id";
    private const ushort GateSoundId = 0x20E;
    private static readonly TimeSpan GateLifetime = TimeSpan.FromSeconds(10);

    public override int SpellId => SpellIds.Magery.Seventh.GateTravel;

    public override SpellCircleType Circle => SpellCircleType.Seventh;

    public override SpellTargetingType Targeting => SpellTargetingType.RequiredItem;

    public override SpellInfo Info { get; } = new(
        "Gate Travel",
        "Vas Rel Por",
        [ReagentType.BlackPearl, ReagentType.MandrakeRoot, ReagentType.SulfurousAsh],
        [1, 1, 1]
    );

    public override double MinSkill => 70.0;

    public override double MaxSkill => 100.0;

    public override async ValueTask ApplyEffectAsync(SpellExecutionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        var rune = context.TargetItem;

        if (rune is null ||
            !TryResolveRuneDestination(rune, out var destinationMapId, out var destinationLocation, out var description))
        {
            return;
        }

        var sourceGate = await CreateGateAsync(
            context,
            context.Caster.Location,
            context.Caster.MapId,
            destinationLocation,
            destinationMapId,
            description,
            cancellationToken
        );

        if (sourceGate is null)
        {
            return;
        }

        var destinationGate = await CreateGateAsync(
            context,
            destinationLocation,
            destinationMapId,
            context.Caster.Location,
            context.Caster.MapId,
            description,
            cancellationToken
        );

        if (destinationGate is null)
        {
            await DeleteGateAsync(context, sourceGate.Id);

            return;
        }

        sourceGate.SetCustomInteger(LinkedGateSerialKey, sourceGate.Id == destinationGate.Id ? 0 : destinationGate.Id.Value);
        destinationGate.SetCustomInteger(LinkedGateSerialKey, sourceGate.Id.Value);
        await context.ItemService.UpsertItemsAsync(sourceGate, destinationGate);

        var timerName = $"spell_gate_travel_{context.Caster.Id}_{sourceGate.Id}_{destinationGate.Id}";
        context.TimerService.RegisterTimer(
            timerName,
            GateLifetime,
            () =>
            {
                DeleteGateAsync(context, sourceGate.Id).AsTask().GetAwaiter().GetResult();
                DeleteGateAsync(context, destinationGate.Id).AsTask().GetAwaiter().GetResult();
            }
        );
    }

    private static async ValueTask<UOItemEntity?> CreateGateAsync(
        SpellExecutionContext context,
        Point3D worldLocation,
        int worldMapId,
        Point3D destinationLocation,
        int destinationMapId,
        string? description,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        var gate = await context.ItemService.SpawnFromTemplateAsync(MoongateTemplateId);
        gate.SetCustomLocation(PointDestinationKey, destinationLocation);
        gate.SetCustomString(MapDestinationKey, destinationMapId.ToString(CultureInfo.InvariantCulture));
        gate.SetCustomBoolean(SourceEffectKey, true);
        gate.SetCustomBoolean(DestinationEffectKey, true);
        gate.SetCustomInteger(SoundIdKey, GateSoundId);

        if (!string.IsNullOrWhiteSpace(description))
        {
            gate.SetCustomString(GateDescriptionKey, description.Trim());
        }

        await context.ItemService.UpsertItemAsync(gate);

        var moved = await context.ItemService.MoveItemToWorldAsync(gate.Id, worldLocation, worldMapId);

        if (!moved)
        {
            await DeleteGateAsync(context, gate.Id);

            return null;
        }

        gate.Location = worldLocation;
        gate.MapId = worldMapId;
        context.SpatialWorldService.AddOrUpdateItem(gate, worldMapId);

        return gate;
    }

    private static async ValueTask DeleteGateAsync(SpellExecutionContext context, Moongate.UO.Data.Ids.Serial gateId)
    {
        if (gateId == 0)
        {
            return;
        }

        _ = await context.ItemService.DeleteItemAsync(gateId);
        context.SpatialWorldService.RemoveEntity(gateId);
    }

    private static bool TryResolveRuneDestination(
        UOItemEntity rune,
        out int mapId,
        out Point3D location,
        out string? description
    )
    {
        mapId = 0;
        location = Point3D.Zero;
        description = null;

        if (!IsRecallRune(rune) || !IsMarkedRune(rune))
        {
            return false;
        }

        if (!TryResolveDestinationLocation(rune, out location))
        {
            return false;
        }

        mapId = TryResolveDestinationMapId(rune, out var resolvedMapId) ? resolvedMapId : rune.MapId;
        rune.TryGetCustomString(DescriptionKey, out description);

        return true;
    }

    private static bool IsRecallRune(UOItemEntity item)
    {
        return item.TryGetCustomString(ItemCustomParamKeys.Item.TemplateId, out var templateId) &&
               string.Equals(templateId, RecallRuneTemplateId, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMarkedRune(UOItemEntity rune)
    {
        if (rune.TryGetCustomBoolean(MarkedKey, out var marked))
        {
            return marked;
        }

        if (rune.TryGetCustomInteger(MarkedKey, out var markedInteger))
        {
            return markedInteger != 0;
        }

        if (rune.TryGetCustomString(MarkedKey, out var markedRaw) && !string.IsNullOrWhiteSpace(markedRaw))
        {
            if (bool.TryParse(markedRaw, out var parsedMarked))
            {
                return parsedMarked;
            }

            if (long.TryParse(markedRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedInteger))
            {
                return parsedInteger != 0;
            }
        }

        return false;
    }

    private static bool TryResolveDestinationLocation(UOItemEntity rune, out Point3D location)
    {
        if (rune.TryGetCustomLocation(PointDestinationKey, out location))
        {
            return true;
        }

        return rune.TryGetCustomLocation(ImportedPointDestinationKey, out location);
    }

    private static bool TryResolveDestinationMapId(UOItemEntity rune, out int mapId)
    {
        if (TryResolveMapId(rune, MapDestinationKey, out mapId))
        {
            return true;
        }

        return TryResolveMapId(rune, ImportedMapDestinationKey, out mapId);
    }

    private static bool TryResolveMapId(UOItemEntity rune, string key, out int mapId)
    {
        mapId = 0;

        if (rune.TryGetCustomInteger(key, out var integerValue))
        {
            mapId = (int)integerValue;

            return true;
        }

        if (!rune.TryGetCustomString(key, out var rawValue) || string.IsNullOrWhiteSpace(rawValue))
        {
            return false;
        }

        if (int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out mapId))
        {
            return true;
        }

        for (var index = 0; index < Map.Maps.Length; index++)
        {
            var map = Map.Maps[index];

            if (map is not null && string.Equals(map.Name, rawValue, StringComparison.OrdinalIgnoreCase))
            {
                mapId = map.MapID;

                return true;
            }
        }

        return false;
    }
}
