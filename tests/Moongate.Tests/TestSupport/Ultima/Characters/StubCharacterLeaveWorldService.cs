using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Interfaces;

namespace Moongate.Tests.TestSupport.Ultima.Characters;

/// <summary>
///     A leave service whose pending saves finish when the test says so.
/// </summary>
public sealed class StubCharacterLeaveWorldService : ICharacterLeaveWorldService
{
    public TaskCompletionSource Pending { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public List<Serial> WaitedFor { get; } = [];

    public StubCharacterLeaveWorldService(bool idle = true)
    {
        if (idle)
        {
            Pending.SetResult();
        }
    }

    public Task WaitForAccountAsync(Serial account)
    {
        WaitedFor.Add(account);

        return Pending.Task;
    }
}
