using Moongate.Server.Core.Data.Realms;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Types.Accounts;

namespace Moongate.Tests.TestSupport.Realms;

internal sealed class ControlledRealmCatalog : IRealmCatalog
{
    public RealmInstance? Result { get; set; }
    public TaskCompletionSource<RealmInstance?>? PendingResult { get; set; }
    public Exception? Failure { get; set; }
    public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public ushort? RequestedIndex { get; private set; }
    public AccountType? RequestedAccountType { get; private set; }

    public ValueTask<IReadOnlyList<RealmDescriptor>> GetAvailableAsync(
        AccountType accountType,
        CancellationToken cancellationToken = default
    )
    {
        return ValueTask.FromResult<IReadOnlyList<RealmDescriptor>>(Result is null ? [] : [Result.Descriptor]);
    }

    public async ValueTask<RealmInstance?> FindByIndexAsync(
        ushort index,
        AccountType accountType,
        CancellationToken cancellationToken = default
    )
    {
        RequestedIndex = index;
        RequestedAccountType = accountType;
        Entered.TrySetResult();

        if (Failure is not null)
        {
            throw Failure;
        }

        return PendingResult is null
                   ? Result
                   : await PendingResult.Task.WaitAsync(cancellationToken);
    }
}
