using Moongate.UO.Data.Bodies;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
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

    public int X => Location.X;

    public int Y => Location.Y;

    public int Z => Location.Z;

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

    public Body Body => Race.Body(this);
    public int HairStyle { get; set; }
    public int HairHue { get; set; }
    public int FacialHairStyle { get; set; }
    public int FacialHairHue { get; set; }
    public int SkinHue { get; set; }

    public ProfessionInfo Profession { get; set; }

    public Point3D Location { get; set; }

    public Map Map { get; set; }

    public DirectionType Direction { get; set; }

    //public Dictionary<SkillName, double> Skills { get; set; } = new();

    public int Level { get; set; } = 1;
    public long Experience { get; set; } = 0;
    public int SkillPoints { get; set; } = 0;
    public int StatPoints { get; set; } = 0;

    /// Character flags and status
    public bool IsAlive { get; set; } = true;

    public bool IsHidden { get; set; }
    public bool IsFrozen { get; set; }

    public bool IsWarMode { get; set; }
    public bool  IsFlying { get; set; }

    public bool IsBlessed { get; set; }

    public bool IgnoreMobiles { get; set; }

    public bool IsPoisoned { get; set; }

    public bool IsParalyzed { get; set; }
    public bool IsInvulnerable { get; set; } = false;

    /// Timing and persistence
    public DateTime Created { get; set; } = DateTime.UtcNow;

    public DateTime LastLogin { get; set; } = DateTime.UtcNow;
    public DateTime LastSaved { get; set; } = DateTime.UtcNow;
    public TimeSpan TotalPlayTime { get; set; } = TimeSpan.Zero;

    public Dictionary<ItemLayerType, ItemReference> Equipment { get; set; } = new();

    public List<SkillEntry> Skills { get; set; }

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
        return Skills.FirstOrDefault(s => s.Skill.SkillID == (int)skill)?.Value ?? 0.0;
    }

    public void SetSkillValue(SkillName skill, double value)
    {
        var skillEntry = Skills.FirstOrDefault(s => s.Skill.SkillID == (int)skill);
        if (skillEntry != null)
        {
            skillEntry.Value = value;
        }
        else
        {
            // If skill does not exist, create a new entry
            Skills.Add(new SkillEntry
            {
                Skill = SkillInfo.Table[(int)skill],
                Value = value,
                Cap = 100, // Default cap, can be adjusted later
                Lock = SkillLock.Locked // Default lock state
            });
        }
    }

    public void UpdateLastLogin()
    {
        LastLogin = DateTime.UtcNow;
    }

    public void UpdatePlayTime(TimeSpan sessionTime)
    {
        TotalPlayTime = TotalPlayTime.Add(sessionTime);
    }

    public virtual void ReceiveSpeech()
    {

    }

    public virtual void Speech()
    {

    }

    public virtual int GetPacketFlags(bool stygianAbyss)
    {
        var flags = 0x0;

        if (IsParalyzed || IsFrozen)
        {
            flags |= 0x01;
        }

        if (Gender == GenderType.Female)
        {
            flags |= 0x02;
        }

        if (stygianAbyss)
        {
            if (IsFlying)
            {
                flags |= 0x04;
            }
        }
        else
        {
            if (IsPoisoned)
            {
                flags |= 0x04;
            }
        }

        if (IsBlessed)
        {
            flags |= 0x08;
        }

        if (IgnoreMobiles)
        {
            flags |= 0x10;
        }

        if (IgnoreMobiles)
        {
            flags |= 0x40;
        }

        if (IsHidden)
        {
            flags |= 0x80;
        }

        return flags;
    }

}
