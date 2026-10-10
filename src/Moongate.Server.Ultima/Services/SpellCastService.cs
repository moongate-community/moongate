using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Scripting.Data.Scripts;
using Moongate.Scripting.Types.Scripts;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Ultima.Data.Effects;
using Moongate.Server.Ultima.Data.Internal.Spells;
using Moongate.Server.Ultima.Data.Mobiles;
using Moongate.Server.Ultima.Data.Spells;
using Moongate.Server.Ultima.Data.Targeting;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Services.Internal;
using Moongate.Server.Ultima.Types.Mobiles;
using Moongate.Server.Ultima.Types.Spells;
using Moongate.Server.Ultima.Types.Targeting;
using Moongate.Ultima.Types;
using Serilog;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     Casts the spells of Magery by the classic rules: see <see cref="ISpellCastService" />.
/// </summary>
public sealed class SpellCastService : ISpellCastService
{
    private const string DelayTimer = "spell_cast";
    private const string GestureTimer = "spell_gesture";
    private const double GestureSeconds = 1.5;
    private const int GestureFrames = 7;
    private const int FizzleGraphic = 0x3735;
    private const int FizzleSpeed = 6;
    private const int FizzleDuration = 30;
    private const int FizzleSound = 0x5C;
    private const int ReflectGraphic = 0x37B9;
    private const int ReflectSpeed = 10;
    private const int ReflectDuration = 5;
    private const int FirstCircle = 1;
    private const int EyeHeight = 14;
    private const int DefaultSkillCap = 1000;
    private const double TenthsPerPoint = 10.0;

    private readonly ILogger _logger;
    private readonly Dictionary<Serial, SpellCast> _casts = [];
    private readonly Dictionary<Serial, DateTimeOffset> _recovered = [];
    private readonly ISpellCatalogService _catalog;
    private readonly ISpellbookService _books;
    private readonly ISpellScriptService _scripts;
    private readonly IItemService _items;
    private readonly IItemTemplateService _templates;
    private readonly IItemHandlingService _handling;
    private readonly IMobileService _mobiles;
    private readonly IMobileStateService _state;
    private readonly ISessionService _sessions;
    private readonly ITargetService _targets;
    private readonly ISpeechService _speech;
    private readonly IEffectService _effects;
    private readonly IWorldViewService _view;
    private readonly ISkillService _skills;
    private readonly ILineOfSightService _sight;
    private readonly ITimerService _timers;
    private readonly TimeProvider _time;
    private readonly IMountService? _mounts;
    private readonly IJailService? _jail;

    public SpellCastService(
        ISpellCatalogService catalog,
        ISpellbookService books,
        ISpellScriptService scripts,
        IItemService items,
        IItemTemplateService templates,
        IItemHandlingService handling,
        IMobileService mobiles,
        IMobileStateService state,
        ISessionService sessions,
        ITargetService targets,
        ISpeechService speech,
        IEffectService effects,
        IWorldViewService view,
        ISkillService skills,
        ILineOfSightService sight,
        ITimerService timers,
        TimeProvider time,
        IMountService? mounts = null,
        IJailService? jail = null,
        ILogger? logger = null
    )
    {
        _logger = logger ?? Log.ForContext<SpellCastService>();
        _mounts = mounts;
        _jail = jail;
        _catalog = catalog;
        _books = books;
        _scripts = scripts;
        _items = items;
        _templates = templates;
        _handling = handling;
        _mobiles = mobiles;
        _state = state;
        _sessions = sessions;
        _targets = targets;
        _speech = speech;
        _effects = effects;
        _view = view;
        _skills = skills;
        _sight = sight;
        _timers = timers;
        _time = time;
    }

    public bool CastFromBook(MobileEntity caster, int spellId, ItemEntity? preferred = null)
    {
        if (!_catalog.TryGet(spellId, out var spell))
        {
            _speech.TellCliloc(caster, ISpellCastService.MissingSpellMessage);

            return false;
        }

        var book = preferred is not null && _books.IsSpellbook(preferred) && _books.IsCarriedBy(caster, preferred) &&
                   _books.Has(preferred, spellId)
            ? preferred
            : _books.FindCarried(caster, spellId);

        if (book is null)
        {
            _speech.TellCliloc(caster, ISpellCastService.MissingSpellMessage);

            return false;
        }

        return Begin(caster, spell, null);
    }

    public bool CastFromScroll(MobileEntity caster, ItemEntity scroll)
    {
        if (!_catalog.TryGetByScrollGraphic(scroll.ItemId, out var spell))
        {
            _speech.TellCliloc(caster, ISpellCastService.DisabledMessage);

            return false;
        }

        if (!IsInPack(caster, scroll))
        {
            _speech.TellCliloc(caster, ISpellCastService.MustBeInPackMessage);

            return false;
        }

        return Begin(caster, spell, scroll);
    }

    public bool IsCasting(MobileEntity caster)
    {
        return _casts.ContainsKey(caster.Id);
    }

    public bool BlocksMovement(MobileEntity caster)
    {
        return _casts.TryGetValue(caster.Id, out var cast) && cast.Phase == SpellCastPhaseType.Casting;
    }

    public void Hurt(MobileEntity caster)
    {
        // The first circle is never ruined by damage, as the classic game has it; an NPC is not disturbed.
        if (caster.IsNpc ||
            !_casts.TryGetValue(caster.Id, out var cast) ||
            cast.Phase != SpellCastPhaseType.Casting ||
            cast.Spell.Circle == FirstCircle)
        {
            return;
        }

        var elapsed = (_time.GetUtcNow() - cast.StartedAt).TotalSeconds;
        End(caster, cast);
        _recovered[caster.Id] = _time.GetUtcNow()
            .AddSeconds(SpellCircleRules.DisturbRecovery(elapsed, DelayOf(cast.Spell)));
        _speech.TellCliloc(caster, ISpellCastService.DisturbedMessage);
    }

    public void Cancel(MobileEntity caster)
    {
        if (!_casts.TryGetValue(caster.Id, out var cast))
        {
            return;
        }

        var waiting = cast.Phase == SpellCastPhaseType.Targeting;
        End(caster, cast);

        if (waiting && _sessions.TryGetByCharacterId(caster.Id, out var session))
        {
            _targets.Cancel(session);
        }
    }

    public void OnSessionClosed(GameSession session)
    {
        if (session.CharacterId.IsValid && _mobiles.TryGet(session.CharacterId, out var caster) &&
            _casts.TryGetValue(caster.Id, out var cast))
        {
            End(caster, cast);
        }

        _recovered.Remove(session.CharacterId);
    }

    private bool Begin(MobileEntity caster, SpellDefinition spell, ItemEntity? scroll)
    {
        if (caster.IsDead)
        {
            _speech.TellCliloc(caster, ISpellCastService.DeadMessage);

            return false;
        }

        // As the jail region of ModernUO: a prisoner casts nothing, so it cannot Recall or Teleport out. Staff is exempt.
        if (_jail?.GetSentence(caster.Id) is not null &&
            !(_sessions.TryGetByCharacterId(caster.Id, out var jailed) && jailed.AccountType >= AccountType.GameMaster))
        {
            _speech.TellCliloc(caster, ISpellCastService.NotInJailMessage);

            return false;
        }

        if (_casts.TryGetValue(caster.Id, out var current))
        {
            // The cursor of the spell before stays: a new cast waits for it, as the classic game does.
            if (current.Phase == SpellCastPhaseType.Casting)
            {
                _speech.TellCliloc(caster, ISpellCastService.AlreadyCastingMessage);
            }

            return false;
        }

        if (!spell.Enabled || !_scripts.Has(spell))
        {
            _speech.TellCliloc(caster, ISpellCastService.DisabledMessage);

            return false;
        }

        if (caster.Frozen)
        {
            _speech.TellCliloc(caster, ISpellCastService.WhileFrozenMessage);

            return false;
        }

        var now = _time.GetUtcNow();

        if (_recovered.TryGetValue(caster.Id, out var until) && until > now)
        {
            _speech.TellCliloc(caster, ISpellCastService.NotRecoveredMessage);

            return false;
        }

        var mana = SpellCircleRules.Mana(spell.Circle);

        if (caster.Mana < mana)
        {
            _speech.SayClilocTo(caster, caster, ISpellCastService.NoManaMessage, mana.ToString());

            return false;
        }

        var cast = new SpellCast(spell, scroll, now);
        _casts[caster.Id] = cast;

        // A spell reveals the hidden caster, then its words are said over its head: a player's only.
        if (caster.Hidden)
        {
            _state.SetHidden(caster, false);
        }

        if (!caster.IsNpc && !string.IsNullOrEmpty(spell.Mantra))
        {
            _speech.Say(caster, spell.Mantra);
        }

        var delay = DelayOf(spell);
        Gesture(caster, cast);

        // One more gesture for each 1.5 seconds the delay lasts, the first being the one just made.
        if (delay > GestureSeconds)
        {
            cast.GestureTimer = _timers.RegisterTimer(
                GestureTimer,
                TimeSpan.FromSeconds(GestureSeconds),
                () => Gesture(caster, cast)
            );
        }

        cast.DelayTimer = _timers.RegisterTimer(DelayTimer, TimeSpan.FromSeconds(delay), () => Ready(caster, cast));

        return true;
    }

    // The delay of the circle, times what the spell asks for: a few summons were slowed in the classic game.
    private static double DelayOf(SpellDefinition spell)
    {
        return SpellCircleRules.CastDelay(spell.Circle) * spell.CastDelayScale;
    }

    private void Gesture(MobileEntity caster, SpellCast cast)
    {
        if (_casts.GetValueOrDefault(caster.Id) == cast &&
            cast.Phase == SpellCastPhaseType.Casting &&
            _mounts?.IsMounted(caster) != true &&
            _mobiles.IsInWorld(caster.Id))
        {
            _view.MobileAnimated(caster, cast.Spell.Action, GestureFrames, 1);
        }
    }

    // The delay is over: the next cast may follow after the recovery, and the spell asks for its target or takes effect.
    private void Ready(MobileEntity caster, SpellCast cast)
    {
        if (_casts.GetValueOrDefault(caster.Id) != cast)
        {
            return;
        }

        cast.DelayTimer = null;
        cast.Phase = SpellCastPhaseType.Targeting;
        _recovered[caster.Id] = _time.GetUtcNow().AddSeconds(SpellCircleRules.RecoverySeconds);

        if (cast.Spell.Target == SpellTargetType.None)
        {
            Execute(caster, cast, SpellTargetInfo.None);

            return;
        }

        if (!_sessions.TryGetByCharacterId(caster.Id, out var session))
        {
            End(caster, cast);

            return;
        }

        var spell = cast.Spell;
        var cursor = spell.Target == SpellTargetType.Location ? TargetCursorType.Location : TargetCursorType.Object;
        var flags = spell.Harmful ? TargetFlagsType.Harmful :
            spell.Target == SpellTargetType.Mobile ? TargetFlagsType.Beneficial : TargetFlagsType.Neutral;
        _targets.Begin(session, cursor, flags, (_, result) => Targeted(caster, cast, result));
    }

    private void Targeted(MobileEntity caster, SpellCast cast, TargetResult result)
    {
        if (_casts.GetValueOrDefault(caster.Id) != cast)
        {
            return;
        }

        if (result.Kind == TargetResultType.Canceled || !_mobiles.IsInWorld(caster.Id))
        {
            End(caster, cast);

            return;
        }

        if (!TryResolve(caster, cast.Spell.Target, result, out var target, out var eye))
        {
            _speech.TellCliloc(caster, ISpellCastService.WontWorkMessage);
            End(caster, cast);

            return;
        }

        if (!(target.Kind == SpellTargetType.Mobile && target.Serial == caster.Id))
        {
            if (target.Map != caster.Map ||
                Math.Max(Math.Abs(target.Location.X - caster.Location.X), Math.Abs(target.Location.Y - caster.Location.Y)) >
                ISpellCastService.TargetRange)
            {
                _speech.TellCliloc(caster, ISpellCastService.TooFarMessage);
                End(caster, cast);

                return;
            }

            var from = new Point3D(caster.Location.X, caster.Location.Y, caster.Location.Z + EyeHeight);

            if (!_sight.HasLineOfSight(caster.Map, from, eye))
            {
                _speech.TellCliloc(caster, ISpellCastService.CannotSeeMessage);
                End(caster, cast);

                return;
            }
        }

        Execute(caster, cast, target);
    }

    // What the cursor picked, as the spell asks for it: a mobile, an item, or a place (an object gives its own).
    private bool TryResolve(
        MobileEntity caster,
        SpellTargetType wanted,
        TargetResult result,
        out SpellTargetInfo target,
        out Point3D eye
    )
    {
        target = SpellTargetInfo.None;
        eye = default;

        if (result.Kind == TargetResultType.Location)
        {
            if (wanted != SpellTargetType.Location)
            {
                return false;
            }

            target = new(SpellTargetType.Location, Serial.Zero, result.Map, result.Location);
            eye = result.Location;

            return true;
        }

        if (result.Serial.IsMobile && _mobiles.TryGet(result.Serial, out var mobile) && _mobiles.IsInWorld(mobile.Id))
        {
            if (wanted is not (SpellTargetType.Mobile or SpellTargetType.Location))
            {
                return false;
            }

            target = wanted == SpellTargetType.Mobile
                ? new(SpellTargetType.Mobile, mobile.Id, mobile.Map, mobile.Location)
                : new(SpellTargetType.Location, Serial.Zero, mobile.Map, mobile.Location);
            eye = new(mobile.Location.X, mobile.Location.Y, mobile.Location.Z + EyeHeight);

            return true;
        }

        if (!result.Serial.IsItem ||
            !_items.TryGet(result.Serial, out var item) ||
            wanted is not (SpellTargetType.Item or SpellTargetType.Location))
        {
            return false;
        }

        // An item the caster carries, such as a rune, is where the caster stands; any other must lie on the ground.
        var place = caster.Location;
        var map = caster.Map;

        if (_items.GetWornRoot(item)?.MobileId != caster.Id)
        {
            if (_items.GetGroundRoot(item) is not { GroundLocation: { } ground, Map: { } groundMap })
            {
                return false;
            }

            place = ground;
            map = groundMap;
        }

        target = wanted == SpellTargetType.Item
            ? new(SpellTargetType.Item, item.Id, map, place)
            : new(SpellTargetType.Location, Serial.Zero, map, place);
        eye = place;

        return true;
    }

    // The checks of the cast, in the order the classic game makes them, then the spell takes effect or fizzles.
    private void Execute(MobileEntity caster, SpellCast cast, SpellTargetInfo target)
    {
        End(caster, cast);
        var spell = cast.Spell;
        var scroll = cast.Scroll;

        if (caster.IsDead || !_mobiles.IsInWorld(caster.Id))
        {
            return;
        }

        if (scroll is not null && (!_items.TryGet(scroll.Id, out _) || scroll.Amount <= 0 || !IsInPack(caster, scroll)))
        {
            Fizzle(caster);

            return;
        }

        // A harmful spell takes nothing from the caster for a target that cannot be harmed.
        if (spell.Harmful &&
            target.Kind == SpellTargetType.Mobile &&
            _mobiles.TryGet(target.Serial, out var victim) &&
            victim.Notoriety == NotorietyType.Invulnerable)
        {
            _speech.TellCliloc(caster, ISpellCastService.CannotHarmMessage);

            return;
        }

        var verdict = _scripts.Check(spell, caster, target, scroll is not null);

        if (verdict.Kind == ScriptResultKind.Failed)
        {
            // A check that breaks is a refusal: nothing is spent for a script nobody can trust.
            _logger.Warning("The check of the spell {Spell} failed, the cast is refused", spell.Key);

            return;
        }

        if (Refused(caster, verdict))
        {
            return;
        }

        // Reagents are the book's: a scroll holds its own. They are lost when the spell fizzles.
        if (scroll is null && !caster.IsNpc && !TryTakeReagents(caster, spell))
        {
            _speech.SayClilocTo(caster, caster, ISpellCastService.NoReagentsMessage);

            return;
        }

        var mana = SpellCircleRules.Mana(spell.Circle);

        if (caster.Mana < mana)
        {
            _speech.SayClilocTo(caster, caster, ISpellCastService.NoManaMessage, mana.ToString());

            return;
        }

        var (min, max) = SpellCircleRules.SkillWindow(spell.Circle, scroll is not null);

        // Every try may teach Evaluating Intelligence, up to its cap, whether the spell then fizzles or not.
        var evaluating = caster.Skills.FirstOrDefault(known => known.Skill == SkillType.EvaluatingIntelligence);
        _skills.Check(caster, SkillType.EvaluatingIntelligence, 0, (evaluating?.Cap ?? DefaultSkillCap) / TenthsPerPoint);

        if (!_skills.Check(caster, SkillType.Magery, min, max))
        {
            Fizzle(caster);

            return;
        }

        _state.SetStats(caster, new MobileStatsChange { Mana = caster.Mana - mana });

        if (scroll is not null)
        {
            _handling.Consume(scroll);
        }

        // A spell Magic Reflection turns back keeps its caster, who is now its target: it hurts itself, with its own
        // skills, and the wearer it was aimed at is told to the script as the reflector.
        var reflector = TryReflect(caster, spell, ref target);

        var result = _scripts.Cast(spell, caster, target, scroll is not null, reflector);

        if (result.Kind is not (ScriptResultKind.Completed or ScriptResultKind.Suspended))
        {
            _logger.Warning("The script of the spell {Spell} did not run: {Result}", spell.Key, result.Kind);
        }
    }

    // Magic Reflection, as the classic single-use rule has it: the first harmful spell that can be reflected, aimed at
    // someone else who wears it, is turned on its caster and the reflection is gone. The reflected spell is not
    // reflected again, since the swap is made once, here. Only the target is swapped: the caster stays the caster and the
    // script makes it the aggressor of the wearer. Gives who reflected it.
    private MobileEntity? TryReflect(MobileEntity caster, SpellDefinition spell, ref SpellTargetInfo target)
    {
        if (!spell.Harmful ||
            !spell.Reflectable ||
            target.Kind != SpellTargetType.Mobile ||
            target.Serial == caster.Id ||
            !_mobiles.TryGet(target.Serial, out var wearer) ||
            !_mobiles.IsInWorld(wearer.Id) ||
            !wearer.GetProp(MagicProps.Reflect, false))
        {
            return null;
        }

        wearer.RemoveProp(MagicProps.Reflect);
        _effects.PlayOn(
            wearer.Id,
            wearer.Map,
            wearer.Location,
            new EffectOptions { Graphic = ReflectGraphic, Speed = ReflectSpeed, Duration = ReflectDuration }
        );
        target = new(SpellTargetType.Mobile, caster.Id, caster.Map, caster.Location);

        return wearer;
    }

    // The script of a spell may refuse before anything is spent: a cliloc number or a text is told to the caster, false
    // refuses without a word.
    private bool Refused(MobileEntity caster, ScriptResult verdict)
    {
        if (verdict.Kind != ScriptResultKind.Completed || verdict.Values is not [var answer, ..])
        {
            return false;
        }

        switch (answer)
        {
            case false:
                return true;
            case string text:
                _speech.Tell(caster, text);

                return true;
            case long or int or double:
                _speech.TellCliloc(caster, Convert.ToInt32(answer));

                return true;
            default:
                return false;
        }
    }

    private void Fizzle(MobileEntity caster)
    {
        _speech.SayClilocTo(caster, caster, ISpellCastService.FizzleMessage);
        _effects.PlayOn(
            caster.Id,
            caster.Map,
            caster.Location,
            new EffectOptions { Graphic = FizzleGraphic, Speed = FizzleSpeed, Duration = FizzleDuration }
        );
        _speech.PlaySound(caster, FizzleSound);
    }

    // Ends the cast: its timers stop and the caster may cast again once it has recovered.
    private void End(MobileEntity caster, SpellCast cast)
    {
        if (_casts.GetValueOrDefault(caster.Id) != cast)
        {
            return;
        }

        _casts.Remove(caster.Id);

        if (cast.DelayTimer is not null)
        {
            _timers.UnregisterTimer(cast.DelayTimer);
            cast.DelayTimer = null;
        }

        if (cast.GestureTimer is not null)
        {
            _timers.UnregisterTimer(cast.GestureTimer);
            cast.GestureTimer = null;
        }
    }

    private bool IsInPack(MobileEntity caster, ItemEntity item)
    {
        return _items.GetWornRoot(item) is { Layer: LayerType.Backpack } root && root.MobileId == caster.Id;
    }

    private bool TryTakeReagents(MobileEntity caster, SpellDefinition spell)
    {
        if (_items.GetWornAt(caster.Id, LayerType.Backpack) is not { } backpack)
        {
            return spell.Reagents.Count == 0;
        }

        var needed = new List<(int Graphic, int Amount)>();

        foreach (var reagent in spell.Reagents)
        {
            if (!_templates.TryGet(reagent.Template, out var template))
            {
                return false;
            }

            needed.Add(((int)template.ItemId.Value, reagent.Amount));
        }

        if (needed.Any(need => CountIn(backpack, need.Graphic) < need.Amount))
        {
            return false;
        }

        foreach (var (graphic, amount) in needed)
        {
            TakeFrom(backpack, graphic, amount);
        }

        return true;
    }

    private int CountIn(ItemEntity container, int graphic)
    {
        var total = 0;

        foreach (var item in _items.GetContents(container.Id))
        {
            total += item.ItemId == graphic ? item.Amount : 0;
            total += CountIn(item, graphic);
        }

        return total;
    }

    private int TakeFrom(ItemEntity container, int graphic, int amount)
    {
        var remaining = amount;

        foreach (var item in _items.GetContents(container.Id).ToList())
        {
            if (remaining <= 0)
            {
                break;
            }

            if (item.ItemId == graphic)
            {
                var take = Math.Min(remaining, item.Amount);

                if (_handling.Consume(item, take))
                {
                    remaining -= take;
                }
            }

            remaining = TakeFrom(item, graphic, remaining);
        }

        return remaining;
    }
}
