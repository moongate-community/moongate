using System.Net;
using System.Net.Sockets;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Core.Types.Hosting;

namespace Moongate.Server.Data.Config.Sections;

/// <summary>
///     Configures the realm advertised through Redis discovery.
/// </summary>
public sealed class RealmDirectoryConfig
{
    public string RealmId { get; set; } = "";

    public string Name { get; set; } = "";

    public int ServerIndex { get; set; }

    public string AdvertisedAddress { get; set; } = "";

    public int AdvertisedPort { get; set; }

    public AccountType MinimumAccountType { get; set; } = AccountType.Regular;

    public int HeartbeatIntervalSeconds { get; set; } = 5;

    public int LeaseDurationSeconds { get; set; } = 15;

    public int MaxRealms { get; set; } = 128;

    public void Validate(ServerMode mode)
    {
        if (HeartbeatIntervalSeconds < 1)
        {
            throw new InvalidOperationException("realm_directory.heartbeat_interval_seconds must be positive.");
        }

        if (LeaseDurationSeconds <= 2 * (long)HeartbeatIntervalSeconds)
        {
            throw new InvalidOperationException(
                "realm_directory.lease_duration_seconds must exceed two heartbeat intervals."
            );
        }

        if (MaxRealms is < 1 or > 128)
        {
            throw new InvalidOperationException("realm_directory.max_realms must be between 1 and 128.");
        }

        if (!Enum.IsDefined(MinimumAccountType))
        {
            throw new InvalidOperationException("realm_directory.minimum_account_type is invalid.");
        }

        if (mode != ServerMode.Game)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(RealmId))
        {
            throw new InvalidOperationException("realm_directory.realm_id is required in game mode.");
        }

        if (string.IsNullOrWhiteSpace(Name) || Name.Length > 32 || Name.Any(character => character is < ' ' or > '~'))
        {
            throw new InvalidOperationException("realm_directory.name must be printable ASCII of at most 32 characters.");
        }

        if (ServerIndex is < 0 or > ushort.MaxValue)
        {
            throw new InvalidOperationException("realm_directory.server_index must fit a ushort.");
        }

        if (!IPAddress.TryParse(AdvertisedAddress, out var address) ||
            address.AddressFamily != AddressFamily.InterNetwork ||
            address.Equals(IPAddress.Any) ||
            address.GetAddressBytes()[0] is >= 224)
        {
            throw new InvalidOperationException("realm_directory.advertised_address must be a client-facing IPv4 address.");
        }

        if (AdvertisedPort is < 1 or > 65535)
        {
            throw new InvalidOperationException("realm_directory.advertised_port must be between 1 and 65535.");
        }
    }
}
