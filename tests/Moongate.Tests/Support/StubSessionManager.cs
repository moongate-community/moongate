using Moongate.Core.Primitives;
using Moongate.Server.Abstractions.Data.Session;
using Moongate.Server.Abstractions.Interfaces.Accounts;
using SquidStd.Network.Client;

namespace Moongate.Tests.Support;

/// <summary>
/// Test double for <see cref="ISessionManager" />: holds no real sessions — which need a live socket —
/// and instead lets a test declare which characters count as played via <see cref="Played" />.
/// </summary>
public sealed class StubSessionManager : ISessionManager
{
    private int _count;

    /// <summary>Characters a session is pretending to play.</summary>
    public HashSet<Serial> Played { get; } = [];

    /// <summary>When set, reading <see cref="Count" /> throws, so a caller's failure handling can be exercised.</summary>
    public bool ThrowOnCount { get; set; }

    /// <summary>Connections the stub reports, including clients that have not entered the world.</summary>
    public int Count
    {
        get => ThrowOnCount ? throw new InvalidOperationException("session count unavailable") : _count;
        set => _count = value;
    }

    public IReadOnlyCollection<PlayerSession> All => [];

    public PlayerSession GetOrCreate(SquidStdTcpClient client)
        => throw new NotSupportedException("The stub holds no sessions.");

    public bool IsCharacterPlayed(Serial mobileId)
        => Played.Contains(mobileId);

    public void Remove(long sessionId) { }

    public bool TryGet(long sessionId, out PlayerSession session)
    {
        session = null!;

        return false;
    }
}
