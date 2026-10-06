using System.Net;
using Moongate.Server.Core.Data.Realms;
using Moongate.Server.Core.Types.Accounts;
using Moongate.Server.Core.Types.Hosting;
using Moongate.Server.Services.Admin;
using Moongate.Server.Services.Sessions;
using Moongate.Server.Ultima.Data.Motd;
using Moongate.Server.Ultima.Services.Books;
using Moongate.Tests.TestSupport.Scripting;

namespace Moongate.Tests.TestSupport.Ultima.Books;

public static class TestBookContexts
{
    public static BookContextFactory Create()
    {
        var loop = new StubGameLoop { IsOnLoopThread = false };
        var realm = new RealmInstance(
            new RealmDescriptor("local", 0, "Felucca", IPAddress.Loopback, 2593, AccountType.Regular),
            Guid.NewGuid()
        );
        return new(
            new SessionService(loop),
            new AdminServerInfoProvider(ServerMode.Game, realm),
            realm,
            new MotdServerIdentity("Moongate"),
            loop
        );
    }
}
