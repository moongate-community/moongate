using Moongate.Core.Primitives;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Ultima.Types.Mobiles;
using Moongate.Ultima.Types;

namespace Moongate.Server.Ultima.Data.Templates.Mobiles;

/// <summary>
///     An authored mobile definition, creature or human NPC, one TOML entry under
///     <c>
///         templates/mobiles/
///     </c>
///     . Every field but <see cref="Id" /> may be unset: it is then inherited through
///     <see cref="BaseId" />, and with none anywhere the stated default applies.
/// </summary>
public class MobileTemplate
{
    /// <summary>
    ///     The stable id a spawn, a loot table or the <c>addnpc</c> command names this template by.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    ///     The <see cref="Id" /> of another <see cref="MobileTemplate" /> this one inherits unset fields from. Resolved by the loader across every loaded file.
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
    ///     The id of a list in <c>data/names.toml</c> to draw a random name from. <c>{gender}</c> becomes <c>male</c> or <c>female</c>, the gender the mobile gets. Unset with no <see cref="Name" />: no name.
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
    ///     The race: its body, skin, hair and beard come from <c>data/races.toml</c> when not set here. Unset for a creature.
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
    ///     Loot template ids rolled into the corpse. Unset is none.
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
    ///     Names the Lua module handling this template's behaviour, AI included. Unset is none.
    /// </summary>
    public string? ScriptId { get; set; }

    /// <summary>
    ///     The lowest account type that sees the mobile. Unset: everyone.
    /// </summary>
    public AccountType? Visibility { get; set; }

    /// <summary>
    ///     Free values for scripts. A child template's tags add to and override its base's.
    /// </summary>
    public Dictionary<string, string>? Tags { get; set; }
}
