using Moongate.Core.Primitives;
using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Interfaces.Chat;
using SquidStd.Persistence.Abstractions.Interfaces.Persistence;
using SquidStd.Scripting.Lua.Attributes.Scripts;

namespace Moongate.Server.Scripting;

/// <summary>
/// Exposes speaking and server-wide broadcasting to Lua. Mobiles are referenced by serial, matching
/// <see cref="MobileModule" />. Neither function mutates an entity store, so unlike a write path they
/// carry no loop-affinity guard — the same reasoning already applied to <see cref="MobileModule" />'s
/// own read-only functions (<c>Get</c>, <c>Skills</c>).
/// </summary>
[ScriptModule("chat", "Speak or broadcast as a mobile.")]
public sealed class ChatModule
{
    private readonly IChatService _chat;
    private readonly IEntityStore<MobileEntity, Serial> _mobiles;

    public ChatModule(IChatService chat, IPersistenceService persistence)
    {
        _chat = chat;
        _mobiles = persistence.GetStore<MobileEntity, Serial>();
    }

    [ScriptFunction("broadcast", "Sends a server-wide system message to every in-world player.")]
    public void Broadcast(string text)
        => _chat.Broadcast(text);

    [ScriptFunction(
        "say",
        "Speaks as the mobile with the given serial; false on unknown serial, blank, overlong or command text."
    )]
    public bool Say(uint serial, string text)
    {
        var mobile = _mobiles.GetById((Serial)serial);

        return mobile is not null && _chat.SayAs(mobile, text);
    }
}
