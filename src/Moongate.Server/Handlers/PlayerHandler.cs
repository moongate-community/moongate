using Moongate.Network.Packets.Incoming.System;
using Moongate.Network.Packets.Interfaces;
using Moongate.Network.Packets.Outgoing.Speech;
using Moongate.Network.Packets.Outgoing.World;
using Moongate.Server.Attributes;
using Moongate.Server.Data.Events.Spatial;
using Moongate.Server.Data.Session;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Interfaces.Services.Packets;
using Moongate.Server.Interfaces.Services.Sessions;
using Moongate.Server.Interfaces.Services.Spatial;
using Moongate.Server.Listeners.Base;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Json.Regions;
using Moongate.UO.Data.Utils;
using Serilog;

namespace Moongate.Server.Handlers;

[RegisterGameEventListener, RegisterPacketHandler(0xD9)]
public class PlayerHandler : BasePacketListener, IGameEventListener<PlayerEnteredRegionEvent>, IGameEventListener<PlayerExitedRegionEvent>
{
    private readonly ILogger _logger = Log.ForContext<PlayerHandler>();

    private readonly ISpatialWorldService _spatialWorldService;
    private readonly IOutgoingPacketQueue _outgoingPacketQueue;
    private readonly IGameNetworkSessionService _gameNetworkSessionService;

    public PlayerHandler(
        ISpatialWorldService spatialWorldService,
        IOutgoingPacketQueue outgoingPacketQueue,
        IGameNetworkSessionService gameNetworkSessionService
    )
        : base(outgoingPacketQueue)
    {
        _spatialWorldService = spatialWorldService;
        _outgoingPacketQueue = outgoingPacketQueue;
        _gameNetworkSessionService = gameNetworkSessionService;
    }

    public Task HandleAsync(PlayerEnteredRegionEvent gameEvent, CancellationToken cancellationToken = default)
    {
        return ProcessRegionAsync(gameEvent.MobileId, gameEvent.RegionId);
    }

    public Task HandleAsync(PlayerExitedRegionEvent gameEvent, CancellationToken cancellationToken = default)
    {
        return ProcessRegionAsync(gameEvent.MobileId, gameEvent.RegionId);
    }

    protected override Task<bool> HandleCoreAsync(GameSession session, IGameNetworkPacket packet)
    {
        if (packet is not SpyOnClientPacket spyOnClientPacket)
        {
            return Task.FromResult(true);
        }

        session.HardwareInfo = new ClientHardwareInfo
        {
            ClientInfoVersion = spyOnClientPacket.ClientInfoVersion,
            InstanceId = spyOnClientPacket.InstanceId,
            OsMajor = spyOnClientPacket.OsMajor,
            OsMinor = spyOnClientPacket.OsMinor,
            OsRevision = spyOnClientPacket.OsRevision,
            CpuManufacturer = spyOnClientPacket.CpuManufacturer,
            CpuFamily = spyOnClientPacket.CpuFamily,
            CpuModel = spyOnClientPacket.CpuModel,
            CpuClockSpeed = spyOnClientPacket.CpuClockSpeed,
            CpuQuantity = spyOnClientPacket.CpuQuantity,
            PhysicalMemory = spyOnClientPacket.PhysicalMemory,
            ScreenWidth = spyOnClientPacket.ScreenWidth,
            ScreenHeight = spyOnClientPacket.ScreenHeight,
            ScreenDepth = spyOnClientPacket.ScreenDepth,
            DirectXVersion = spyOnClientPacket.DirectXVersion,
            DirectXMinor = spyOnClientPacket.DirectXMinor,
            VideoCardDescription = spyOnClientPacket.VideoCardDescription,
            VideoCardVendorId = spyOnClientPacket.VideoCardVendorId,
            VideoCardDeviceId = spyOnClientPacket.VideoCardDeviceId,
            VideoCardMemory = spyOnClientPacket.VideoCardMemory,
            Distribution = spyOnClientPacket.Distribution,
            ClientsRunning = spyOnClientPacket.ClientsRunning,
            ClientsInstalled = spyOnClientPacket.ClientsInstalled,
            PartialInstalled = spyOnClientPacket.PartialInstalled,
            UnknownFlag = spyOnClientPacket.UnknownFlag,
            LanguageCode = spyOnClientPacket.LanguageCode,
            UnknownEnding = spyOnClientPacket.UnknownEnding
        };

        _logger.Debug(
            "Stored client hardware info for session {SessionId} Instance={InstanceId} OS={OsMajor}.{OsMinor}.{OsRevision}",
            session.SessionId,
            session.HardwareInfo.InstanceId,
            session.HardwareInfo.OsMajor,
            session.HardwareInfo.OsMinor,
            session.HardwareInfo.OsRevision
        );

        return Task.FromResult(true);
    }

    private Task ProcessRegionAsync(Serial mobileId, int regionId)
    {
        if (_gameNetworkSessionService.TryGetByCharacterId(mobileId, out var session))
        {
            var region = _spatialWorldService.GetRegionById(regionId);

            if (region is not null)
            {
                if (region.Music.HasValue)
                {
                    _logger.Debug(
                        "Sending region info {RegionId} with music {MusicId} for session {SessionId}",
                        region.Id,
                        region.Music.Value,
                        session.SessionId
                    );
                    _outgoingPacketQueue.Enqueue(session.SessionId, new SetMusicPacket(region.Music.Value));

                    if (region is JsonTownRegion regionJson)
                    {
                        _outgoingPacketQueue.Enqueue(
                            session.SessionId,
                            SpeechMessageFactory.CreateSystem($"Welcome in {regionJson.Name}", SpeechHues.Green)
                        );

                        if (!regionJson.GuardsDisabled)
                        {
                            _outgoingPacketQueue.Enqueue(
                                session.SessionId,
                                SpeechMessageFactory.CreateSystem("You are protected by the town guards.", SpeechHues.Green)
                            );
                        }
                        else
                        {
                            _outgoingPacketQueue.Enqueue(
                                session.SessionId,
                                SpeechMessageFactory.CreateSystem("You are not protected by the town guards", SpeechHues.Red)
                            );
                        }
                    }
                }
            }
        }

        return Task.CompletedTask;
    }
}
