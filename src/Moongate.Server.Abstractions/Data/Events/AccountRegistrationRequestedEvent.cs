using Moongate.Core.Primitives;
using SquidStd.Core.Interfaces.Events;

namespace Moongate.Server.Abstractions.Data.Events;

/// <summary>
/// Raised when a web registration creates a pending (inactive) account. The seam for a future email
/// feature: it subscribes, builds a verify link from <paramref name="Token" />, and sends the mail.
/// </summary>
public sealed record AccountRegistrationRequestedEvent(Serial AccountId, string Username, string Email, string Token)
    : IEvent;
