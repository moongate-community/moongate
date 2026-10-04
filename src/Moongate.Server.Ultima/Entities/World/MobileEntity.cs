using FreeSql.DataAnnotations;
using Moongate.Core.Geometry;
using Moongate.Core.Interfaces.Entities;
using Moongate.Core.Primitives;
using Moongate.Core.Types.Geometry;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Ultima.Data.Mobiles;
using Moongate.Server.Ultima.Entities.Internal;
using Moongate.Server.Ultima.Types.Mobiles;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Entities.World;

/// <summary>
///     A mobile of the world: a player character or an NPC. Both share the same serial range and the same state; an
///     NPC is the one without an <see cref="AccountId" />.
/// </summary>
/// <remarks>
///     Names are not unique, for NPCs or player characters; character creation checks only their form and banned
///     words.
/// </remarks>
[Table(Name = "world.mobiles")]
public class MobileEntity : IMoongateEntity
{
    [Column(Name = "id", IsPrimary = true, MapType = typeof(long))]
    public Serial Id { get; set; }

    /// <summary>
    ///     The account of a player character; <see langword="null" /> for an NPC.
    /// </summary>
    [Column(MapType = typeof(long?), IsNullable = true)]
    public Serial? AccountId { get; set; }

    /// <summary>
    ///     The character-list slot of a player character; <see langword="null" /> for an NPC.
    /// </summary>
    public int? Slot { get; set; }

    /// <summary>
    ///     When the player asked to delete this character, in UTC; null for an active character or an NPC.
    /// </summary>
    public DateTime? DeletionRequestedAt { get; set; }

    /// <summary>
    ///     Gets whether this mobile is an NPC, that is, it belongs to no account.
    /// </summary>
    [Column(IsIgnore = true)]
    public bool IsNpc => AccountId is null;

    public string Name { get; set; }

    [Column(MapType = typeof(byte))]
    public GenderType Gender { get; set; }

    [Column(MapType = typeof(byte))]
    public RaceType Race { get; set; }

    /// <summary>
    ///     The body id, which follows race and gender (for example 400 for a male human).
    /// </summary>
    public int Body { get; set; }

    public Hue SkinHue { get; set; }

    public int Strength { get; set; }

    public int Dexterity { get; set; }

    public int Intelligence { get; set; }

    /// <summary>
    ///     The item id of the hair style; 0 means no hair.
    /// </summary>
    public int HairStyle { get; set; }

    public Hue HairHue { get; set; }

    /// <summary>
    ///     The item id of the beard style; 0 means no beard.
    /// </summary>
    public int BeardStyle { get; set; }

    public Hue BeardHue { get; set; }

    /// <summary>
    ///     The skills the mobile has; a skill missing from the list is at 0 with the default cap and lock.
    /// </summary>
    [JsonMap, Column(DbType = "jsonb", IsNullable = true)]
    public List<MobileSkill> Skills { get; set; } = [];

    /// <summary>
    ///     The id of the mobile template an NPC was made from; <see langword="null" /> for a player character.
    /// </summary>
    public string? TemplateId { get; set; }

    /// <summary>
    ///     The title when it differs from the template's; null uses the template's.
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    ///     The notoriety when it differs from the template's; null uses the template's.
    /// </summary>
    [Column(MapType = typeof(byte?))]
    public NotorietyType? Notoriety { get; set; }

    /// <summary>
    ///     The current hit points.
    /// </summary>
    public int Hits { get; set; }

    /// <summary>
    ///     The hit points when fully healed.
    /// </summary>
    public int HitsMax { get; set; }

    /// <summary>
    ///     The current mana.
    /// </summary>
    public int Mana { get; set; }

    /// <summary>
    ///     The mana when full.
    /// </summary>
    public int ManaMax { get; set; }

    /// <summary>
    ///     The current stamina.
    /// </summary>
    public int Stamina { get; set; }

    /// <summary>
    ///     The stamina when fully rested.
    /// </summary>
    public int StaminaMax { get; set; }

    /// <summary>
    ///     The fame; an NPC's is rolled from its template.
    /// </summary>
    public int Fame { get; set; }

    /// <summary>
    ///     The karma; an NPC's is rolled from its template.
    /// </summary>
    public int Karma { get; set; }

    /// <summary>
    ///     The armor rating; an NPC's is rolled from its template.
    /// </summary>
    public int Armor { get; set; }

    /// <summary>
    ///     The physical resistance, in percent.
    /// </summary>
    public int ResistPhysical { get; set; }

    /// <summary>
    ///     The fire resistance, in percent.
    /// </summary>
    public int ResistFire { get; set; }

    /// <summary>
    ///     The cold resistance, in percent.
    /// </summary>
    public int ResistCold { get; set; }

    /// <summary>
    ///     The poison resistance, in percent.
    /// </summary>
    public int ResistPoison { get; set; }

    /// <summary>
    ///     The energy resistance, in percent.
    /// </summary>
    public int ResistEnergy { get; set; }

    [JsonMap, Column(DbType = "jsonb", IsNullable = true)]
    public Dictionary<string, object?>? Props { get; set; }

    public DateTime CreatedAt { get; set; }

    public int X { get; set; }

    public int Y { get; set; }

    public int Z { get; set; }

    /// <summary>
    ///     Stored as its byte value: <see cref="MapType" /> is byte-backed, which FreeSql cannot write to an int column.
    /// </summary>
    [Column(MapType = typeof(byte))]
    public MapType Map { get; set; }

    /// <summary>
    ///     Where the mobile faces, without the running bit; a character comes back facing the same way.
    /// </summary>
    [Column(MapType = typeof(byte))]
    public DirectionType Direction { get; set; } = DirectionType.South;

    /// <summary>
    ///     How full the mobile is, from 0 (starving) to 20 (full): it drops with time for a player and rises by eating.
    /// </summary>
    public int Hunger { get; set; } = 20;

    /// <summary>
    ///     How quenched the mobile is, from 0 (parched) to 20: it drops with time for a player and rises by drinking.
    /// </summary>
    public int Thirst { get; set; } = 20;

    /// <summary>
    ///     Whether the mobile is hidden: the players do not see it, the staff does.
    /// </summary>
    public bool Hidden { get; set; }

    /// <summary>
    ///     Whether the mobile is frozen: it neither steps nor turns.
    /// </summary>
    public bool Frozen { get; set; }

    /// <summary>
    ///     Until when the mobile is a criminal, in UTC; null for one that is not. Its name is grey until then.
    /// </summary>
    public DateTime? CriminalUntil { get; set; }

    /// <summary>
    ///     Whether the mobile is a criminal now, kept by the crime service from <see cref="CriminalUntil" />. It is not
    ///     a column: the time is what is saved.
    /// </summary>
    [Column(IsIgnore = true)]
    public bool Criminal { get; set; }

    /// <summary>
    ///     Gets the notoriety those who see the mobile are shown: its own, grey while it is a criminal, and a
    ///     murderer's red before that.
    /// </summary>
    [Column(IsIgnore = true)]
    public NotorietyType ShownNotoriety =>
        Notoriety == NotorietyType.Murderer ? NotorietyType.Murderer :
        Criminal ? NotorietyType.Criminal : Notoriety ?? NotorietyType.Innocent;

    /// <summary>
    ///     Whether the mobile is in war mode. It is not a column: a mobile comes back in peace.
    /// </summary>
    [Column(IsIgnore = true)]
    public bool WarMode { get; set; }

    /// <summary>
    ///     Gets or sets <see cref="X" />, <see cref="Y" /> and <see cref="Z" /> together. It is not a column: the three
    ///     coordinates are stored apart so the database can filter and index them.
    /// </summary>
    [Column(IsIgnore = true)]
    public Point3D Location
    {
        get => new(X, Y, Z);
        set => (X, Y, Z) = (value.X, value.Y, value.Z);
    }

    /// <summary>
    ///     Whether a viewer does not see the mobile: it is hidden, the viewer is not the mobile itself and is not staff.
    /// </summary>
    public bool IsHiddenFrom(Serial viewer, AccountType account)
    {
        return Hidden && viewer != Id && account < AccountType.GameMaster;
    }

    /// <summary>
    ///     Sets the prop <paramref name="key" />, such as a quest step for a script; null removes it.
    /// </summary>
    /// <exception cref="ArgumentException">
    ///     The key is empty, or the value is not a string, a number, a bool or an enum.
    /// </exception>
    public void SetProp(string key, object? value)
    {
        Props = PropsDictionary.Set(Props, key, value);
    }

    /// <summary>
    ///     Gets the prop <paramref name="key" /> as <typeparamref name="T" />, or <paramref name="defaultValue" /> when
    ///     the mobile does not have it.
    /// </summary>
    /// <exception cref="InvalidCastException">The prop holds a value that does not convert to <typeparamref name="T" />.</exception>
    public T GetProp<T>(string key, T defaultValue = default!)
    {
        return TryGetProp<T>(key, out var value) ? value : defaultValue;
    }

    /// <summary>
    ///     Gets the prop <paramref name="key" /> as <typeparamref name="T" />; false when the mobile does not have it.
    /// </summary>
    /// <exception cref="InvalidCastException">The prop holds a value that does not convert to <typeparamref name="T" />.</exception>
    public bool TryGetProp<T>(string key, out T value)
    {
        return PropsDictionary.TryGet(Props, key, out value);
    }

    /// <summary>
    ///     Removes the prop <paramref name="key" />; false when the mobile did not have it.
    /// </summary>
    public bool RemoveProp(string key)
    {
        Props = PropsDictionary.Remove(Props, key, out var removed);

        return removed;
    }

    /// <summary>
    ///     A one-line description for logs and debugging: serial, name, whose it is (the account of a player character,
    ///     the template of an NPC), race, gender, body and where it stands.
    /// </summary>
    /// <summary>
    ///     Gets a detached copy to save: the live mobile keeps changing on the game loop while the copy is written.
    /// </summary>
    public MobileEntity Snapshot()
    {
        var copy = (MobileEntity)MemberwiseClone();
        // Not a column: left in, a change of war mode alone would write the row again.
        copy.WarMode = false;
        copy.Criminal = false;

        // Only a character keeps its time: an NPC comes back innocent.
        if (IsNpc)
        {
            copy.CriminalUntil = null;
        }
        copy.Skills =
        [
            .. Skills.Select(
                skill => new MobileSkill
                    { Skill = skill.Skill, Base = skill.Base, Cap = skill.Cap, Lock = skill.Lock }
            )
        ];
        copy.Props = Props is null ? null : new Dictionary<string, object?>(Props);

        return copy;
    }

    public override string ToString()
    {
        var owner = IsNpc ? $"npc \"{TemplateId}\"" : $"player of {AccountId}";

        return $"{Id} \"{Name}\" {owner} ({Race} {Gender}, body 0x{Body:X4}) at {Map} {Location}";
    }
}
