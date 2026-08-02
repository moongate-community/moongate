using DryIoc;
using Moongate.Core.Extensions;
using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Interfaces.Chat;
using Moongate.Server.Abstractions.Interfaces.Items;
using Moongate.Server.Abstractions.Interfaces.Mobiles;
using Moongate.Server.Abstractions.Interfaces.World;
using Moongate.Server.Scripting;
using Moongate.Server.Scripting.Refs;
using Moongate.Server.Services.Items;
using Moongate.Server.Services.Mobiles;
using Moongate.Server.Services.World;
using Moongate.Tests.Support;
using SquidStd.Core.Data.Bootstrap;
using SquidStd.Core.Interfaces.Events;
using SquidStd.Persistence.Abstractions.Interfaces.Persistence;
using SquidStd.Scripting.Lua.Extensions.Scripts;
using SquidStd.Scripting.Lua.Interfaces.Scripts;
using SquidStd.Scripting.Lua.Services;
using SquidStd.Services.Core.Services;
using SquidStd.Services.Core.Services.Bootstrap;

namespace Moongate.Tests.Scripting;

/// <summary>
/// Exercises the handle through real Lua, which is where its two risks live: the dot call surviving
/// the marshaller, and the layer argument arriving as something ScriptEnums accepts. Neither is
/// visible to a C#-only test.
/// </summary>
public class MobileRefLuaTests
{
    [Fact]
    public async Task MobileRef_FromLua_ExposesTheThreeMethodsAndSpeaks()
    {
        var root = Path.Combine(Path.GetTempPath(), "mg-ref-lua-" + Guid.NewGuid().ToString("N"));
        var scripts = Path.Combine(root, "scripts");
        Directory.CreateDirectory(scripts);

        var persistence = new FakePersistenceService();
        var chat = new RecordingChatService();
        var mobile = new MobileEntity { Name = "Squid", MapId = 1, Position = new(1, 1, 0) };
        persistence.Store<MobileEntity>().UpsertAsync(mobile).WaitSync();

        var bootstrap = SquidStdBootstrap.Create(new SquidStdOptions { ConfigName = "moongate", RootDirectory = root });

        bootstrap.ConfigureServices(
            container =>
            {
                container.RegisterLuaEngine(new(root, scripts, "MoongateTests", "1.0.0"));

                // MobileModule takes eight dependencies; the module is registered rather than
                // hand-built so the test goes through the same resolution production does.
                var events = new EventBusService();
                var spatial = new SpatialIndexService(persistence, new StubLoopAffinity(), events);
                var items = new ItemService(persistence);
                var mobiles = new MobileService(persistence, spatial, events);

                container.RegisterInstance<IPersistenceService>(persistence);
                container.RegisterInstance<IEventBus>(events);
                container.RegisterInstance<ISpatialIndexService>(spatial);
                container.RegisterInstance<IItemService>(items);
                container.RegisterInstance<IMobileService>(mobiles);
                container.RegisterInstance<IChatService>(chat);
                container.RegisterInstance<IItemFactoryService>(new ItemFactoryService(new ItemTemplateService(), new(1)));
                container.RegisterInstance<IMobileFactoryService>(
                    new MobileFactoryService(
                        new StartingCityService(),
                        new MobileTemplateService(),
                        new(1),
                        new NameService()
                    )
                );

                // Registered the way production does, so the test exercises the real wiring.
                container.RegisterDelegate(
                    resolver => BuildFactory(resolver.Resolve<IScriptEngineService>(), persistence, chat),
                    Reuse.Singleton
                );

                container.RegisterScriptModule<MobileModule>();

                return container;
            }
        );

        await bootstrap.StartAsync();

        try
        {
            var engine = bootstrap.Resolve<IScriptEngineService>();
            var serial = mobile.Id.Value;

            // A handle for a serial nobody has is nil, and one for a real mobile is a table.
            Assert.Equal("nil", engine.ExecuteFunction("type(mobile.ref(57005))").Data);
            Assert.Equal("table", engine.ExecuteFunction($"type(mobile.ref({serial}))").Data);

            // The dot is the documented form -- the closures take no self. The colon happens to work
            // too, because MoonSharp's delegate adapter absorbs the extra leading argument rather than
            // failing on it; verified by hand, not relied upon.
            Assert.True(engine.ExecuteFunction($"mobile.ref({serial}).say('Welcome back.')").Data is true);
            Assert.Single(chat.Messages);

            // The layer arrives as a Lua string and must reach ScriptEnums intact. False because item
            // 1 does not exist -- what it proves is that the string crossed the marshaller.
            Assert.True(engine.ExecuteFunction($"mobile.ref({serial}).equip(1, 'Shirt')").Data is false);
        }
        finally
        {
            await bootstrap.StopAsync();
            TemporaryDirectory.Remove(root);
        }
    }

    private static MobileRefFactory BuildFactory(
        IScriptEngineService engine,
        FakePersistenceService persistence,
        RecordingChatService chat
    )
    {
        // The same unwrap the plugin performs: the MoonSharp Script belongs to the Lua engine.
        var script = engine is LuaScriptEngineService lua
                         ? lua.LuaScript
                         : throw new InvalidOperationException("This test requires the SquidStd Lua engine implementation.");

        var events = new EventBusService();
        var spatial = new SpatialIndexService(persistence, new StubLoopAffinity(), events);

        return new(
            script,
            persistence,
            chat,
            new MobileService(persistence, spatial, events),
            new ItemService(persistence)
        );
    }
}
