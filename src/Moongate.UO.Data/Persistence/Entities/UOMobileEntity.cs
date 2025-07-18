using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using Moongate.UO.Data.Bodies;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Interfaces.Entities;
using Moongate.UO.Data.Maps;
using Moongate.UO.Data.Professions;
using Moongate.UO.Data.Races.Base;
using Moongate.UO.Data.Skills;
using Moongate.UO.Data.Types;
using Moongate.UO.Extensions;

namespace Moongate.UO.Data.Persistence.Entities;

public class UOMobileEntity : IPositionEntity, ISerialEntity, INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public delegate void EquipmentChangedEventHandler(
        UOMobileEntity mobile, ItemLayerType layer, ItemReference item
    );

    public event EquipmentChangedEventHandler? EquipmentAdded;
    public event EquipmentChangedEventHandler? EquipmentRemoved;


    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }


    public delegate void MobileMovedEventHandler(
        UOMobileEntity mobile, Point3D location
    );

    public delegate void ChatMessageDelegate(
        UOMobileEntity? mobile, ChatMessageType messageType, short hue, string text, int graphic, int font
    );

    public delegate void ChatMessageReceiveDelegate(
        UOMobileEntity? self, UOMobileEntity? sender, ChatMessageType messageType, short hue, string text, int graphic,
        int font
    );

    public delegate void ItemOnGroundDelegate(UOItemEntity item, Point3D location);

    public event ItemOnGroundDelegate? ItemOnGround;

    public event ItemOnGroundDelegate? ItemRemoved;

    public event ChatMessageReceiveDelegate? ChatMessageReceived;
    public event ChatMessageDelegate? ChatMessageSent;

    /// <summary>
    /// Called when a mobile != of self moves to a new location.
    /// </summary>
    public event MobileMovedEventHandler? MobileMoved;


    public void OtherMobileMoved(UOMobileEntity mobile, Point3D location)
    {
        MobileMoved?.Invoke(mobile, location);
    }


    public void MoveTo(Point3D newLocation)
    {
        var oldLocation = Location;
        Location = newLocation;
    }

    public void ViewItemOnGround(UOItemEntity item, Point3D location)
    {
        ItemOnGround?.Invoke(item, location);
    }

    public void OnItemRemoved(UOItemEntity item, Point3D location)
    {
        ItemRemoved?.Invoke(item, location);
    }

    public Serial Id { get; set; }

    public string? TemplateId { get; set; }

    public string Name { get; set; }
    public string Title { get; set; }

    [JsonIgnore] public bool IsPlayer { get; set; }

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

    public Body Body
    {
        get => GetBody();
        set => SetBody(value);
    }

    public Body? BaseBody { get; set; }


    public virtual Body GetBody()
    {
        if (BaseBody == 0x00)
        {
            return Race.Body(this);
        }
        return BaseBody ?? Race.Body(this);
    }

    public void SetBody(Body body)
    {
        BaseBody = body;
        OnPropertyChanged(nameof(Body));
    }

    public void OverrideBody(Body body)
    {
        BaseBody = body;
        OnPropertyChanged(nameof(Body));
    }

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


    public int FireResistance { get; set; } = 0;
    public int ColdResistance { get; set; } = 0;
    public int PoisonResistance { get; set; } = 0;
    public int EnergyResistance { get; set; } = 0;

    public int Luck { get; set; } = 0;

    /// Character flags and status
    public bool IsAlive { get; set; } = true;

    public bool IsHidden { get; set; }
    public bool IsFrozen { get; set; }

    public bool IsWarMode { get; set; }
    public bool IsFlying { get; set; }

    public bool IsBlessed { get; set; }
    public bool IgnoreMobiles { get; set; }
    public bool IsPoisoned { get; set; }
    public bool IsParalyzed { get; set; }
    public bool IsInvulnerable { get; set; }

    public bool IsMounted { get; set; }

    /// Timing and persistence
    public DateTime Created { get; set; } = DateTime.UtcNow;

    public DateTime LastLogin { get; set; } = DateTime.UtcNow;
    public DateTime LastSaved { get; set; } = DateTime.UtcNow;
    public TimeSpan TotalPlayTime { get; set; } = TimeSpan.Zero;

    public Dictionary<ItemLayerType, ItemReference> Equipment { get; set; } = new();
    public Notoriety Notoriety { get; set; } = Notoriety.Innocent;

    public List<SkillEntry> Skills { get; set; } = new();


    public int Gold { get; set; }


    public int GetGold()
    {
        var backpack = Equipment[ItemLayerType.Backpack].ToEntity();
        return backpack.ContainsItem(0x0EEF, out var item) ? item.Amount : 0;
    }

    public void SetGold(int gold)
    {
        Gold = gold;
    }

    public void AddGold(int amount)
    {
        var currentGold = GetGold();
        SetGold(currentGold + amount);
    }

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
            Skills.Add(
                new SkillEntry
                {
                    Skill = SkillInfo.Table[(int)skill],
                    Value = value,
                    Cap = 100,              // Default cap, can be adjusted later
                    Lock = SkillLock.Locked // Default lock state
                }
            );
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

    public void AddItem(ItemLayerType layer, UOItemEntity item)
    {
        item.OwnerId = Id;

        if (Equipment.ContainsKey(layer))
        {
            RemoveItem(layer);
        }

        Equipment[layer] = item.ToItemReference();

        EquipmentAdded?.Invoke(this, layer, item.ToItemReference());
    }

    public void RemoveItem(ItemLayerType layer)
    {
        if (Equipment.Remove(layer, out var itemRef))
        {
            EquipmentRemoved?.Invoke(this, layer, itemRef);
        }
    }

    public bool HasItem(ItemLayerType layer)
    {
        return Equipment.ContainsKey(layer);
    }

    public UOItemEntity? GetItem(ItemLayerType layer)
    {
        return Equipment.TryGetValue(layer, out var itemRef) ? itemRef.ToEntity() : null;
    }


    public virtual void ReceiveSpeech(
        UOMobileEntity? mobileEntity, ChatMessageType messageType, short hue, string text, int graphic, int font
    )
    {
        ChatMessageReceived?.Invoke(this, mobileEntity, messageType, hue, text, graphic, font);
    }

    public virtual void Speech(ChatMessageType messageType, short hue, string text, int graphic, int font)
    {
        ChatMessageSent?.Invoke(this, messageType, hue, text, graphic, font);
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
