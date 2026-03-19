using Moongate.Core.Data.Directories;
using Moongate.Core.Types;
using Moongate.Server.Interfaces.Services.Scripting;
using Serilog;

namespace Moongate.Server.Services.Scripting;

/// <summary>
/// Loads static NPC AI prompt files from templates/npc_ai_prompts.
/// </summary>
public sealed class NpcAiPromptService : INpcAiPromptService
{
    private static readonly ILogger Logger = Log.ForContext<NpcAiPromptService>();
    private readonly string _promptsRootPath;

    public NpcAiPromptService(DirectoriesConfig directoriesConfig)
    {
        ArgumentNullException.ThrowIfNull(directoriesConfig);
        _promptsRootPath = Path.Combine(directoriesConfig[DirectoryType.Templates], "npc_ai_prompts");
    }

    public bool TryLoad(string promptFile, out string prompt)
    {
        prompt = string.Empty;

        if (string.IsNullOrWhiteSpace(promptFile))
        {
            return false;
        }

        if (!TryResolvePromptPath(promptFile, out var promptPath))
        {
            Logger.Warning("Rejected npc AI prompt path '{PromptFile}'.", promptFile);

            return false;
        }

        if (!File.Exists(promptPath))
        {
            Logger.Warning("Npc AI prompt '{PromptPath}' was not found.", promptPath);

            return false;
        }

        prompt = File.ReadAllText(promptPath).Trim();

        return !string.IsNullOrWhiteSpace(prompt);
    }

    private bool TryResolvePromptPath(string promptFile, out string promptPath)
    {
        promptPath = string.Empty;
        var normalized = promptFile.Trim();

        if (Path.IsPathRooted(normalized))
        {
            return false;
        }

        var fileName = normalized.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)
                           ? normalized
                           : $"{normalized}.txt";
        var fullRootPath = Path.GetFullPath(_promptsRootPath);
        var candidatePath = Path.GetFullPath(Path.Combine(fullRootPath, fileName));

        if (!candidatePath.StartsWith(fullRootPath, StringComparison.Ordinal))
        {
            return false;
        }

        promptPath = candidatePath;

        return true;
    }
}
