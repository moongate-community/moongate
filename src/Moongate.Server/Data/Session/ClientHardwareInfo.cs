namespace Moongate.Server.Data.Session;

/// <summary>
/// Captures client-reported hardware metadata from packet 0xD9.
/// </summary>
public sealed class ClientHardwareInfo
{
    public byte ClientInfoVersion { get; set; }

    public uint InstanceId { get; set; }

    public uint OsMajor { get; set; }

    public uint OsMinor { get; set; }

    public uint OsRevision { get; set; }

    public byte CpuManufacturer { get; set; }

    public uint CpuFamily { get; set; }

    public uint CpuModel { get; set; }

    public uint CpuClockSpeed { get; set; }

    public byte CpuQuantity { get; set; }

    public uint PhysicalMemory { get; set; }

    public uint ScreenWidth { get; set; }

    public uint ScreenHeight { get; set; }

    public uint ScreenDepth { get; set; }

    public ushort DirectXVersion { get; set; }

    public ushort DirectXMinor { get; set; }

    public string VideoCardDescription { get; set; } = "";

    public uint VideoCardVendorId { get; set; }

    public uint VideoCardDeviceId { get; set; }

    public uint VideoCardMemory { get; set; }

    public byte Distribution { get; set; }

    public byte ClientsRunning { get; set; }

    public byte ClientsInstalled { get; set; }

    public byte PartialInstalled { get; set; }

    public byte UnknownFlag { get; set; }

    public string LanguageCode { get; set; } = "";

    public string UnknownEnding { get; set; } = "";
}
