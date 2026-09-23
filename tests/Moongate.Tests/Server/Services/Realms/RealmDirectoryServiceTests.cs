using System.Net;
using Moongate.Server.Core.Data.Realms;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Core.Types.Realms;
using Moongate.Server.Services.Realms;
using Moongate.Server.Services.Realms.Internal;
using Moongate.Tests.Support.Timing;

namespace Moongate.Tests.Server.Services.Realms;

public sealed class RealmDirectoryServiceTests
{
    [Fact]
    public void Register_SameInstanceAndMetadata_IsIdempotent()
    {
        var directory = Create();
        var instance = Guid.NewGuid();
        var registration = Registration("realm-a", 1);

        var first = directory.Register("realm-a", registration, instance);
        var second = directory.Register("realm-a", registration, instance);

        Assert.Equal(first.LeaseId, second.LeaseId);
        Assert.Single(directory.GetAvailable(AccountType.Regular));
    }

    [Fact]
    public void Register_NewInstanceReplacesOldGeneration()
    {
        var directory = Create();
        var registration = Registration("realm-a", 1);
        var first = directory.Register("realm-a", registration, Guid.NewGuid());
        var second = directory.Register("realm-a", registration, Guid.NewGuid());

        Assert.NotEqual(first.LeaseId, second.LeaseId);
        Assert.Equal(RealmRegistrationError.StaleLease, directory.Renew("realm-a", "realm-a", first.LeaseId));
        Assert.False(directory.Unregister("realm-a", "realm-a", first.LeaseId));
        Assert.Equal(RealmRegistrationError.None, directory.Renew("realm-a", "realm-a", second.LeaseId));
        Assert.Single(directory.GetAvailable(AccountType.Regular));
    }

    [Fact]
    public void Register_SameInstanceWithChangedMetadata_RejectsWithoutRenewing()
    {
        var clock = new ManualTimeProvider();
        var directory = Create(clock);
        var instance = Guid.NewGuid();
        var lease = directory.Register("realm-a", Registration("realm-a", 1), instance);
        clock.Advance(TimeSpan.FromSeconds(10));

        Assert.Equal(RealmRegistrationError.InvalidDescriptor,
            Assert.Throws<RealmDirectoryException>(() =>
                directory.Register("realm-a", Registration("realm-a", 2), instance)).Error);
        clock.Advance(TimeSpan.FromSeconds(5));

        Assert.Empty(directory.GetAvailable(AccountType.Regular));
        Assert.Equal(RealmRegistrationError.ExpiredLease, directory.Renew("realm-a", "realm-a", lease.LeaseId));
    }

    [Fact]
    public void Register_RejectsOtherPeerAndDuplicateIndexWithoutMutation()
    {
        var directory = Create();
        var first = directory.Register("realm-a", Registration("realm-a", 1), Guid.NewGuid());

        Assert.Equal(RealmRegistrationError.IdentityMismatch,
            Assert.Throws<RealmDirectoryException>(() =>
                directory.Register("realm-b", Registration("realm-a", 1), Guid.NewGuid())).Error);
        Assert.Equal(RealmRegistrationError.DuplicateIndex,
            Assert.Throws<RealmDirectoryException>(() =>
                directory.Register("realm-b", Registration("realm-b", 1), Guid.NewGuid())).Error);

        Assert.Equal(RealmRegistrationError.None, directory.Renew("realm-a", "realm-a", first.LeaseId));
        Assert.Equal("realm-a", Assert.Single(directory.GetAvailable(AccountType.Regular)).RealmId);
    }

    [Fact]
    public void GetAvailable_FiltersByAccountTypeAndSortsByStableIndex()
    {
        var directory = Create();
        directory.Register("realm-c", Registration("realm-c", 4, AccountType.Administrator), Guid.NewGuid());
        directory.Register("realm-a", Registration("realm-a", 2), Guid.NewGuid());
        directory.Register("realm-b", Registration("realm-b", 1, AccountType.GameMaster), Guid.NewGuid());

        Assert.Equal([2], directory.GetAvailable(AccountType.Regular).Select(realm => realm.ServerIndex));
        Assert.Equal([1, 2, 4], directory.GetAvailable(AccountType.Administrator)
            .Select(realm => realm.ServerIndex));
    }

    [Fact]
    public void GetAvailable_OmitsExpiredLeaseWithoutCleanupTimer()
    {
        var clock = new ManualTimeProvider();
        var directory = Create(clock);
        var lease = directory.Register("realm-a", Registration("realm-a", 1), Guid.NewGuid());
        clock.Advance(TimeSpan.FromSeconds(15));

        Assert.Empty(directory.GetAvailable(AccountType.Regular));
        Assert.Equal(RealmRegistrationError.ExpiredLease, directory.Renew("realm-a", "realm-a", lease.LeaseId));
        directory.Register("realm-b", Registration("realm-b", 1), Guid.NewGuid());
        Assert.Single(directory.GetAvailable(AccountType.Regular));
    }

    [Fact]
    public void Register_CapacityRejectsWithoutChangingExistingLease()
    {
        var directory = Create(maxRealms: 1);
        var first = directory.Register("realm-a", Registration("realm-a", 1), Guid.NewGuid());

        Assert.Equal(RealmRegistrationError.CapacityExceeded,
            Assert.Throws<RealmDirectoryException>(() =>
                directory.Register("realm-b", Registration("realm-b", 2), Guid.NewGuid())).Error);
        Assert.Equal(RealmRegistrationError.None, directory.Renew("realm-a", "realm-a", first.LeaseId));
    }

    [Fact]
    public void Register_DefaultCapacityStopsAt128()
    {
        var directory = Create();
        for (ushort index = 0; index < 128; index++)
        {
            var id = $"realm-{index}";
            directory.Register(id, Registration(id, index), Guid.NewGuid());
        }

        Assert.Equal(128, directory.GetAvailable(AccountType.Regular).Count);
        Assert.Equal(RealmRegistrationError.CapacityExceeded,
            Assert.Throws<RealmDirectoryException>(() =>
                directory.Register("realm-overflow", Registration("realm-overflow", 128), Guid.NewGuid())).Error);
    }

    [Fact]
    public void RegisterLocal_RemainsAvailableWithoutLease()
    {
        var clock = new ManualTimeProvider();
        var directory = Create(clock);
        directory.RegisterLocal(new RealmDescriptor("local", 1, "Local", IPAddress.Loopback, 2593,
            AccountType.Regular));
        clock.Advance(TimeSpan.FromDays(2));

        Assert.Single(directory.GetAvailable(AccountType.Regular));
    }

    [Fact]
    public void GetAvailable_ReturnsDefensiveSnapshot()
    {
        var directory = Create();
        directory.Register("realm-a", Registration("realm-a", 1), Guid.NewGuid());
        var snapshot = directory.GetAvailable(AccountType.Regular);
        var address = snapshot[0].Address;
        address.Address = 0;

        Assert.Equal(IPAddress.Loopback, directory.GetAvailable(AccountType.Regular)[0].Address);
    }

    private static RealmDirectoryService Create(ManualTimeProvider? clock = null, int maxRealms = 128)
        => new(clock ?? new ManualTimeProvider(), TimeSpan.FromSeconds(15), maxRealms);

    private static RealmRegistration Registration(string id, ushort index, AccountType minimum = AccountType.Regular)
        => new(id, index, id, IPAddress.Loopback, 2593, minimum);
}
