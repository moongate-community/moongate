using Moongate.UO.Data.Localization;
using Moongate.UO.Data.Skills;
using Moongate.UO.Data.Version;

namespace Moongate.UO.Context;

public static class UOContext
{

    public static int LineOfSight { get; set; } = 10;
    public static ClientVersion ServerClientVersion { get; set; }
    public static SkillInfo[] SkillsInfo { get; set; }

    public static Dictionary<int, CliLocEntry> LocalizedMessages { get; set; } = new();




}
