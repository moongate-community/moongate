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
using Moongate.UO.Data.Utils;
using Moongate.UO.Extensions;

namespace Moongate.UO.Data.Persistence.Entities;

public class UOMobileEntity : IPositionEntity, ISerialEntity, INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public delegate void EquipmentChangedEventHandler(UOMobileEntity mobile, ItemLayerType layer, ItemReference item);

    public event EquipmentChangedEventHandler? EquipmentAdded;
    public event EquipmentChangedEventHandler? EquipmentRemoved;

    public delegate void MobileMovedEventHandler(UOMobileEntity mobile, Point3D location);

    public delegate void ChatMessageDelegate(
        UOMobileEntity? mobile,
        ChatMessageType messageType,
        short hue,
        string text,
        int graphic,
        int font
    );

    public delegate void ChatMessageReceiveDelegate(
        UOMobileEntity? self,
        UOMobileEntity? sender,
        ChatMessageType messageType,
        short hue,
        string text,
        int graphic,
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

    public Serial Id { get; set; }
    public string? TemplateId { get; set; }

    public string BrainId { get; set; }

    public string Name { get; set; }
    public string Title { get; set; }

    [JsonIgnore]
    public bool IsPlayer { get; set; }

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

    public int HairStyle { get; set; }
    public int HairHue { get; set; }
    public int FacialHairStyle { get; set; }
    public int FacialHairHue { get; set; }
    public int SkinHue { get; set; }

    public ProfessionInfo Profession { get; set; }

    public Point3D Location { get; set; }

    public Map Map { get; set; }

    public DirectionType Direction { get; set; }

    //public Dictionary<UOSkillName, double> Skills { get; set; } = new();

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

    public void AddGold(int amount)
    {
        var currentGold = GetGold();
        SetGold(currentGold + amount);
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

    public virtual Body GetBody()
    {
        if (BaseBody == 0x00)
        {
            return Race.Body(this);
        }

        if (BaseBody == null)
        {
            return Race.Human.Body(this);
        }

        return BaseBody ?? Race.Body(this);
    }

    public int GetGold()
    {
        var backpack = Equipment[ItemLayerType.Backpack].ToEntity();

        return backpack.ContainsItem(0x0EEF, out var item) ? item.Amount : 0;
    }

    public UOItemEntity? GetItem(ItemLayerType layer)
        => Equipment.TryGetValue(layer, out var itemRef) ? itemRef.ToEntity() : null;

    public virtual byte GetPacketFlags(bool stygianAbyss)
    {
        byte flags = 0x0;

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

    public double GetSkillValue(UOSkillName skill)
    {
        return Skills.FirstOrDefault(s => s.Skill.SkillID == (int)skill)?.Value ?? 0.0;
    }

    public bool HasItem(ItemLayerType layer)
        => Equipment.ContainsKey(layer);

    public void Heal(int amount)
    {
        Hits = Math.Min(MaxHits, Hits + amount);
    }

    public void MoveTo(Point3D newLocation)
    {
        var oldLocation = Location;
        Location = newLocation;
    }

    public void OnItemRemoved(UOItemEntity item, Point3D location)
    {
        ItemRemoved?.Invoke(item, location);
    }

    public void OtherMobileMoved(UOMobileEntity mobile, Point3D location)
    {
        MobileMoved?.Invoke(mobile, location);
    }

    public void OverrideBody(Body body)
    {
        BaseBody = body;
        OnPropertyChanged(nameof(Body));
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

    public virtual void ReceiveSpeech(
        UOMobileEntity? mobileEntity,
        ChatMessageType messageType,
        short hue,
        string text,
        int graphic,
        int font
    )
    {
        ChatMessageReceived?.Invoke(this, mobileEntity, messageType, hue, text, graphic, font);
    }

    public void RemoveItem(ItemLayerType layer)
    {
        if (Equipment.Remove(layer, out var itemRef))
        {
            EquipmentRemoved?.Invoke(this, layer, itemRef);
        }
    }

    public void RestoreMana(int amount)
    {
        Mana = Math.Min(MaxMana, Mana + amount);
    }

    public void RestoreStamina(int amount)
    {
        Stamina = Math.Min(MaxStamina, Stamina + amount);
    }

    /// <summary>
    /// Say something with default settings (most common usage)
    /// Uses default hue, font, and regular message type
    /// </summary>
    public virtual void Say(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        Speech(ChatMessageType.Regular, SpeechHues.Default, text, SpeechHues.DefaultGraphic, SpeechHues.DefaultFont);
    }

    /// <summary>
    /// Say something with custom hue
    /// </summary>
    public virtual void Say(string text, short hue)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        Speech(ChatMessageType.Regular, hue, text, SpeechHues.DefaultGraphic, SpeechHues.DefaultFont);
    }

    /// <summary>
    /// Say something with formatting parameters
    /// </summary>
    public virtual void Say(string format, params object[] args)
    {
        if (string.IsNullOrEmpty(format) || args == null)
        {
            return;
        }

        var text = string.Format(format, args);
        Say(text);
    }

    /// <summary>
    /// Say something with custom hue and formatting
    /// </summary>
    public virtual void Say(short hue, string format, params object[] args)
    {
        if (string.IsNullOrEmpty(format) || args == null)
        {
            return;
        }

        var text = string.Format(format, args);
        Say(text, hue);
    }

    public void SetBody(Body body)
    {
        BaseBody = body;
        OnPropertyChanged(nameof(Body));
    }

    public void SetGold(int gold)
    {
        Gold = gold;
    }

    public void SetSkillValue(UOSkillName skill, double value)
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
                new()
                {
                    Skill = SkillInfo.Table[(int)skill],
                    Value = value,
                    Cap = 100,              // Default cap, can be adjusted later
                    Lock = UOSkillLock.Locked // Default lock state
                }
            );
        }
    }

    public virtual void Speech(ChatMessageType messageType, short hue, string text, int graphic, int font)
    {
        ChatMessageSent?.Invoke(this, messageType, hue, text, graphic, font);
    }

    public void UpdateLastLogin()
    {
        LastLogin = DateTime.UtcNow;
    }

    public void UpdatePlayTime(TimeSpan sessionTime)
    {
        TotalPlayTime = TotalPlayTime.Add(sessionTime);
    }

    public void ViewItemOnGround(UOItemEntity item, Point3D location)
    {
        ItemOnGround?.Invoke(item, location);
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new(propertyName));
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);

        return true;
    }

#region Emotes and Actions

    /// <summary>
    /// Perform an emote action (appears as *text*)
    /// </summary>
    public virtual void Emote(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        /// Auto-format with asterisks if not already present
        var emoteText = text.StartsWith("*") && text.EndsWith("*") ? text : $"*{text}*";
        Speech(ChatMessageType.Emote, SpeechHues.Default, emoteText, SpeechHues.DefaultGraphic, SpeechHues.DefaultFont);
    }

    /// <summary>
    /// Perform an emote with custom hue
    /// </summary>
    public virtual void Emote(string text, short hue)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var emoteText = text.StartsWith("*") && text.EndsWith("*") ? text : $"*{text}*";
        Speech(ChatMessageType.Emote, hue, emoteText, SpeechHues.DefaultGraphic, SpeechHues.DefaultFont);
    }

    /// <summary>
    /// Perform an emote with formatting
    /// </summary>
    public virtual void Emote(string format, params object[] args)
    {
        if (string.IsNullOrEmpty(format) || args == null)
        {
            return;
        }

        var text = string.Format(format, args);
        Emote(text);
    }

#endregion

#region Whispers and Yells

    /// <summary>
    /// Whisper (short range speech)
    /// </summary>
    public virtual void Whisper(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        Speech(ChatMessageType.Whisper, SpeechHues.Default, text, SpeechHues.DefaultGraphic, SpeechHues.DefaultFont);
    }

    /// <summary>
    /// Whisper with custom hue
    /// </summary>
    public virtual void Whisper(string text, short hue)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        Speech(ChatMessageType.Whisper, hue, text, SpeechHues.DefaultGraphic, SpeechHues.DefaultFont);
    }

    /// <summary>
    /// Yell (extended range speech)
    /// </summary>
    public virtual void Yell(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        Speech(ChatMessageType.Yell, SpeechHues.Default, text, SpeechHues.DefaultGraphic, SpeechHues.DefaultFont);
    }

    /// <summary>
    /// Yell with custom hue
    /// </summary>
    public virtual void Yell(string text, short hue)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        Speech(ChatMessageType.Yell, hue, text, SpeechHues.DefaultGraphic, SpeechHues.DefaultFont);
    }

#endregion

#region Combat and Spell Messages

    /// <summary>
    /// Combat message (for battle text)
    /// </summary>
    public virtual void CombatMessage(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        Speech(ChatMessageType.Regular, SpeechHues.Red, text, SpeechHues.DefaultGraphic, SpeechHues.DefaultFont);
    }

    /// <summary>
    /// Spell casting message
    /// </summary>
    public virtual void SpellMessage(string spellWords)
    {
        if (string.IsNullOrEmpty(spellWords))
        {
            return;
        }

        Speech(ChatMessageType.Spell, SpeechHues.Default, spellWords, SpeechHues.DefaultGraphic, SpeechHues.DefaultFont);
    }

    /// <summary>
    /// Magic effect message (for magical actions)
    /// </summary>
    public virtual void MagicMessage(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        Speech(ChatMessageType.Regular, SpeechHues.BrightBlue, text, SpeechHues.DefaultGraphic, SpeechHues.DefaultFont);
    }

#endregion
}
