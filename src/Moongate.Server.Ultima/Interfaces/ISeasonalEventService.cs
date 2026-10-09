using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Data.Schedule;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     The seasonal events of
///     <c>
///         data/schedule.toml
///     </c>
///     : on by their dates, or forced on or off by the staff, and announced to
///     <c>
///         scripts/events/&lt;id&gt;.lua
///     </c>
///     with
///     <c>
///         on_start
///     </c>
///     and
///     <c>
///         on_end
///     </c>
///     .
/// </summary>
public interface ISeasonalEventService : IMoongateStartupService
{
    /// <summary>
    ///     Gets every event with its mode and its state now.
    /// </summary>
    IReadOnlyList<SeasonalEventState> Events { get; }

    /// <summary>
    ///     Tells whether an event is on now.
    /// </summary>
    /// <param name="id">
    ///     The id of the event.
    /// </param>
    /// <returns>
    ///     False for an event that is off or does not exist.
    /// </returns>
    bool IsActive(string id);

    /// <summary>
    ///     Gets one event.
    /// </summary>
    /// <param name="id">
    ///     The id of the event.
    /// </param>
    /// <returns>
    ///     The event, or null when there is none with that id.
    /// </returns>
    SeasonalEventState? Get(string id);

    /// <summary>
    ///     Changes the mode of an event and starts or ends it if its state changes.
    /// </summary>
    /// <param name="id">
    ///     The id of the event.
    /// </param>
    /// <param name="mode">
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
    /// <returns>
    ///     False for an unknown event or mode.
    /// </returns>
    bool SetMode(string id, string mode);
}
