using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Persistence.Interfaces;
using Moongate.Ultima.Maps;
using Moongate.UO.Data.Hues;
using Moongate.UO.Data.Types;

namespace Moongate.Persistence.Entities;

public class MobileEntity : ISerialIdEntity
{
    public Serial Id { get; set; }

    public string Name { get; set; }

    public int MapId { get; set; }

    public Point3D Position { get; set; }

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
}
