using Moongate.Core.Primitives;
using Moongate.Core.Utils;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Ultima.Types.Guilds;
using Moongate.Server.Ultima.Types.Mobiles;
using Moongate.Server.Ultima.Utils;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Data.Templates.Mobiles;

/// <summary>
///     An authored mobile definition, creature or human NPC, one TOML entry under <c>templates/mobiles/</c>. Every
///     field but <see cref="Id" /> may be unset: it is then inherited through
///     <see cref="BaseId" />, and with none anywhere the stated default applies.
/// </summary>
public class MobileTemplate
{
    private const int MaximumSkillValue = 120; // A skill goes up to 120 with bonuses.
    private const int MaximumResistance = 100;
    private const int MaximumPercent = 100;
    private const int MaximumHue = ushort.MaxValue;

    /// <summary>
    ///     The stable id a spawn, a loot table or the <c>addnpc</c> command names this template by.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    ///     The <see cref="Id" /> of another <see cref="MobileTemplate" /> this one inherits unset fields from. Resolved by the
    ///     loader across every loaded file.
    /// </summary>
    public string? BaseId { get; set; }

    /// <summary>
    ///     A designer's note. Read by nobody at runtime.
    /// </summary>
    public string? Comment { get; set; }

    /// <summary>
    ///     A fixed name, such as "an orc". Unset draws one from <see cref="NameList" />.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    ///     The id of a list in <c>data/names.toml</c> to draw a random name from. <c>{gender}</c> becomes <c>male</c>
    ///     or <c>female</c>, the gender the mobile gets. Unset with no <see cref="Name" />: no name.
    /// </summary>
    public string? NameList { get; set; }

    /// <summary>
    ///     Shown after the name, such as "the guard". Unset is none.
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    ///     The body the client draws. Unset uses the <see cref="Race" /> body for the gender.
    /// </summary>
    public int? Body { get; set; }

    /// <summary>
    ///     Male, female, or picked 50/50 for every mobile. Unset is male.
    /// </summary>
    public MobileGenderType? Gender { get; set; }

    /// <summary>
    ///     The race: its body, skin, hair and beard come from <c>data/races.toml</c> when not set here. Unset for a
    ///     creature.
    /// </summary>
    public RaceType? Race { get; set; }

    /// <summary>
    ///     The skin hue or a range. Unset uses the race skin hues, else 0.
    /// </summary>
    public HueSpec? SkinHue { get; set; }

    /// <summary>
    ///     Hair item ids; one is picked. Unset uses the race styles for the gender.
    /// </summary>
    public List<int>? Hair { get; set; }

    /// <summary>
    ///     The hair hue or a range. Unset uses the race hair hues.
    /// </summary>
    public HueSpec? HairHue { get; set; }

    /// <summary>
    ///     Beard item ids; one is picked. Unset uses the race styles; females get none.
    /// </summary>
    public List<int>? Beard { get; set; }

    /// <summary>
    ///     The beard hue or a range. Unset uses the race hair hues.
    /// </summary>
    public HueSpec? BeardHue { get; set; }

    /// <summary>
    ///     Strength, rolled for every mobile. Unset is 10.
    /// </summary>
    public DiceSpec? Strength { get; set; }

    /// <summary>
    ///     Dexterity, rolled for every mobile. Unset is 10.
    /// </summary>
    public DiceSpec? Dexterity { get; set; }

    /// <summary>
    ///     Intelligence, rolled for every mobile. Unset is 10.
    /// </summary>
    public DiceSpec? Intelligence { get; set; }

    /// <summary>
    ///     Maximum hit points. Unset is the strength.
    /// </summary>
    public DiceSpec? Hits { get; set; }

    /// <summary>
    ///     Maximum mana. Unset is the intelligence.
    /// </summary>
    public DiceSpec? Mana { get; set; }

    /// <summary>
    ///     Maximum stamina. Unset is the dexterity.
    /// </summary>
    public DiceSpec? Stamina { get; set; }

    /// <summary>
    ///     The damage of an unarmed hit. Unset is 1d4.
    /// </summary>
    public DiceSpec? Damage { get; set; }

    /// <summary>
    ///     The natural armour. Unset is 0.
    /// </summary>
    public DiceSpec? Armor { get; set; }

    /// <summary>
    ///     Resistances in percent; each is inherited on its own. Unset is 0 each.
    /// </summary>
    public MobileResistances? Resistances { get; set; }

    /// <summary>
    ///     Skills by <c>SkillType</c> name, in whole points from 0 to 120; each is inherited on its own. Unset is none.
    /// </summary>
    public Dictionary<string, DiceSpec>? Skills { get; set; }

    /// <summary>
    ///     The name colour the client shows. Unset is <see cref="NotorietyType.Innocent" />.
    /// </summary>
    public NotorietyType? Notoriety { get; set; }

    /// <summary>
    ///     Karma, which may be negative. Unset is 0.
    /// </summary>
    public DiceSpec? Karma { get; set; }

    /// <summary>
    ///     Fame. Unset is 0.
    /// </summary>
    public DiceSpec? Fame { get; set; }

    /// <summary>
    ///     What the mobile wears and holds. Unset is nothing.
    /// </summary>
    public List<MobileEquipmentEntry>? Equipment { get; set; }

    /// <summary>
    ///     Loot template ids, each rolled once into the backpack when the mobile spawns; list one twice to roll it
    ///     twice. Unset is none.
    /// </summary>
    public List<string>? Loot { get; set; }

    /// <summary>
    ///     Gold in the backpack. Unset is 0.
    /// </summary>
    public DiceSpec? Gold { get; set; }

    /// <summary>
    ///     The mobile's sounds; each is inherited on its own. Unset is none.
    /// </summary>
    public MobileSounds? Sounds { get; set; }

    /// <summary>
    ///     The global Lua table, defined by <c>scripts/mobiles/&lt;script_id&gt;.lua</c>, whose functions handle the
    ///     NPC's events: <c>on_think</c>, <c>on_speech</c>, <c>on_spawn</c> and <c>on_mobile_in_range</c>. A lower-case
    ///     Lua identifier.
    ///     Unset: no script.
    /// </summary>
    public string? ScriptId { get; set; }

    /// <summary>
    ///     The guild this guildmaster takes members for, such as <c>blacksmiths</c>: a player says <c>join</c> to it
    ///     and
    ///     pays 500 gold. Unset: it is no guildmaster.
    /// </summary>
    public NpcGuildType? NpcGuild { get; set; }

    /// <summary>
    ///     The percent of its hit points under which the creature runs from a fight, from 0 to 100; -1 for one that never
    ///     does, as UOX3's <c>FLEEAT</c>. Unset: the script's own, 20 for a monster and 10 for an animal.
    /// </summary>
    public int? FleeAt { get; set; }

    /// <summary>
    ///     The hue of the blood the creature leaves when it is hit, 0 for the red of blood; -1 for one that does not
    ///     bleed, such as the undead and the golems, as ServUO's <c>BloodHue</c> and Source-X's <c>BLOODCOLOR</c>.
    ///     Unset:
    ///     red.
    /// </summary>
    public int? BloodHue { get; set; }

    /// <summary>
    ///     The lowest account type that sees the mobile. Unset: everyone.
    /// </summary>
    public AccountType? Visibility { get; set; }

    /// <summary>
    ///     Where the mobiles move: land, water or both. Unset is land.
    /// </summary>
    public MobileMovementType? Movement { get; set; }

    /// <summary>
    ///     Whether the mobiles open the closed doors in their way when they walk to a place. Unset: a human or a monster
    ///     body does, an animal or a sea creature does not.
    /// </summary>
    public bool? OpensDoors { get; set; }

    /// <summary>
    ///     Free values for scripts. A child template's tags add to and override its base's.
    /// </summary>
    public Dictionary<string, string>? Tags { get; set; }

    /// <summary>
    ///     Checks the values a template author can get wrong; the template loader calls it for every template.
    /// </summary>
    /// <exception cref="InvalidDataException">
    ///     A value is out of range; the message names the template and the field.
    /// </exception>
    public void Validate()
    {
        foreach (var (field, dice) in new (string, DiceSpec?)[]
                 {
                     ("strength", Strength), ("dexterity", Dexterity), ("intelligence", Intelligence), ("hits", Hits),
                     ("mana", Mana), ("stamina", Stamina), ("damage", Damage), ("armor", Armor), ("fame", Fame),
                     ("gold", Gold)
                 })
        {
            if (dice is { Min: < 0 })
            {
                throw Invalid(field, "must not roll below 0");
            }
        }

        if (Skills is not null)
        {
            foreach (var (name, dice) in Skills)
            {
                if (!EnumNameUtils.TryParse<SkillType>(name, out _))
                {
                    throw Invalid("skills", $"has '{name}', which is not a skill");
                }

                if (dice.Min < 0 || dice.Max > MaximumSkillValue)
                {
                    throw Invalid("skills", $"'{name}' must roll between 0 and 120");
                }
            }
        }

        if (Resistances is not null &&
            new[] { Resistances.Physical, Resistances.Fire, Resistances.Cold, Resistances.Poison, Resistances.Energy }
                .Any(dice => dice is { } value && (value.Min < 0 || value.Max > MaximumResistance)))
        {
            throw Invalid("resistances", "must roll between 0 and 100");
        }

        if (Sounds is not null &&
            new[] { Sounds.StartAttack, Sounds.Idle, Sounds.Attack, Sounds.Hurt, Sounds.Death }.Any(sound => sound < 0))
        {
            throw Invalid("sounds", "must be 0 or more");
        }

        if (Equipment is not null &&
            Equipment.Any(entry => entry.Items.Count == 0 || entry.Items.Any(string.IsNullOrWhiteSpace)))
        {
            throw Invalid("equipment", "must name at least one item and no empty item id");
        }

        if (Tags is not null && Tags.Keys.Any(string.IsNullOrWhiteSpace))
        {
            throw Invalid("tags", "must not have an empty key");
        }

        if (BloodHue is < -1 or > MaximumHue)
        {
            throw Invalid("blood_hue", "must be from -1 to 65535");
        }

        if (FleeAt is < -1 or > MaximumPercent)
        {
            throw Invalid("flee_at", "must be from -1 to 100");
        }

        if (ScriptId is not null && !ScriptIdUtils.IsValid(ScriptId))
        {
            throw Invalid("script_id", ScriptIdUtils.Rule);
        }
    }

    private InvalidDataException Invalid(string field, string rule)
    {
        return new($"Mobile template '{Id}': {field} {rule}.");
    }
}
