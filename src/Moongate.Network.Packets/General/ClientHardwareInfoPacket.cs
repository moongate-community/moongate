using System.Diagnostics.CodeAnalysis;
using System.Text;
using Moongate.Network.Packets.Attributes;
using Moongate.Network.Packets.Base;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Spans;
using Moongate.Network.Packets.Types.Packets;

namespace Moongate.Network.Packets.General;

/// <summary>
///     Hardware and operating system information the client sends once connected to the game server ("Spy on
///     Client 2"). The server only needs it for diagnostics; the packet exists so the frame is consumed instead
///     of being rejected as an unknown opcode. Numbers are big-endian and text is UTF-16BE.
/// </summary>
[PacketHandler(0xD9, PacketSizing.Fixed, Length = 268, Description = "Client hardware information")]
public sealed class ClientHardwareInfoPacket : BaseFixedPacket<ClientHardwareInfoPacket>,
    IIncomingPacket<ClientHardwareInfoPacket>
{
    private const int VideoDescriptionLength = 128;
    private const int LanguageCodeLength = 8;
    private const int TrailingUnknownLength = 64;

    /// <summary>
    ///     Always 0x02 on the classic client; not verified for the Enhanced Client.
    /// </summary>
    public required byte ClientType { get; init; }

    public required uint InstanceId { get; init; }
    public required uint OsMajor { get; init; }
    public required uint OsMinor { get; init; }
    public required uint OsRevision { get; init; }
    public required byte CpuManufacturer { get; init; }
    public required uint CpuFamily { get; init; }
    public required uint CpuModel { get; init; }
    public required uint CpuClockSpeedMhz { get; init; }
    public required byte CpuCount { get; init; }
    public required uint MemoryMb { get; init; }
    public required uint ScreenWidth { get; init; }
    public required uint ScreenHeight { get; init; }
    public required uint ScreenDepth { get; init; }
    public required ushort DirectXMajor { get; init; }
    public required ushort DirectXMinor { get; init; }
    public required string VideoDescription { get; init; }
    public required uint VideoVendorId { get; init; }
    public required uint VideoDeviceId { get; init; }
    public required uint VideoMemoryMb { get; init; }
    public required byte Distribution { get; init; }
    public required byte ClientsRunning { get; init; }
    public required byte ClientsInstalled { get; init; }
    public required byte PartialInstalled { get; init; }
    public required string LanguageCode { get; init; }

    public static bool TryParse(ReadOnlySpan<byte> data, [NotNullWhen(true)] out ClientHardwareInfoPacket? packet)
    {
        packet = null;

        if (!HasValidHeader(data))
        {
            return false;
        }

        var reader = new PacketReader(data[1..]);

        if (!reader.TryReadByte(out var clientType) ||
            !reader.TryReadUInt32BigEndian(out var instanceId) ||
            !reader.TryReadUInt32BigEndian(out var osMajor) ||
            !reader.TryReadUInt32BigEndian(out var osMinor) ||
            !reader.TryReadUInt32BigEndian(out var osRevision) ||
            !reader.TryReadByte(out var cpuManufacturer) ||
            !reader.TryReadUInt32BigEndian(out var cpuFamily) ||
            !reader.TryReadUInt32BigEndian(out var cpuModel) ||
            !reader.TryReadUInt32BigEndian(out var cpuClockSpeedMhz) ||
            !reader.TryReadByte(out var cpuCount) ||
            !reader.TryReadUInt32BigEndian(out var memoryMb) ||
            !reader.TryReadUInt32BigEndian(out var screenWidth) ||
            !reader.TryReadUInt32BigEndian(out var screenHeight) ||
            !reader.TryReadUInt32BigEndian(out var screenDepth) ||
            !reader.TryReadUInt16BigEndian(out var directXMajor) ||
            !reader.TryReadUInt16BigEndian(out var directXMinor) ||
            !reader.TryReadBytes(VideoDescriptionLength, out var videoDescription) ||
            !reader.TryReadUInt32BigEndian(out var videoVendorId) ||
            !reader.TryReadUInt32BigEndian(out var videoDeviceId) ||
            !reader.TryReadUInt32BigEndian(out var videoMemoryMb) ||
            !reader.TryReadByte(out var distribution) ||
            !reader.TryReadByte(out var clientsRunning) ||
            !reader.TryReadByte(out var clientsInstalled) ||
            !reader.TryReadByte(out var partialInstalled) ||
            !reader.TryReadBytes(LanguageCodeLength, out var languageCode) ||
            !reader.TryReadBytes(TrailingUnknownLength, out _))
        {
            return false;
        }

        packet = new()
        {
            ClientType = clientType,
            InstanceId = instanceId,
            OsMajor = osMajor,
            OsMinor = osMinor,
            OsRevision = osRevision,
            CpuManufacturer = cpuManufacturer,
            CpuFamily = cpuFamily,
            CpuModel = cpuModel,
            CpuClockSpeedMhz = cpuClockSpeedMhz,
            CpuCount = cpuCount,
            MemoryMb = memoryMb,
            ScreenWidth = screenWidth,
            ScreenHeight = screenHeight,
            ScreenDepth = screenDepth,
            DirectXMajor = directXMajor,
            DirectXMinor = directXMinor,
            VideoDescription = DecodeText(videoDescription),
            VideoVendorId = videoVendorId,
            VideoDeviceId = videoDeviceId,
            VideoMemoryMb = videoMemoryMb,
            Distribution = distribution,
            ClientsRunning = clientsRunning,
            ClientsInstalled = clientsInstalled,
            PartialInstalled = partialInstalled,
            LanguageCode = DecodeText(languageCode)
        };

        return true;
    }

    private static string DecodeText(ReadOnlySpan<byte> utf16BigEndian)
    {
        var text = Encoding.BigEndianUnicode.GetString(utf16BigEndian);
        var terminator = text.IndexOf('\0');

        return terminator < 0 ? text : text[..terminator];
    }
}
