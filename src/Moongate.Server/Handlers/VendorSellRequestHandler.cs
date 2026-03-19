using Moongate.Abstractions.Interfaces.Services.Base;
using Moongate.Server.Attributes;
using Moongate.Server.Data.Events.Interaction;
using Moongate.Server.Interfaces.Services.Events;
using Moongate.Server.Interfaces.Services.Interaction;

namespace Moongate.Server.Handlers;

[RegisterGameEventListener]
public sealed class VendorSellRequestHandler : IGameEventListener<VendorSellRequestedEvent>, IMoongateService
{
    private readonly IPlayerSellBuyService _playerSellBuyService;

    public VendorSellRequestHandler(IPlayerSellBuyService playerSellBuyService)
    {
        _playerSellBuyService = playerSellBuyService;
    }

    public Task HandleAsync(VendorSellRequestedEvent gameEvent, CancellationToken cancellationToken = default)
        => _playerSellBuyService.HandleVendorSellRequestAsync(gameEvent.SessionId, gameEvent.VendorSerial, cancellationToken);

    public Task StartAsync()
        => Task.CompletedTask;

    public Task StopAsync()
        => Task.CompletedTask;
}
