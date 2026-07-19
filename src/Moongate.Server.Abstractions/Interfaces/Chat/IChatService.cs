using Moongate.Persistence.Entities;
using Moongate.UO.Data.Hues;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Abstractions.Interfaces.Chat;

/// <summary>
/// Speaks or broadcasts as a mobile. <see cref="Say" /> fans a message out by proximity through
/// <see cref="World.IWorldService.SendToPlayersInRange{TPacket}" /> and publishes
/// <see cref="Data.Events.MobileSpeechEvent" />; <see cref="Broadcast" /> sends a system message to
/// every in-world session with no range check.
/// </summary>
public interface IChatService
{
    void Broadcast(string text, Hue? hue = null);

    void Say(MobileEntity speaker, ChatMessageType type, string text, Hue hue, int range);
}
