using System.Net;
using System.Net.Sockets;
using Moongate.Network.Packets.Data.Login;
using Moongate.Server.Core.Data.Realms;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Core.Types.Realms;
using Moongate.Server.Services.Realms.Internal;

namespace Moongate.Server.Services.Realms;

/// <summary>Thread-safe, process-local directory of remote leases and local realms.</summary>
public sealed class RealmDirectoryService : IRealmDirectoryService, IRealmCatalog
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, RealmDirectoryEntry> _remote = new(StringComparer.Ordinal);
    private readonly Dictionary<string, RealmDescriptor> _local = new(StringComparer.Ordinal);
    private readonly TimeProvider _clock;
    private readonly TimeSpan _leaseDuration;
    private readonly int _maxRealms;

    public RealmDirectoryService(TimeProvider clock, TimeSpan leaseDuration, int maxRealms = 128)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(leaseDuration, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxRealms, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(maxRealms, 128);
        _clock = clock;
        _leaseDuration = leaseDuration;
        _maxRealms = maxRealms;
    }

    public RealmLease Register(string peerId, RealmRegistration registration, Guid instanceId)
    {
        ArgumentNullException.ThrowIfNull(registration);
        if (instanceId == Guid.Empty)
        {
            throw new RealmDirectoryException(RealmRegistrationError.InvalidDescriptor, "Realm instance ID must be nonempty.");
        }

        if (!StringComparer.Ordinal.Equals(peerId, registration.RealmId))
        {
            throw new RealmDirectoryException(RealmRegistrationError.IdentityMismatch, "Realm identity does not match the authenticated peer.");
        }

        var descriptor = new RealmDescriptor(
            registration.RealmId, registration.ServerIndex, registration.Name, registration.Address,
            registration.Port, registration.MinimumAccountType
        );
        Validate(descriptor);

        lock (_gate)
        {
            RemoveExpired();
            if (_local.ContainsKey(descriptor.RealmId))
            {
                throw new RealmDirectoryException(RealmRegistrationError.InvalidDescriptor, "Realm ID is already registered locally.");
            }

            if (_remote.TryGetValue(descriptor.RealmId, out var previous))
            {
                if (!StringComparer.Ordinal.Equals(previous.PeerId, peerId))
                {
                    throw new RealmDirectoryException(RealmRegistrationError.IdentityMismatch, "Realm ID belongs to a different peer.");
                }

                if (previous.InstanceId == instanceId)
                {
                    if (!SameMetadata(previous.Descriptor, descriptor))
                    {
                        throw new RealmDirectoryException(RealmRegistrationError.InvalidDescriptor, "Realm metadata changed without a new instance ID.");
                    }

                    previous.LastRenewedTimestamp = _clock.GetTimestamp();
                    return new(previous.LeaseId);
                }
            }

            if (_remote.Values.Any(entry => entry.Descriptor.RealmId != descriptor.RealmId &&
                                            entry.Descriptor.ServerIndex == descriptor.ServerIndex) ||
                _local.Values.Any(entry => entry.ServerIndex == descriptor.ServerIndex))
            {
                throw new RealmDirectoryException(RealmRegistrationError.DuplicateIndex, "Realm server index is already registered.");
            }

            if (previous is null && _remote.Count + _local.Count >= _maxRealms)
            {
                throw new RealmDirectoryException(RealmRegistrationError.CapacityExceeded, "Realm directory capacity exceeded.");
            }

            var leaseId = Guid.NewGuid();
            _remote[descriptor.RealmId] = new(peerId, instanceId, leaseId, descriptor, _clock.GetTimestamp());
            return new(leaseId);
        }
    }

    public RealmRegistrationError Renew(string peerId, string realmId, Guid leaseId)
    {
        lock (_gate)
        {
            if (!_remote.TryGetValue(realmId, out var entry) || IsExpired(entry))
            {
                return RealmRegistrationError.ExpiredLease;
            }

            if (!StringComparer.Ordinal.Equals(entry.PeerId, peerId))
            {
                return RealmRegistrationError.IdentityMismatch;
            }

            if (entry.LeaseId != leaseId)
            {
                return RealmRegistrationError.StaleLease;
            }

            entry.LastRenewedTimestamp = _clock.GetTimestamp();
            return RealmRegistrationError.None;
        }
    }

    public bool Unregister(string peerId, string realmId, Guid leaseId)
    {
        lock (_gate)
        {
            if (!_remote.TryGetValue(realmId, out var entry) || IsExpired(entry) ||
                !StringComparer.Ordinal.Equals(entry.PeerId, peerId) || entry.LeaseId != leaseId)
            {
                return false;
            }

            return _remote.Remove(realmId);
        }
    }

    public IReadOnlyList<RealmDescriptor> GetAvailable(AccountType accountType)
    {
        if (!Enum.IsDefined(accountType))
        {
            return [];
        }

        lock (_gate)
        {
            RemoveExpired();
            return _local.Values.Concat(_remote.Values.Select(entry => entry.Descriptor))
                         .Where(realm => accountType >= realm.MinimumAccountType)
                         .OrderBy(realm => realm.ServerIndex)
                         .ToArray();
        }
    }

    public ValueTask<IReadOnlyList<RealmDescriptor>> GetAvailableAsync(
        AccountType accountType,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        return ValueTask.FromResult(GetAvailable(accountType));
    }

    public ValueTask<RealmInstance?> FindByIndexAsync(
        ushort index,
        AccountType accountType,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        var descriptor = GetAvailable(accountType).FirstOrDefault(realm => realm.ServerIndex == index);

        return ValueTask.FromResult(descriptor is null ? null : new RealmInstance(descriptor, Guid.Empty));
    }

    public void RegisterLocal(RealmDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        Validate(descriptor);
        lock (_gate)
        {
            RemoveExpired();
            if (_remote.ContainsKey(descriptor.RealmId) || _local.ContainsKey(descriptor.RealmId))
            {
                throw new InvalidOperationException("Realm ID is already registered.");
            }

            if (_remote.Values.Any(entry => entry.Descriptor.ServerIndex == descriptor.ServerIndex) ||
                _local.Values.Any(entry => entry.ServerIndex == descriptor.ServerIndex))
            {
                throw new InvalidOperationException("Realm server index is already registered.");
            }

            if (_remote.Count + _local.Count >= _maxRealms)
            {
                throw new InvalidOperationException("Realm directory capacity exceeded.");
            }

            _local.Add(descriptor.RealmId, descriptor);
        }
    }

    private bool IsExpired(RealmDirectoryEntry entry)
        => _clock.GetElapsedTime(entry.LastRenewedTimestamp) >= _leaseDuration;

    private void RemoveExpired()
    {
        foreach (var id in _remote.Where(entry => IsExpired(entry.Value)).Select(entry => entry.Key).ToArray())
        {
            _remote.Remove(id);
        }
    }

    private static bool SameMetadata(RealmDescriptor first, RealmDescriptor second)
        => first.ServerIndex == second.ServerIndex && first.Name == second.Name &&
           first.Address.Equals(second.Address) && first.Port == second.Port &&
           first.MinimumAccountType == second.MinimumAccountType;

    private static void Validate(RealmDescriptor descriptor)
    {
        if (string.IsNullOrWhiteSpace(descriptor.RealmId) || descriptor.RealmId.Length > 128)
        {
            throw new InvalidOperationException("Realm ID must be 1 to 128 characters.");
        }

        if (descriptor.Address.AddressFamily != AddressFamily.InterNetwork ||
            descriptor.Address.Equals(IPAddress.Any) || descriptor.Address.GetAddressBytes()[0] >= 224)
        {
            throw new InvalidOperationException("Realm address must be a client-facing IPv4 literal.");
        }

        if (descriptor.Port == 0 || !Enum.IsDefined(descriptor.MinimumAccountType))
        {
            throw new InvalidOperationException("Realm port or minimum account type is invalid.");
        }

        _ = new GameServerEntry(descriptor.ServerIndex, descriptor.Name, 0, 0, descriptor.Address);
    }
}
