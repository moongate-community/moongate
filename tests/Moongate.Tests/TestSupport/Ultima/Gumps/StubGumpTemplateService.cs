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

    public bool Exists(string id)
    {
        return Ids.Contains(id);
    }

    public bool Open(
        GameSession session,
        string id,
        IReadOnlyDictionary<string, string> args,
        Action<GameSession, GumpTemplateAnswer> onAnswer,
        Action<GameSession, GumpCloseReasonType>? onClosed = null
    )
    {
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
