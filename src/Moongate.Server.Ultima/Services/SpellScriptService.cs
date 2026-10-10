using Lua;
using Moongate.Scripting.Data.Config;
using Moongate.Scripting.Data.Scripts;
using Moongate.Scripting.Interfaces;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Data.Spells;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Services.Internal;
using Moongate.Server.Ultima.Types.Spells;
using Serilog;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     Loads <c>scripts/spells/*.lua</c> at startup and calls the script of a spell that is cast.
/// </summary>
public sealed class SpellScriptService : ISpellScriptService, IMoongateStartupService
{
    public const string SpellsDirectory = "spells";

    private const string CastFunction = "cast";
    private const string CheckFunction = "check";

    private readonly ILogger _logger = Log.ForContext<SpellScriptService>();
    private readonly IScriptEngine _engine;
    private readonly IGameLoopService _loop;
    private readonly ScriptEngineOptions _options;
    private HashSet<string> _keys = new(StringComparer.Ordinal);
    private bool _running;

    public SpellScriptService(IScriptEngine engine, IGameLoopService loop, ScriptEngineOptions options)
    {
        _engine = engine;
        _loop = loop;
        _options = options;
    }

    public async Task StartAsync()
    {
        await ScriptDirectoryLoader.LoadAsync(_engine, _loop, _options.ScriptsDirectory, SpellsDirectory, _logger);
        var directory = Path.Combine(_options.ScriptsDirectory, SpellsDirectory);
        _keys = Directory.Exists(directory)
            ? Directory.GetFiles(directory, "*.lua", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileNameWithoutExtension)
                .OfType<string>()
                .ToHashSet(StringComparer.Ordinal)
            : new(StringComparer.Ordinal);
        _running = true;
    }

    public Task StopAsync()
    {
        _running = false;

        return Task.CompletedTask;
    }

    public bool Has(SpellDefinition spell)
    {
        // The file is there and it loaded: its table holds the cast function, which is all a spell must have.
        return _running && _keys.Contains(spell.Key) && _engine.HasMember(spell.Key, CastFunction);
    }

    public ScriptResult Check(SpellDefinition spell, MobileEntity caster, SpellTargetInfo target, bool fromScroll)
    {
        return Call(spell, CheckFunction, caster, target, fromScroll);
    }

    public ScriptResult Cast(SpellDefinition spell, MobileEntity caster, SpellTargetInfo target, bool fromScroll)
    {
        return Call(spell, CastFunction, caster, target, fromScroll);
    }

    private ScriptResult Call(SpellDefinition spell, string function, MobileEntity caster, SpellTargetInfo target, bool fromScroll)
    {
        if (!Has(spell))
        {
            return ScriptResult.Missing;
        }

        return _engine.CallMember(
            $"{SpellsDirectory}/{spell.Key}.lua",
            spell.Key,
            function,
            (long)caster.Id.Value,
            TargetTable(target),
            InfoTable(spell, fromScroll)
        );
    }

    private static LuaTable TargetTable(SpellTargetInfo target)
    {
        var table = new LuaTable();

        switch (target.Kind)
        {
            case SpellTargetType.Mobile:
                table["kind"] = "mobile";
                table["serial"] = (long)target.Serial.Value;

                break;
            case SpellTargetType.Item:
                table["kind"] = "item";
                table["serial"] = (long)target.Serial.Value;

                break;
            case SpellTargetType.Location:
                table["kind"] = "location";
                table["map"] = (int)target.Map;
                table["x"] = target.Location.X;
                table["y"] = target.Location.Y;
                table["z"] = target.Location.Z;

                break;
            default:
                table["kind"] = "none";

                break;
        }

        return table;
    }

    private static LuaTable InfoTable(SpellDefinition spell, bool fromScroll)
    {
        var table = new LuaTable();
        table["id"] = spell.Id;
        table["key"] = spell.Key;
        table["name"] = spell.Name;
        table["circle"] = spell.Circle;
        table["scroll"] = fromScroll;
        table["mana"] = SpellCircleRules.Mana(spell.Circle);
        table["sound"] = spell.Sound;
        table["effect"] = spell.Effect;
        table["effect_duration"] = spell.EffectDuration;
        table["projectile"] = spell.Projectile;
        table["projectile_speed"] = spell.ProjectileSpeed;
        table["harmful"] = spell.Harmful;
        table["resistable"] = spell.Resistable;

        return table;
    }
}
