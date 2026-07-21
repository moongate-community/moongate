using Moongate.Server.Abstractions.Data.Stats;
using Moongate.Server.Abstractions.Interfaces.Server;

namespace Moongate.Tests.Support;

/// <summary>
/// Test double for <see cref="IServerStatsService" />: lets a test state exactly which snapshot the
/// endpoint serves, so the route's behaviour is tested without a game loop or a timer.
/// </summary>
public sealed class StubServerStatsService : IServerStatsService
{
    public ServerStatsSnapshot Current { get; set; } = ServerStatsSnapshot.Empty;

    public void Refresh() { }
}
