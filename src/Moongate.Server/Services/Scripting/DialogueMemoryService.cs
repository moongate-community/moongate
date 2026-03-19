using System.Collections.Concurrent;
using System.Text.Json;
using Moongate.Core.Data.Directories;
using Moongate.Server.Data.Scripting;
using Moongate.Server.Interfaces.Services.Scripting;
using Moongate.Server.Json;
using Moongate.UO.Data.Ids;

namespace Moongate.Server.Services.Scripting;

/// <summary>
/// Persists typed dialogue memory per npc under runtime/dialogue_memory.
/// </summary>
public sealed class DialogueMemoryService : IDialogueMemoryService
{
    private sealed class CachedMemoryFile
    {
        public bool Dirty { get; set; }

        public NpcDialogueMemoryFile File { get; set; } = new();
    }

    private readonly ConcurrentDictionary<Serial, CachedMemoryFile> _cache = [];
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        TypeInfoResolver = MoongateServerJsonContext.Default
    };
    private readonly string _memoriesRootPath;

    public DialogueMemoryService(DirectoriesConfig directoriesConfig)
    {
        ArgumentNullException.ThrowIfNull(directoriesConfig);
        _memoriesRootPath = Path.Combine(directoriesConfig.Root, "runtime", "dialogue_memory");
    }

    public NpcDialogueMemoryFile LoadOrCreate(Serial npcId)
        => _cache.GetOrAdd(npcId, LoadMemoryFile).File;

    public DialogueMemoryEntry GetOrCreateEntry(Serial npcId, Serial otherMobileId)
    {
        var cached = _cache.GetOrAdd(npcId, LoadMemoryFile);

        if (!cached.File.Entries.TryGetValue(otherMobileId.Value, out var entry))
        {
            entry = new DialogueMemoryEntry();
            cached.File.Entries[otherMobileId.Value] = entry;
            cached.Dirty = true;
        }

        return entry;
    }

    public void MarkDirty(Serial npcId)
    {
        var cached = _cache.GetOrAdd(npcId, LoadMemoryFile);
        cached.Dirty = true;
    }

    public void Save(Serial npcId)
    {
        var cached = _cache.GetOrAdd(npcId, LoadMemoryFile);

        if (!cached.Dirty)
        {
            return;
        }

        var memoryPath = ResolveMemoryPath(npcId);
        Directory.CreateDirectory(_memoriesRootPath);
        File.WriteAllText(memoryPath, JsonSerializer.Serialize(cached.File, _jsonOptions));
        cached.Dirty = false;
    }

    private CachedMemoryFile LoadMemoryFile(Serial npcId)
    {
        var memoryPath = ResolveMemoryPath(npcId);
        Directory.CreateDirectory(_memoriesRootPath);

        if (File.Exists(memoryPath))
        {
            var json = File.ReadAllText(memoryPath);
            var memoryFile = JsonSerializer.Deserialize<NpcDialogueMemoryFile>(json, _jsonOptions) ??
                             CreateDefaultMemoryFile(npcId);

            if (memoryFile.NpcId == 0)
            {
                memoryFile.NpcId = npcId.Value;
            }

            return new()
            {
                Dirty = false,
                File = memoryFile
            };
        }

        var defaultFile = CreateDefaultMemoryFile(npcId);
        File.WriteAllText(memoryPath, JsonSerializer.Serialize(defaultFile, _jsonOptions));

        return new()
        {
            Dirty = false,
            File = defaultFile
        };
    }

    private static NpcDialogueMemoryFile CreateDefaultMemoryFile(Serial npcId)
        => new()
        {
            NpcId = npcId.Value
        };

    private static string FormatFileName(Serial npcId)
    {
        var hex = npcId.Value.ToString("X");

        if (hex.Length < 6)
        {
            hex = hex.PadLeft(6, '0');
        }

        return $"0x{hex}.json";
    }

    private string ResolveMemoryPath(Serial npcId)
    {
        var fullRootPath = Path.GetFullPath(_memoriesRootPath);
        var candidatePath = Path.GetFullPath(Path.Combine(fullRootPath, FormatFileName(npcId)));

        if (!candidatePath.StartsWith(fullRootPath, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Resolved dialogue memory path escaped memory root.");
        }

        return candidatePath;
    }
}
