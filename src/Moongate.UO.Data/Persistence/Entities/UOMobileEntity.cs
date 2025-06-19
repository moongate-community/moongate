using System.Text.Json.Serialization;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Json.Converters;
using Moongate.UO.Data.Maps;
using Moongate.UO.Data.Professions;
using Moongate.UO.Data.Races.Base;
using Moongate.UO.Data.Skills;
using Moongate.UO.Data.Types;

namespace Moongate.UO.Data.Persistence.Entities;

public class UOMobileEntity
{
    public Serial Id { get; set; }
    public string Name { get; set; }
    public string Title { get; set; }

    public int Strength { get; set; } = 10;
    public int Dexterity { get; set; } = 10;
    public int Intelligence { get; set; } = 10;

    /// Current health/mana/stamina
    public int Hits { get; set; }

    public int Mana { get; set; }
    public int Stamina { get; set; }

    /// Max health/mana/stamina (calculated from stats but can be modified)
    public int MaxHits { get; set; }

    public int MaxMana { get; set; }
    public int MaxStamina { get; set; }

    /// Character appearance
    public GenderType Gender { get; set; }

    public Race Race { get; set; }
    public int HairStyle { get; set; }
    public int HairHue { get; set; }
    public int FacialHairStyle { get; set; }
    public int FacialHairHue { get; set; }
    public int SkinHue { get; set; }

    public ProfessionInfo Profession { get; set; }
    public Point3D Location { get; set; }

    [JsonConverter(typeof(MapConverter))] public Map Map { get; set; }

    public DirectionType Direction { get; set; }

    public Dictionary<SkillName, double> Skills { get; set; } = new();

    public int Level { get; set; } = 1;
    public long Experience { get; set; } = 0;
    public int SkillPoints { get; set; } = 0;
    public int StatPoints { get; set; } = 0;

    /// Character flags and status
    public bool IsAlive { get; set; } = true;

    public bool IsHidden { get; set; } = false;
    public bool IsFrozen { get; set; } = false;
    public bool IsInvulnerable { get; set; } = false;

    /// Timing and persistence
    public DateTime Created { get; set; } = DateTime.UtcNow;

    public DateTime LastLogin { get; set; } = DateTime.UtcNow;
    public DateTime LastSaved { get; set; } = DateTime.UtcNow;
    public TimeSpan TotalPlayTime { get; set; } = TimeSpan.Zero;

    /// Bank and currency
    public int Gold { get; set; } = 0;

    public void RecalculateMaxStats()
    {
        MaxHits = Math.Max(1, Strength);
        MaxMana = Math.Max(1, Intelligence);
        MaxStamina = Math.Max(1, Dexterity);

        /// Ensure current stats don't exceed max
        Hits = Math.Min(Hits, MaxHits);
        Mana = Math.Min(Mana, MaxMana);
        Stamina = Math.Min(Stamina, MaxStamina);
    }

    public void Heal(int amount)
    {
        Hits = Math.Min(MaxHits, Hits + amount);
    }

    public void RestoreMana(int amount)
    {
        Mana = Math.Min(MaxMana, Mana + amount);
    }

    public void RestoreStamina(int amount)
    {
        Stamina = Math.Min(MaxStamina, Stamina + amount);
    }

    public double GetSkillValue(SkillName skill)
    {
        return Skills.TryGetValue(skill, out var value) ? value : 0.0;
    }

    public void SetSkillValue(SkillName skill, double value)
    {
        Skills[skill] = Math.Max(0.0, Math.Min(100.0, value));
    }

    public void UpdateLastLogin()
    {
        LastLogin = DateTime.UtcNow;
    }

    public void UpdatePlayTime(TimeSpan sessionTime)
    {
        TotalPlayTime = TotalPlayTime.Add(sessionTime);
    }
}
