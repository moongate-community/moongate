namespace Moongate.Server.Types.Network;

/// <summary>Controls whether UO listeners accept encrypted and plaintext connections.</summary>
public enum NetworkEncryptionMode
{
    Disabled,
    Optional,
    Required
}
