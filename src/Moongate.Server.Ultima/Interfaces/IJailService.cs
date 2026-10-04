using Moongate.Core.Primitives;
using Moongate.Persistence.Interfaces;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Data.Jail;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Types.Jail;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     The jail: the cells of <c>data/jail.toml</c>, who is in them and until when. A mobile, a player or an NPC, is
///     sent to a cell for a number of real days; when they are over it goes back where it was arrested.
/// </summary>
/// <remarks>
///     Game loop only, once started. The world save writes <see cref="Sentences" /> and deletes the ended ones.
/// </remarks>
public interface IJailService : IMoongateStartupService, IPersistenceDeletionSource
{
    /// <summary>
    ///     Gets whether there is a jail: <c>data/jail.toml</c> was found.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    ///     Gets the cells in file order; none without a jail.
    /// </summary>
    IReadOnlyList<JailCell> Cells { get; }

    /// <summary>
    ///     Gets the live sentences, which the world save writes.
    /// </summary>
    IReadOnlyCollection<JailSentenceEntity> Sentences { get; }

    /// <summary>
    ///     Gets the longest sentence in days.
    /// </summary>
    int MaxDays { get; }

    /// <summary>
    ///     Gets the sentence of the mobile; null when it has none.
    /// </summary>
    JailSentenceEntity? GetSentence(Serial prisoner);

    /// <summary>
    ///     Gets the sentence that holds the cell now, one that names it and is not over; null for a free cell.
    /// </summary>
    JailSentenceEntity? GetOccupant(int cell);

    /// <summary>
    ///     Sends the mobile to the cell for that many days and keeps where it was. A mobile already in jail moves to
    ///     the cell with a sentence that starts now, and keeps the place it was first arrested on.
    /// </summary>
    JailResultType Jail(MobileEntity prisoner, int cell, int days, MobileEntity by);

    /// <summary>
    ///     Ends the sentence now with no fine and no note: a prisoner in the world goes back at once, a player who is
    ///     offline at its next login. False when the mobile has no sentence.
    /// </summary>
    bool Pardon(Serial prisoner);

    /// <summary>
    ///     Releases every prisoner whose sentence is over and who is in the world: the fine is taken, it goes back
    ///     where it was arrested and gets its release note. The sentence of an NPC that is gone is dropped.
    /// </summary>
    void Check();
}
