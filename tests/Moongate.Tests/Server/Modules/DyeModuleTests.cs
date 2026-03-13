using Moongate.Server.Data.Internal.Entities;
using Moongate.Server.Interfaces.Services.Interaction;
using Moongate.Server.Modules;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using MoonSharp.Interpreter;

namespace Moongate.Tests.Server.Modules;

public sealed class DyeModuleTests
{
    private sealed class DyeModuleTestService : IDyeColorService
    {
        public Func<UOItemEntity, bool>? LastCallback { get; private set; }
        public long LastSessionId { get; private set; }
        public Serial LastDyeTubSerial { get; private set; }
        public Serial LastItemSerial { get; private set; }

        public Task StartAsync()
            => Task.CompletedTask;

        public Task StopAsync()
            => Task.CompletedTask;

        public Task<bool> BeginAsync(long sessionId, Serial dyeTubSerial, Func<UOItemEntity, bool>? targetSelectedCallback = null)
        {
            LastSessionId = sessionId;
            LastDyeTubSerial = dyeTubSerial;
            LastCallback = targetSelectedCallback;

            return Task.FromResult(true);
        }

        public Task<bool> HandleResponseAsync(Moongate.Server.Data.Session.GameSession session, Moongate.Network.Packets.Incoming.Interaction.DyeWindowPacket packet)
            => Task.FromResult(true);

        public Task<bool> SendDyeableAsync(long sessionId, Serial itemSerial, ushort model = 4011)
        {
            LastSessionId = sessionId;
            LastItemSerial = itemSerial;

            return Task.FromResult(true);
        }
    }

    [Test]
    public void Begin_WithLuaClosure_ShouldWrapCallback()
    {
        var service = new DyeModuleTestService();
        var module = new DyeModule(service);
        var script = new Script();
        script.DoString("selected = 0");
        var callback = script.DoString("return function(target_serial) selected = target_serial return true end").Function;

        var ok = module.Begin(42, 0x40000001u, callback);
        var accepted = service.LastCallback!(new UOItemEntity { Id = (Serial)0x40000009u, Name = "boots" });

        Assert.Multiple(
            () =>
            {
                Assert.That(ok, Is.True);
                Assert.That(accepted, Is.True);
                Assert.That(service.LastSessionId, Is.EqualTo(42));
                Assert.That(service.LastDyeTubSerial, Is.EqualTo((Serial)0x40000001u));
                Assert.That(script.Globals.Get("selected").Number, Is.EqualTo(0x40000009));
            }
        );
    }

    [Test]
    public void SendDyeable_ShouldForwardToService()
    {
        var service = new DyeModuleTestService();
        var module = new DyeModule(service);

        var ok = module.SendDyeable(43, 0x40000010u);

        Assert.Multiple(
            () =>
            {
                Assert.That(ok, Is.True);
                Assert.That(service.LastSessionId, Is.EqualTo(43));
                Assert.That(service.LastItemSerial, Is.EqualTo((Serial)0x40000010u));
            }
        );
    }
}
