using Moongate.Core.Directories;
using Moongate.Core.Server.Attributes.Scripts;
using Moongate.Core.Server.Interfaces.Services;
using Moongate.Core.Server.Types;

namespace Moongate.Server.Modules;

[ScriptModule("scripts")]
public class LoadScriptModule
{
    private readonly IScriptEngineService _scriptEngineService;

    private readonly DirectoriesConfig _directoriesConfig;

    public LoadScriptModule(IScriptEngineService scriptEngineService, DirectoriesConfig directoriesConfig)
    {
        _scriptEngineService = scriptEngineService;
        _directoriesConfig = directoriesConfig;
    }

    [ScriptFunction("Include directory")]
    public void IncludeDirectory(string directoryName)
    {
        var fullPath = Path.Combine(_directoriesConfig[DirectoryType.Scripts], directoryName);

        if (!Directory.Exists(fullPath))
        {
            throw new DirectoryNotFoundException($"Directory '{directoryName}' not found in scripts directory.");
        }

        foreach (var file in Directory.GetFiles(fullPath, "*.js"))
        {
            try
            {
                _scriptEngineService.ExecuteScriptFile(file);
            }
            catch (Exception ex)
            {
                throw new($"Failed to load script '{file}': {ex.Message}", ex);
            }
        }
    }

    [ScriptFunction("Include script")]
    public void IncludeScript(string scriptName)
    {
        var fullPath = Path.Combine(_directoriesConfig[DirectoryType.Scripts], scriptName);

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"Script file '{scriptName}' not found in scripts directory.");
        }

        try
        {
            _scriptEngineService.ExecuteScriptFile(fullPath);
        }
        catch (Exception ex)
        {
            throw new($"Failed to load script '{scriptName}': {ex.Message}", ex);
        }
    }
}
