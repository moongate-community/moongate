using Moongate.Network.Packets.Incoming.GeneralInformation;
using Moongate.Network.Spans;
using UOMap = Moongate.UO.Data.Maps.Map;

namespace Moongate.Network.Packets.Outgoing.World;

/// <summary>
/// Creates commonly used General Information packets.
/// </summary>
public static class GeneralInformationFactory
{
    private const int SupportedMapPatchCount = 4;

    public static GeneralInformationPacket CreateSetCursorHueSetMap(byte mapId)
        => GeneralInformationPacketBuilder.CreateSetCursorHueSetMap(mapId);

    public static GeneralInformationPacket CreateSetCursorHueSetMap(UOMap? map)
        => GeneralInformationPacketBuilder.CreateSetCursorHueSetMap((byte)(map?.MapID ?? 0));

    public static GeneralInformationPacket CreateEnableMapDiffMapPatches()
    {
        var writer = new SpanWriter(36, true);
        writer.Write(SupportedMapPatchCount);

        for (var index = 0; index < SupportedMapPatchCount; index++)
        {
            var patch = UOMap.Maps[index]?.Tiles.Patch;
            writer.Write(patch?.StaticBlocks ?? 0);
            writer.Write(patch?.LandBlocks ?? 0);
        }

        var payload = writer.ToArray();
        writer.Dispose();

        return GeneralInformationPacketBuilder.CreateEnableMapDiff(payload);
    }
}
