using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Modules;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;
using Moongate.UO.Data.Utils;

namespace Moongate.Tests.Server.Modules;

public sealed class EffectModuleTests
{
    [Test]
    public void Send_ShouldForwardToDispatchService()
    {
        var dispatch = new EffectModuleTestDispatchEventsService { BroadcastResult = 3 };
        var module = new EffectModule(dispatch);

        var recipients = module.Send(1, 100, 200, 7, 0x3728, effect: 2023);

        Assert.Multiple(
            () =>
            {
                Assert.That(recipients, Is.EqualTo(3));
                Assert.That(dispatch.LastBroadcastMapId, Is.EqualTo(1));
                Assert.That(dispatch.LastBroadcastLocation, Is.EqualTo(new Point3D(100, 200, 7)));
                Assert.That(dispatch.LastBroadcastItemId, Is.EqualTo(0x3728));
                Assert.That(dispatch.LastBroadcastEffect, Is.EqualTo(2023));
            }
        );
    }

    [Test]
    public void SendToPlayer_ShouldForwardToDispatchService()
    {
        var dispatch = new EffectModuleTestDispatchEventsService { UnicastResult = true };
        var module = new EffectModule(dispatch);

        var sent = module.SendToPlayer((uint)0x22, 10, 20, 3, 0x3728, effect: 5023);

        Assert.Multiple(
            () =>
            {
                Assert.That(sent, Is.True);
                Assert.That(dispatch.LastCharacterId, Is.EqualTo((Serial)0x22));
                Assert.That(dispatch.LastUnicastLocation, Is.EqualTo(new Point3D(10, 20, 3)));
                Assert.That(dispatch.LastUnicastItemId, Is.EqualTo(0x3728));
                Assert.That(dispatch.LastUnicastEffect, Is.EqualTo(5023));
            }
        );
    }

    private sealed class EffectModuleTestDispatchEventsService : IDispatchEventsService
    {
        public int BroadcastResult { get; set; } = 1;

        public bool UnicastResult { get; set; } = true;

        public int LastBroadcastMapId { get; private set; }

        public Point3D LastBroadcastLocation { get; private set; } = Point3D.Zero;

        public ushort LastBroadcastItemId { get; private set; }

        public ushort LastBroadcastEffect { get; private set; }

        public Serial LastCharacterId { get; private set; }

        public Point3D LastUnicastLocation { get; private set; } = Point3D.Zero;

        public ushort LastUnicastItemId { get; private set; }

        public ushort LastUnicastEffect { get; private set; }

        public Task<int> DispatchMobileUpdateAsync(
            UOMobileEntity mobile,
            int mapId,
            int range,
            bool isNew,
            bool stygianAbyss = true,
            CancellationToken cancellationToken = default
        )
            => Task.FromResult(0);

        public Task<int> DispatchMobileSpeechAsync(
            UOMobileEntity speaker,
            string text,
            int range,
            ChatMessageType messageType = ChatMessageType.Regular,
            short hue = SpeechHues.Default,
            short font = SpeechHues.DefaultFont,
            string language = "ENU",
            CancellationToken cancellationToken = default
        )
            => Task.FromResult(0);

        public Task<int> DispatchMobileSoundAsync(
            int mapId,
            Point3D location,
            ushort soundModel,
            byte mode = 1,
            ushort unknown3 = 0,
            int? range = null,
            CancellationToken cancellationToken = default
        )
            => Task.FromResult(0);

        public Task<bool> DispatchSoundToPlayerAsync(
            Serial characterId,
            Point3D location,
            ushort soundModel,
            byte mode = 1,
            ushort unknown3 = 0,
            CancellationToken cancellationToken = default
        )
            => Task.FromResult(false);

        public Task<int> DispatchMobileEffectAsync(
            int mapId,
            Point3D location,
            ushort itemId,
            byte speed = 10,
            byte duration = 10,
            int hue = 0,
            int renderMode = 0,
            ushort effect = 0,
            ushort explodeEffect = 0,
            ushort explodeSound = 0,
            byte layer = 255,
            ushort unknown3 = 0,
            int? range = null,
            CancellationToken cancellationToken = default
        )
        {
            _ = speed;
            _ = duration;
            _ = hue;
            _ = renderMode;
            _ = explodeEffect;
            _ = explodeSound;
            _ = layer;
            _ = unknown3;
            _ = range;
            _ = cancellationToken;
            LastBroadcastMapId = mapId;
            LastBroadcastLocation = location;
            LastBroadcastItemId = itemId;
            LastBroadcastEffect = effect;

            return Task.FromResult(BroadcastResult);
        }

        public Task<bool> DispatchEffectToPlayerAsync(
            Serial characterId,
            Point3D location,
            ushort itemId,
            byte speed = 10,
            byte duration = 10,
            int hue = 0,
            int renderMode = 0,
            ushort effect = 0,
            ushort explodeEffect = 0,
            ushort explodeSound = 0,
            byte layer = 255,
            ushort unknown3 = 0,
            CancellationToken cancellationToken = default
        )
        {
            _ = speed;
            _ = duration;
            _ = hue;
            _ = renderMode;
            _ = explodeEffect;
            _ = explodeSound;
            _ = layer;
            _ = unknown3;
            _ = cancellationToken;
            LastCharacterId = characterId;
            LastUnicastLocation = location;
            LastUnicastItemId = itemId;
            LastUnicastEffect = effect;

            return Task.FromResult(UnicastResult);
        }
    }
}
