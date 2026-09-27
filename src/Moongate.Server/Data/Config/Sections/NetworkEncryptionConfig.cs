using Moongate.Network.Packets.Data.Encryption;
using Moongate.Network.Packets.Types.Encryption;
using Moongate.Server.Types.Network;

namespace Moongate.Server.Data.Config.Sections;

/// <summary>POL-compatible encryption policy shared by the login and game listeners.</summary>
public sealed class NetworkEncryptionConfig
{
    public NetworkEncryptionMode Mode { get; set; } = NetworkEncryptionMode.Disabled;
    public string ClientVersion { get; set; } = "";

    public void Validate()
    {
        if (!Enum.IsDefined(Mode))
        {
            throw new InvalidOperationException("network.encryption.mode must be Disabled, Optional, or Required.");
        }

        if (Mode == NetworkEncryptionMode.Disabled)
        {
            return;
        }

        try
        {
            if (UoEncryptionProfile.Parse(ClientVersion).Type != UoEncryptionType.None)
            {
                return;
            }
        }
        catch (FormatException exception)
        {
            throw new InvalidOperationException("network.encryption.client_version must be a valid POL client version.", exception);
        }

        throw new InvalidOperationException("Enabled network encryption requires an encrypted client version.");
    }

    /// <summary>Describes the effective policy and algorithms for the startup log.</summary>
    public string GetDescription()
    {
        Validate();
        if (Mode == NetworkEncryptionMode.Disabled)
        {
            return "Disabled (plaintext)";
        }

        var type = UoEncryptionProfile.Parse(ClientVersion).Type;
        var algorithm = type == UoEncryptionType.Twofish ? "Twofish / MD5-XOR" : type.ToString();
        return $"{Mode}; client {ClientVersion}; login XOR; game {algorithm}";
    }
}
