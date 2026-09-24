using Moongate.Server.Core.Data.Realms;
using Moongate.Server.Core.Interfaces.Services;

namespace Moongate.Tests.TestSupport.Realms;

internal sealed class RecordingGameHandoffStore : IGameHandoffStore
{
    private readonly List<(string RealmId, uint AuthKey)> _revoked = [];

    public IReadOnlyList<(string RealmId, uint AuthKey)> Revoked => _revoked;
    public PendingHandoff? IssuedHandoff { get; private set; }
    public ReadOnlyMemory<byte> IssuedKeyBuffer { get; private set; }
    public byte[]? IssuedKeySnapshot { get; private set; }
    public uint NextAuthKey { get; set; } = 0x01020304;
    public Exception? IssueFailure { get; set; }
    public Task? IssueGate { get; set; }
    public TaskCompletionSource IssueEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public int IssueCount { get; private set; }

    public async ValueTask<uint> IssueAsync(
        PendingHandoff handoff,
        ReadOnlyMemory<byte> credentialKey,
        CancellationToken token = default
    )
    {
        IssueCount++;
        IssuedHandoff = handoff;
        IssuedKeyBuffer = credentialKey;
        IssuedKeySnapshot = credentialKey.ToArray();
        IssueEntered.TrySetResult();

        if (IssueGate is not null)
        {
            await IssueGate.WaitAsync(token);
        }

        if (IssueFailure is not null)
        {
            throw IssueFailure;
        }

        return NextAuthKey;
    }

    public ValueTask<PendingHandoff?> RedeemAsync(
        string realmId,
        Guid instanceId,
        uint authKey,
        string username,
        string password,
        CancellationToken token = default
    )
        => throw new NotSupportedException();

    public ValueTask RevokeAsync(string realmId, uint authKey, CancellationToken token = default)
    {
        _revoked.Add((realmId, authKey));

        return ValueTask.CompletedTask;
    }
}
