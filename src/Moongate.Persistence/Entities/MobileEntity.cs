using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Core.Types;
using Moongate.Persistence.Interfaces;
using Moongate.Ultima.Types;
using Moongate.UO.Data.Hues;
using Moongate.UO.Data.Types;

namespace Moongate.Persistence.Entities;

public class MobileEntity : ISerialIdEntity, IPositionEntity
{
    public Serial Id { get; set; }

    public string Name { get; set; }

    public int MapId { get; set; }

    public Point3D Position { get; set; }

    public DirectionType Direction { get; set; }

    public GenderType Gender { get; set; }

    public RaceType Race { get; set; }

    public byte ProfessionId { get; set; }

    public int Strength { get; set; }

    public int Dexterity { get; set; }

    public int Intelligence { get; set; }

    /// <summary>Skill values keyed by skill id, stored in tenths (500 = 50.0).</summary>
    public Dictionary<int, int> Skills { get; set; } = new();

    public Hue SkinHue { get; set; }

    public ushort HairStyle { get; set; }

    public Hue HairHue { get; set; }

    public ushort FacialHairStyle { get; set; }

    public Hue FacialHairHue { get; set; }

    /// <summary>The body graphic id; for non-player mobiles it comes from the spawn template.</summary>
    public int Body { get; set; }

    /// <summary>Lua brain script id for the future AI system; empty when none.</summary>
    public string BrainScriptId { get; set; } = string.Empty;

    /// <summary>Loot table id for the future loot-on-death system; empty when none.</summary>
    public string LootTableId { get; set; } = string.Empty;

    /// <summary>Serial of this mobile's backpack container; <see cref="Serial.Zero" /> when it has none.</summary>
    public Serial BackpackId { get; set; }

    /// <summary>Equipped item serials keyed by layer; the bank box lives on <see cref="LayerType.Bank" />.</summary>
    public Dictionary<LayerType, Serial> EquippedItemIds { get; set; } = new();
}
