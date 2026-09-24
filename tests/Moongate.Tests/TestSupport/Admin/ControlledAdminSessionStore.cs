using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Admin;
using Moongate.Server.Core.Interfaces.Admin;

namespace Moongate.Tests.TestSupport.Admin;

internal sealed class ControlledAdminSessionStore : IAdminSessionStore
{
    private readonly IAdminSessionStore _inner;
    public Func<Task>? BeforeIssue { get; set; }
    public Func<Task>? AfterReset { get; set; }
    public Func<Task>? BeforeOpen { get; set; }
    public string? LastDigest { get; private set; }
    public int IssuedCount { get; private set; }

    public ControlledAdminSessionStore(IAdminSessionStore inner) { _inner = inner; }
    public Task<AdminAccountGate?> ReadGateAsync(Serial id, CancellationToken token = default) => _inner.ReadGateAsync(id, token);
    public async Task<AdminAccountGate> ResetGateAsync(Serial id, bool blocked, CancellationToken token = default)
    {
        var gate = await _inner.ResetGateAsync(id, blocked, token);
        if (AfterReset is not null) { await AfterReset(); }
        return gate;
    }
    public async Task<bool> TryOpenGateAsync(Serial id, Guid generation, CancellationToken token = default)
    {
        if (BeforeOpen is not null) { await BeforeOpen(); }
        return await _inner.TryOpenGateAsync(id, generation, token);
    }
    public async Task<AdminSession> IssueAsync(AdminIdentity identity, Guid generation, string hash, TimeSpan lifetime, CancellationToken token = default)
    {
        if (BeforeIssue is not null) { await BeforeIssue(); }
        LastDigest = hash;
        var session = await _inner.IssueAsync(identity, generation, hash, lifetime, token);
        IssuedCount++;
        return session;
    }
    public Task<AdminSession?> FindAsync(string hash, CancellationToken token = default) => _inner.FindAsync(hash, token);
    public Task RemoveAsync(string hash, CancellationToken token = default) => _inner.RemoveAsync(hash, token);
}
