using Moongate.UO.Data.Packets.GeneralInformation;
using Moongate.UO.Data.Packets.GeneralInformation.SubCommands;
using Moongate.UO.Data.Packets.GeneralInformation.SubCommands.Base.Interfaces;
using Moongate.UO.Data.Packets.GeneralInformation.Types;
using Moongate.Uo.Services.Network.Packets.GeneralInformation.SubCommands;

namespace Moongate.UO.Data.Extensions;

/// <summary>
/// Extension methods for General Information packets
/// </summary>
public static class GeneralInformationExtensions
{
    /// <summary>
    /// Parses the subcommand data as the specified type
    /// </summary>
    /// <typeparam name="T">Type to parse as</typeparam>
    /// <param name="packet">General Information packet</param>
    /// <returns>Parsed subcommand data</returns>
    public static T ParseSubcommand<T>(this GeneralInformationPacket packet) where T : class, ISubcommandData, new()
    {
        var parser = packet.CreateParser();
        return parser.Parse<T>();
    }

    public static ISubcommandData ParseSubcommandTyped(this GeneralInformationPacket packet)
    {
        return packet.SubcommandType switch
        {
            SubcommandType.ScreenSize => packet.ParseSubcommand<ScreenSizeData>(),
            SubcommandType.ClientLanguage => packet.ParseSubcommand<ClientLanguageData>(),
            SubcommandType.ClientType => packet.ParseSubcommand<ClientTypeData>(),
            SubcommandType.SetCursorHueSetMap => packet.ParseSubcommand<SetCursorHueSetMapData>(),
            SubcommandType.Damage => packet.ParseSubcommand<DamageData>(),
            _ => throw new NotSupportedException($"Unsupported subcommand type: {packet.SubcommandType}")
        };
    }


    /// <summary>
    /// Gets screen size data from packet
    /// </summary>
    /// <param name="packet">General Information packet</param>
    /// <returns>Screen size data or null if not applicable</returns>
    public static ScreenSizeData? GetScreenSize(this GeneralInformationPacket packet)
    {
        return packet.SubcommandType == SubcommandType.ScreenSize
            ? packet.ParseSubcommand<ScreenSizeData>()
            : null;
    }

    /// <summary>
    /// Gets client language data from packet
    /// </summary>
    /// <param name="packet">General Information packet</param>
    /// <returns>Client language data or null if not applicable</returns>
    public static ClientLanguageData? GetClientLanguage(this GeneralInformationPacket packet)
    {
        return packet.SubcommandType == SubcommandType.ClientLanguage
            ? packet.ParseSubcommand<ClientLanguageData>()
            : null;
    }

    /// <summary>
    /// Gets client type data from packet
    /// </summary>
    /// <param name="packet">General Information packet</param>
    /// <returns>Client type data or null if not applicable</returns>
    public static ClientTypeData? GetClientType(this GeneralInformationPacket packet)
    {
        return packet.SubcommandType == SubcommandType.ClientType
            ? packet.ParseSubcommand<ClientTypeData>()
            : null;
    }

    /// <summary>
    /// Gets map ID from Set Map packet
    /// </summary>
    /// <param name="packet">General Information packet</param>
    /// <returns>Map ID or null if not applicable</returns>
    public static byte? GetMapId(this GeneralInformationPacket packet)
    {
        return packet.SubcommandType == SubcommandType.SetCursorHueSetMap
            ? packet.ParseSubcommand<SetCursorHueSetMapData>()?.MapId
            : null;
    }

    /// <summary>
    /// Gets damage data from packet
    /// </summary>
    /// <param name="packet">General Information packet</param>
    /// <returns>Damage data or null if not applicable</returns>
    public static DamageData? GetDamage(this GeneralInformationPacket packet)
    {
        return packet.SubcommandType == SubcommandType.Damage
            ? packet.ParseSubcommand<DamageData>()
            : null;
    }

    /// <summary>
    /// Checks if this packet is sent by client
    /// </summary>
    /// <param name="packet">General Information packet</param>
    /// <returns>True if typically sent by client</returns>
    public static bool IsClientPacket(this GeneralInformationPacket packet)
    {
        return packet.SubcommandType switch
        {
            SubcommandType.ScreenSize          => true,
            SubcommandType.ClientLanguage      => true,
            SubcommandType.ClientType          => true,
            SubcommandType.ClosedStatusGump    => true,
            SubcommandType.Client3DAction      => true,
            SubcommandType.RequestPopupMenu    => true,
            SubcommandType.PopupEntrySelection => true,
            SubcommandType.UseTargetedItem     => true,
            SubcommandType.CastTargetedSpell   => true,
            SubcommandType.UseTargetedSkill    => true,
            _                                  => false
        };
    }

    /// <summary>
    /// Checks if this packet is sent by server
    /// </summary>
    /// <param name="packet">General Information packet</param>
    /// <returns>True if typically sent by server</returns>
    public static bool IsServerPacket(this GeneralInformationPacket packet)
    {
        return packet.SubcommandType switch
        {
            SubcommandType.InitializeFastWalkPrevention => true,
            SubcommandType.AddKeyToFastWalkStack        => true,
            SubcommandType.CloseGenericGump             => true,
            SubcommandType.SetCursorHueSetMap           => true,
            SubcommandType.DisplayPopupMenu             => true,
            SubcommandType.CloseUserInterfaceWindows    => true,
            SubcommandType.CodexOfWisdom                => true,
            SubcommandType.EnableMapDiff                => true,
            SubcommandType.ExtendedStats                => true,
            SubcommandType.NewSpellbook                 => true,
            SubcommandType.SendHouseRevisionState       => true,
            SubcommandType.CustomHousing                => true,
            SubcommandType.AbilityIconConfirm           => true,
            SubcommandType.Damage                       => true,
            SubcommandType.SEAbilityChange              => true,
            SubcommandType.MountSpeed                   => true,
            SubcommandType.ChangeRace                   => true,
            _                                           => false
        };
    }
}
