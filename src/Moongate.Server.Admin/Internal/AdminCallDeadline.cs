namespace Moongate.Server.Admin.Internal;

/// <summary>
///     Endpoint metadata: how long a call to the endpoint may take, instead of the default of the host.
/// </summary>
internal sealed record AdminCallDeadline(TimeSpan Duration);
