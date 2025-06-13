using Moongate.UO.Data.Skills;
using Moongate.UO.Data.Version;

namespace Moongate.UO.Context;

public static class UOContext
{
    public static ClientVersion ServerClientVersion { get; set; }

    public static SkillInfo[] SkillsInfo { get; set; }

}
