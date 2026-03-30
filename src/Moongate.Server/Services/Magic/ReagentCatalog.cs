using Moongate.Server.Types.Magic;

namespace Moongate.Server.Services.Magic;

/// <summary>
/// Maps reagent types to Moongate item template identifiers.
/// </summary>
public static class ReagentCatalog
{
    private static readonly IReadOnlyDictionary<ReagentType, string> TemplateIds =
        new Dictionary<ReagentType, string>
        {
            [ReagentType.BlackPearl] = "black_pearl",
            [ReagentType.Bloodmoss] = "bloodmoss",
            [ReagentType.Garlic] = "garlic",
            [ReagentType.Ginseng] = "ginseng",
            [ReagentType.MandrakeRoot] = "mandrake_root",
            [ReagentType.Nightshade] = "nightshade",
            [ReagentType.SulfurousAsh] = "sulfurous_ash",
            [ReagentType.SpidersSilk] = "spiders_silk",
            [ReagentType.BatWing] = "bat_wing",
            [ReagentType.GraveDust] = "grave_dust",
            [ReagentType.DaemonBlood] = "daemon_blood",
            [ReagentType.NoxCrystal] = "nox_crystal",
            [ReagentType.PigIron] = "pig_iron"
        };

    public static string? GetTemplateId(ReagentType reagent)
        => TemplateIds.TryGetValue(reagent, out var templateId) ? templateId : null;
}
