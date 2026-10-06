using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Ultima.Data.Gumps;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Types.Gumps;

namespace Moongate.Tests.TestSupport.Ultima.Gumps;

/// <summary>
///     Has the gumps in <see cref="Ids" /> and answers every question with <see cref="Answer" />; records what was asked.
/// </summary>
public sealed class StubGumpTemplateService : IGumpTemplateService
{
    public HashSet<string> Ids { get; } = [];

    public string? Answer { get; set; }

    public List<string> Asked { get; } = [];

    /// <summary>
    ///     Gets or sets what happens while the player is being asked, before the answer.
    /// </summary>
    public Action? WhileAsking { get; set; }

    /// <summary>
    ///     What <see cref="Open(GameSession, string, IReadOnlyDictionary{string, string}, Action{GameSession, GumpTemplateAnswer}, Action{GameSession, GumpCloseReasonType}?)" />
    ///     was asked, so a test can answer or close the gump.
    /// </summary>
    public List<(GameSession Session, string Id, IReadOnlyDictionary<string, string> Args, Action<GameSession, GumpTemplateAnswer> OnAnswer, Action<GameSession, GumpCloseReasonType>? OnClosed)> Opened { get; } = [];

    public bool Exists(string id)
    {
        return Ids.Contains(id);
    }

    public bool TryGet(string id, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out Moongate.Server.Ultima.Data.Templates.Gumps.GumpTemplate? template)
    {
        template = null;

        return false;
    }

    public bool Open(
        GameSession session,
        Moongate.Server.Ultima.Data.Templates.Gumps.GumpTemplate template,
        IReadOnlyDictionary<string, string> args,
        Action<GameSession, GumpTemplateAnswer> onAnswer,
        Action<GameSession, GumpCloseReasonType>? onClosed = null
    )
    {
        return Ids.Contains(template.Id);
    }

    public bool Open(
        GameSession session,
        string id,
        IReadOnlyDictionary<string, string> args,
        Action<GameSession, GumpTemplateAnswer> onAnswer,
        Action<GameSession, GumpCloseReasonType>? onClosed = null
    )
    {
        Opened.Add((session, id, args, onAnswer, onClosed));

        return Ids.Contains(id);
    }

    public Task<string?> AskAsync(
        GameSession session,
        string id,
        IReadOnlyDictionary<string, string> args,
        CancellationToken cancellationToken = default
    )
    {
        Asked.Add(id);
        WhileAsking?.Invoke();

        return Task.FromResult(Ids.Contains(id) ? Answer : null);
    }
}
