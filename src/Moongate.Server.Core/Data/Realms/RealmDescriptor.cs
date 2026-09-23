using System.Net;
using Moongate.Server.Core.Types.Accounts;

namespace Moongate.Server.Core.Data.Realms;

/// <summary>An immutable client-facing realm snapshot.</summary>
public sealed class RealmDescriptor
{
    private readonly byte[] _addressBytes;

    public string RealmId { get; }

    public ushort ServerIndex { get; }

    public string Name { get; }

    public IPAddress Address => new(_addressBytes);

    public ushort Port { get; }

    public AccountType MinimumAccountType { get; }

    public RealmDescriptor(
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
        _addressBytes = address.GetAddressBytes().ToArray();
        Port = port;
        MinimumAccountType = minimumAccountType;
    }
}
