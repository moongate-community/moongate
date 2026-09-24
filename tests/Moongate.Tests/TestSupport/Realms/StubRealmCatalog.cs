using Moongate.Server.Core.Data.Realms;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Types.Accounts;

namespace Moongate.Tests.TestSupport.Realms;

internal sealed class StubRealmCatalog : IRealmCatalog
{
    private readonly RealmInstance[] _realms;

    public StubRealmCatalog(params RealmDescriptor[] descriptors)
    {
        _realms = descriptors.Select(descriptor => new RealmInstance(descriptor, Guid.NewGuid())).ToArray();
    }

    public ValueTask<IReadOnlyList<RealmDescriptor>> GetAvailableAsync(
        AccountType accountType,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<RealmDescriptor> realms = _realms
                                                .Where(realm => accountType >= realm.Descriptor.MinimumAccountType)
                                                .Select(realm => realm.Descriptor)
                                                .OrderBy(realm => realm.ServerIndex)
                                                .ToArray();

        return ValueTask.FromResult(realms);
    }

    public ValueTask<RealmInstance?> FindByIndexAsync(
        ushort index,
        AccountType accountType,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        return ValueTask.FromResult(_realms.FirstOrDefault(
            realm => realm.Descriptor.ServerIndex == index && accountType >= realm.Descriptor.MinimumAccountType
        ));
    }
}
