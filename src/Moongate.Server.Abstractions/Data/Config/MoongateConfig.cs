namespace Moongate.Server.Abstractions.Data.Config;

/// <summary>Root Moongate configuration section, loaded from moongate.yaml.</summary>
public sealed class MoongateConfig
{
    public string ShardName { get; set; } = "Moongate";

    public int StatsRefreshSeconds { get; set; } = 30;

    public string UltimaDirectory { get; set; }

    /// <summary>
    /// Which client cliloc table to load, as the file suffix. One of chs, cht, custom1, custom2,
    /// deu, enu, esp, fra, jpn, kor — anything else finds no file, and items with no name of their
    /// own fall back to their template id.
    /// </summary>
    public string Language { get; set; } = "enu";

    /// <summary>
    /// How many 8x8-tile map blocks each facet keeps in memory, land and statics counted separately.
    /// Blocks are read from the client files on demand and the least recently used are dropped past
    /// this cap. The default holds a contiguous 512x512-tile region — far more than gameplay needs,
    /// and enough that panning the web map viewer around one area does not re-read constantly. Lower
    /// it on a memory-tight host; 0 disables caching entirely and re-reads every block.
    /// </summary>
    public int MapBlockCacheSize { get; set; } = 4096;

    public MoongateNetworkConfig Network { get; set; } = new();

    public NpcAiConfig NpcAi { get; set; } = new();

    /// <summary>
    /// When true, a world mutation attempted off the game-loop thread throws instead of warning.
    /// Off by default (a live shard warns rather than crashes); dev and CI turn it on so regressions
    /// fail loudly.
    /// </summary>
    public bool StrictLoopAffinity { get; set; } = false;
}
