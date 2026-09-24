using DryIoc;
using Moongate.Core.Directories;
using Moongate.Persistence.Extensions;
using Moongate.Persistence.Interfaces;
using Moongate.Persistence.Services;
using Moongate.Persistence.Types.Persistence;
using Moongate.Server.Core.Data.Admin;
using Moongate.Server.Ultima;
using Moongate.Server.Ultima.Entities.Auth;
using Moongate.Server.Ultima.Services;
using Moongate.Tests.TestSupport.Persistence;
using Moongate.Tests.TestSupport.Server.Ultima;

namespace Moongate.Tests.TestSupport.Admin;

internal sealed class AccountAdminFixture : IAsyncDisposable
{
    private readonly Container _peer;
    private readonly TemporaryPersistenceDirectory _directory;
    public AccountServiceFixture Accounts { get; }
    public AdminRedisFixture Redis { get; }
    public ControlledAdminSessionStore Store { get; }
    public AccountAdminAccessService Authority { get; }
    public AccountAdminAccessService PeerAuthority { get; }

    private AccountAdminFixture(AccountServiceFixture accounts, AdminRedisFixture redis, Container peer, TemporaryPersistenceDirectory directory)
    {
        Accounts = accounts;
        Redis = redis;
        _peer = peer;
        _directory = directory;
        Store = new(redis.Store);
        Authority = new(accounts.Persistence, accounts.Accounts, Store, new AdminSessionOptions(TimeSpan.FromMinutes(30)));
        PeerAuthority = new(peer.Resolve<MoongatePersistenceService>(), peer.Resolve<IDataAccess<AccountEntity>>(), redis.Store, new AdminSessionOptions(TimeSpan.FromMinutes(30)));
    }

    public static async Task<AccountAdminFixture> CreateAsync()
    {
        var accounts = await AccountServiceFixture.CreateAsync();
        var redis = await AdminRedisFixture.CreateAsync();
        var directory = new TemporaryPersistenceDirectory();
        var peer = new Container();
        peer.RegisterInstance(new DirectoriesConfig(directory.Path, []));
        peer.RegisterMoongatePersistence(new([new(PersistenceDatabaseTarget.Accounts, accounts.Database.ConnectionString)], false));
        new MoongateUltimaPlugin().Register(peer);
        await peer.Resolve<MoongatePersistenceService>().InitializeAsync();
        return new(accounts, redis, peer, directory);
    }

    public async ValueTask DisposeAsync()
    {
        await _peer.Resolve<MoongatePersistenceService>().DisposeAsync();
        _peer.Dispose();
        _directory.Dispose();
        await Redis.DisposeAsync();
        await Accounts.DisposeAsync();
    }
}
