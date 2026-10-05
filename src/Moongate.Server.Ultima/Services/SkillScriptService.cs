using Moongate.Core.Utils;
using Moongate.Scripting.Data.Config;
using Moongate.Scripting.Data.Scripts;
using Moongate.Scripting.Interfaces;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Services.Internal;
using Moongate.Ultima.Types;
using Serilog;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     Loads <c>scripts/skills/*.lua</c> at startup and calls the script of a skill a player uses.
/// </summary>
public sealed class SkillScriptService : ISkillScriptService, IMoongateStartupService
{
    public const string SkillsDirectory = "skills";

    private const string UseFunction = "on_use";

    private readonly ILogger _logger = Log.ForContext<SkillScriptService>();
    private readonly IScriptEngine _engine;
    private readonly IGameLoopService _loop;
    private readonly ScriptEngineOptions _options;
    private bool _running;

    public SkillScriptService(IScriptEngine engine, IGameLoopService loop, ScriptEngineOptions options)
    {
        _engine = engine;
        _loop = loop;
        _options = options;
    }

    public async Task StartAsync()
    {
        await ScriptDirectoryLoader.LoadAsync(_engine, _loop, _options.ScriptsDirectory, SkillsDirectory, _logger);
        _running = true;
    }

    public Task StopAsync()
    {
        _running = false;

        return Task.CompletedTask;
    }

    public ScriptResult Use(SkillType skill, MobileEntity user)
    {
        if (!_running || !Enum.IsDefined(skill))
        {
            return ScriptResult.Missing;
        }

        var name = EnumNameUtils.Format(skill);

        return _engine.CallMember($"{SkillsDirectory}/{name}.lua", name, UseFunction, (long)user.Id.Value);
    }
}
