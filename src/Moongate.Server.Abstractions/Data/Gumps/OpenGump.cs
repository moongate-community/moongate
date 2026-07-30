using Moongate.Server.Abstractions.Types.Gumps;

namespace Moongate.Server.Abstractions.Data.Gumps;

/// <summary>
/// A gump the server has sent and not yet had an answer to, holding what it drew so the answer can
/// be checked against it. Nothing about a response is trustworthy on its own: the client chooses the
/// button id it reports, so the only defence is remembering which ids were real.
/// </summary>
/// <param name="Serial">The gump's serial, unique per session.</param>
/// <param name="TypeId">The type the client uses to decide whether a new gump replaces this one.</param>
/// <param name="GumpId">The author's name for this gump, used by <c>Close</c> and in logs.</param>
/// <param name="ButtonIds">Every button id the builder emitted.</param>
/// <param name="SwitchIds">Every checkbox and radio id the builder emitted.</param>
/// <param name="TextEntryIds">Every text entry id the builder emitted.</param>
/// <param name="OnResponse">What to run once the answer has been accepted.</param>
public sealed record OpenGump(
    uint Serial,
    int TypeId,
    string GumpId,
    IReadOnlySet<int> ButtonIds,
    IReadOnlySet<int> SwitchIds,
    IReadOnlySet<int> TextEntryIds,
    Action<GumpResponse>? OnResponse
)
{
    /// <summary>The longest text the client's own field will produce.</summary>
    public const int MaximumTextLength = 239;

    /// <summary>
    /// Checks a response against what was drawn, returning <see cref="GumpRejectionType.None" /> when
    /// every id in it is one this gump emitted.
    /// </summary>
    public GumpRejectionType Validate(
        int button,
        IReadOnlyList<int> switches,
        IReadOnlyDictionary<int, string> textEntries
    )
    {
        // Button 0 is the client's own close button and is never drawn by the server.
        if (button != 0 && !ButtonIds.Contains(button))
        {
            return GumpRejectionType.UnknownButton;
        }

        foreach (var id in switches)
        {
            if (!SwitchIds.Contains(id))
            {
                return GumpRejectionType.UnknownSwitch;
            }
        }

        foreach (var (id, text) in textEntries)
        {
            if (!TextEntryIds.Contains(id))
            {
                return GumpRejectionType.UnknownTextEntry;
            }

            if (text.Length > MaximumTextLength)
            {
                return GumpRejectionType.TextTooLong;
            }
        }

        return GumpRejectionType.None;
    }
}
