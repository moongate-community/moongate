using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Data.Session;
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

    /// <summary>
    /// Sends a system message to one session — the sibling of <see cref="Broadcast" />, which sends to
    /// every session. Exists on the contract rather than inside the server because the packet factory
    /// that builds these messages does not, and a plugin with something to tell one player would
    /// otherwise have nowhere to say it.
    /// </summary>
    void SendSystemMessage(PlayerSession session, string text, Hue? hue = null);

    void Say(MobileEntity speaker, ChatMessageType type, string text, Hue hue, int range);

    /// <summary>
    /// Speaks as <paramref name="speaker" />, applying every rule that governs speech: blank and
    /// overlong text are refused, and text the classifier reads as a command is refused rather than
    /// spoken. True when something was said.
    /// </summary>
    bool SayAs(MobileEntity speaker, string text);
}
