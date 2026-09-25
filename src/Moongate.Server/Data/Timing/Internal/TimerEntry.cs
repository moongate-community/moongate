namespace Moongate.Server.Data.Timing.Internal;

/// <summary>
///     A live registration, physically retained in one wheel bucket or the ready set.
/// </summary>
internal sealed class TimerEntry
{
    internal required string Id { get; init; }
    internal required string Name { get; init; }
    internal required Action Callback { get; init; }
    internal required long IntervalTicks { get; init; }
    internal required bool Repeat { get; init; }
    internal required long Sequence { get; init; }
    internal long NominalDeadlineTicks { get; set; }
    internal long DueTick { get; set; }
    internal LinkedListNode<TimerEntry>? Node { get; set; }
    internal bool Ready { get; set; }
}
