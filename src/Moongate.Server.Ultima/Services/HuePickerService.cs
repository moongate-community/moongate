using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Characters;
using Moongate.Server.Ultima.Data.HuePicking;
using Moongate.Server.Ultima.Data.Internal.HuePicking;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Packets.World;
using Serilog;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     Keeps the one hue picker a player may have in its session: a new one ends the old with no hue, and so does the
///     session closing. Unlike the other emulators, an answer is taken only for the picker that is open, by its id,
///     and its hue is kept from 2 to 1001 as ModernUO's <c>ClipDyedHue</c>.
/// </summary>
public sealed class HuePickerService : IHuePickerService
{
    private readonly ILogger _logger = Log.ForContext<HuePickerService>();
    private readonly IMobileService _mobiles;
    private readonly IPacketSendService _sender;

    public HuePickerService(IMobileService mobiles, IPacketSendService sender)
    {
        _mobiles = mobiles;
        _sender = sender;
    }

    public void Begin(GameSession session, int graphic, Action<GameSession, int?> callback)
    {
        if (!session.CharacterId.IsValid || !_mobiles.IsInWorld(session.CharacterId))
        {
            Invoke(session, callback, null);

            return;
        }

        var state = session.Get(HuePickerSessionKeys.State);

        if (state is null)
        {
            state = new();
            session.Set(HuePickerSessionKeys.State, state);
        }

        var previous = state.Pending;
        state.LastId = state.LastId == int.MaxValue ? 1 : state.LastId + 1;
        state.Pending = new(state.LastId, callback);

        if (previous is not null)
        {
            Invoke(session, previous.Callback, null);
        }

        _sender.TrySend(session.SessionId, new HuePickerPacket(state.LastId, unchecked((ushort)graphic)));
    }

    public bool TryComplete(GameSession session, int pickerId, int hue)
    {
        var state = session.Get(HuePickerSessionKeys.State);

        if (state?.Pending is not { } pending || pending.Id != pickerId)
        {
            return false;
        }

        state.Pending = null;
        Invoke(session, pending.Callback, CharacterCreationRules.ValidateClothingHue(new Hue(unchecked((ushort)hue))).Value);

        return true;
    }

    public void OnSessionClosed(GameSession session)
    {
        var state = session.Get(HuePickerSessionKeys.State);

        if (state?.Pending is not { } pending)
        {
            return;
        }

        state.Pending = null;
        Invoke(session, pending.Callback, null);
    }

    private void Invoke(GameSession session, Action<GameSession, int?> callback, int? hue)
    {
        try
        {
            callback(session, hue);
        }
        catch (Exception exception)
        {
            _logger.Error(exception, "A hue picker callback of session {SessionId} failed", session.SessionId);
        }
    }
}
