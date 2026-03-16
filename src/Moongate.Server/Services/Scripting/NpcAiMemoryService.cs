using Moongate.Core.Data.Directories;
using Moongate.Core.Types;
using Moongate.Server.Interfaces.Services.Scripting;
using Moongate.UO.Data.Ids;

namespace Moongate.Server.Services.Scripting;

/// <summary>
/// Persists long-term NPC memories as plain text files under templates/npc_memories.
/// </summary>
public sealed class NpcAiMemoryService : INpcAiMemoryService
{
    private readonly string _memoriesRootPath;

    public NpcAiMemoryService(DirectoriesConfig directoriesConfig)
    {
        ArgumentNullException.ThrowIfNull(directoriesConfig);
        _memoriesRootPath = Path.Combine(directoriesConfig[DirectoryType.Templates], "npc_memories");
    }

    public string LoadOrCreate(Serial npcId, string npcName)
    {
        var memoryPath = ResolveMemoryPath(npcId);
        Directory.CreateDirectory(_memoriesRootPath);

        if (File.Exists(memoryPath))
        {
            return File.ReadAllText(memoryPath);
        }

        var defaultMemory = CreateDefaultMemory(npcName);
        File.WriteAllText(memoryPath, defaultMemory);

        return defaultMemory;
    }

    public void Save(Serial npcId, string memory)
    {
        var memoryPath = ResolveMemoryPath(npcId);
        Directory.CreateDirectory(_memoriesRootPath);
        File.WriteAllText(memoryPath, memory ?? string.Empty);
    }

    private string ResolveMemoryPath(Serial npcId)
    {
        var fullRootPath = Path.GetFullPath(_memoriesRootPath);
        var candidatePath = Path.GetFullPath(Path.Combine(fullRootPath, FormatFileName(npcId)));

        if (!candidatePath.StartsWith(fullRootPath, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Resolved npc memory path escaped memory root.");
        }

        return candidatePath;
    }

    private static string FormatFileName(Serial npcId)
    {
        var hex = npcId.Value.ToString("X");
        if (hex.Length < 6)
        {
            hex = hex.PadLeft(6, '0');
        }

        return $"0x{hex}.txt";
    }

    private static string CreateDefaultMemory(string npcName)
    {
        var resolvedName = string.IsNullOrWhiteSpace(npcName) ? "This NPC" : npcName.Trim();

        return $$"""
                 [Core Memory]
                 {{resolvedName}} has no long-term memories yet.

                 [Known Players]

                 [Recent Important Events]
                 """;
    }
}
