using Moongate.Server.Abstractions.Data.Gumps;
using Moongate.Server.Abstractions.Data.Session;
using Moongate.Server.Abstractions.Types.Gumps;

namespace Moongate.Server.Abstractions.Interfaces.Gumps;

/// <summary>
/// Draws gumps for a session and routes their answers back.
/// <para>
/// A gump answers once. The client closes it when a reply button is pressed, so the server drops it
/// from the session's open set as the answer arrives; a gump that should stay up is sent again from
/// its own callback.
/// </para>
/// </summary>
public interface IGumpService
{
    /// <summary>
    /// Draws a gump and sends it. <paramref name="gumpId" /> is the author's name for it: it decides
    /// the type the client uses to replace an earlier copy, and is what <see cref="Close" /> takes.
    /// </summary>
    void Show(PlayerSession session, string gumpId, Action<IGumpBuilder> build, Action<GumpResponse>? onResponse = null);

    /// <summary>
    /// Handles an answer: checks it against what was drawn, forgets the gump, and runs its callback.
    /// Returns why it was refused, or <see cref="GumpRejectionType.None" /> when it was accepted.
    /// </summary>
    GumpRejectionType HandleResponse(
        PlayerSession session,
        uint serial,
        int typeId,
        int button,
        IReadOnlyList<int> switches,
        IReadOnlyDictionary<int, string> textEntries
    );

    /// <summary>Forgets a named gump for this session. Returns false when none was open.</summary>
    bool Close(PlayerSession session, string gumpId);

    /// <summary>Forgets every gump open for this session, and returns how many there were.</summary>
    int CloseAll(PlayerSession session);
}
