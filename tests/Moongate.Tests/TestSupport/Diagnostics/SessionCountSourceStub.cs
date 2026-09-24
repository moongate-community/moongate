using System.Diagnostics.CodeAnalysis;
using Moongate.Core.Primitives;
using Moongate.Network.Interfaces.Client;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Services;

namespace Moongate.Tests.TestSupport.Diagnostics;

internal sealed class SessionCountSourceStub : ISessionService
{
    private int _count;

    public int Count
    {
        get
        {
            CountReadCount++;

            return _count;
        }
        set => _count = value;
    }

    public int CountReadCount { get; private set; }

    public SessionCountSourceStub(int count)
    {
        _count = count;
    }

    public void Clear()
        => throw new NotSupportedException();

    public IReadOnlyCollection<GameSession> GetAll()
        => throw new NotSupportedException();

    public GameSession GetOrCreate(INetworkConnection client)
        => throw new NotSupportedException();

    public bool Remove(long sessionId)
        => throw new NotSupportedException();

    public bool TryGet(long sessionId, [NotNullWhen(true)] out GameSession? session)
        => throw new NotSupportedException();

    public bool TryGetByCharacterId(Serial characterId, [NotNullWhen(true)] out GameSession? session)
        => throw new NotSupportedException();
}
