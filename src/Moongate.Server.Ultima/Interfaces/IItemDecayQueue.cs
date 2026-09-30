using Moongate.Server.Ultima.Entities.World;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     The ground items waiting to decay, ordered by <see cref="ItemEntity.DecayAt" />. The item service keeps it up to
///     date as items land on and leave the ground; the decay service takes the due ones. Called on the game loop.
/// </summary>
public interface IItemDecayQueue
{
    /// <summary>
    ///     The item just landed on the ground: it decays after its template's decay time from now, or never.
    /// </summary>
    void Restart(ItemEntity item);

    /// <summary>
    ///     The item lies on the ground already, such as one loaded at startup: it keeps its saved decay time, or starts one.
    /// </summary>
    void Track(ItemEntity item);

    /// <summary>
    ///     The item left the ground: it no longer decays.
    /// </summary>
    void Stop(ItemEntity item);

    /// <summary>
    ///     Takes the items whose decay time has passed.
    /// </summary>
    IReadOnlyList<ItemEntity> TakeDue();
}
