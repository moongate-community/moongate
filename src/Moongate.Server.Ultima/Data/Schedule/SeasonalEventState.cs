namespace Moongate.Server.Ultima.Data.Schedule;

/// <summary>
///     A seasonal event as the staff and the scripts see it.
/// </summary>
/// <param name="Id">
///     The name of the event.
/// </param>
/// <param name="Name">
///     The name shown to people.
/// </param>
/// <param name="From">
///     The first day of the window, as month-day.
/// </param>
/// <param name="To">
///     The last day of the window, as month-day.
/// </param>
/// <param name="Mode">
///     <c>
///         auto
///     </c>
///     ,
///     <c>
///         on
///     </c>
///     or
///     <c>
///         off
///     </c>
///     .
/// </param>
/// <param name="Active">
///     Whether the event is on now.
/// </param>
public sealed record SeasonalEventState(string Id, string Name, string From, string To, string Mode, bool Active);
