using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Data.Jail;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Types.Jail;
using Moongate.Ultima.Types;

namespace Moongate.Tests.TestSupport.Ultima.Jail;

/// <summary>
///     A jail with settable cells and sentences that records who was sent where and who was pardoned.
/// </summary>
public sealed class StubJailService : IJailService
{
    public bool IsEnabled { get; set; } = true;

    public List<JailCell> CellList { get; } = [];

    public List<JailSentenceEntity> SentenceList { get; } = [];

    public IReadOnlyList<JailCell> Cells => CellList;

    public IReadOnlyCollection<JailSentenceEntity> Sentences => SentenceList;

    public int MaxDays { get; set; } = 30;

    public MapType? Map { get; set; } = MapType.Felucca;

    /// <summary>
    ///     The time a sentence is over at, in Unix milliseconds.
    /// </summary>
    public long Now { get; set; }

    public JailResultType Result { get; set; } = JailResultType.Ok;

    public List<(MobileEntity Prisoner, int Cell, int Days, MobileEntity By)> Jailed { get; } = [];

    public List<Serial> Pardoned { get; } = [];

    public JailSentenceEntity? GetSentence(Serial prisoner)
    {
        return SentenceList.FirstOrDefault(sentence => sentence.Id == prisoner);
    }

    public JailSentenceEntity? GetOccupant(int cell)
    {
        return SentenceList.FirstOrDefault(sentence => sentence.Cell == cell && !sentence.IsOver(Now));
    }

    /// <summary>
    ///     The reason given with each jailing, in order; null for none.
    /// </summary>
    public List<string?> Reasons { get; } = [];

    public JailResultType Jail(MobileEntity prisoner, int cell, int days, MobileEntity by, string? reason = null)
    {
        Jailed.Add((prisoner, cell, days, by));
        Reasons.Add(reason);

        return Result;
    }

    /// <summary>
    ///     Who a search by name gives, whatever the name.
    /// </summary>
    public List<JailCandidate> Found { get; } = [];

    /// <summary>
    ///     The names searched, in order.
    /// </summary>
    public List<string> Searched { get; } = [];

    public Task<IReadOnlyList<JailCandidate>> FindAsync(string name, CancellationToken cancellationToken = default)
    {
        Searched.Add(name);

        return Task.FromResult<IReadOnlyList<JailCandidate>>(Found.ToList());
    }

    public bool Pardon(Serial prisoner)
    {
        Pardoned.Add(prisoner);

        return GetSentence(prisoner) is not null;
    }

    public void Check()
    {
    }

    public IReadOnlyCollection<Serial> Capture()
    {
        return [];
    }

    public void Committed(IReadOnlyCollection<Serial> serials)
    {
    }

    public Task StartAsync()
    {
        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        return Task.CompletedTask;
    }
}
