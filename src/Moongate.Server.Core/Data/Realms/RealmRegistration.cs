using System.Net;
using Moongate.Server.Core.Types.Accounts;

namespace Moongate.Server.Core.Data.Realms;

/// <summary>Metadata a game process offers for discovery.</summary>
public sealed class RealmRegistration
{
    public string RealmId { get; }

    public ushort ServerIndex { get; }

    public string Name { get; }

    public IPAddress Address { get; }

    public ushort Port { get; }

    public AccountType MinimumAccountType { get; }

    public RealmRegistration(
        string realmId,
        ushort serverIndex,
        string name,
        IPAddress address,
        ushort port,
        AccountType minimumAccountType
    )
    {
        RealmId = realmId;
        ServerIndex = serverIndex;
        Name = name;
        Address = new IPAddress(address.GetAddressBytes());
        Port = port;
        MinimumAccountType = minimumAccountType;
    }
}
