using Moongate.Server.Core.Data.Sessions;

namespace Moongate.Server.Ultima.Data.Internal.HuePicking;

/// <summary>
///     The hue picker a player was shown: the id its answer must carry and what to call with the hue picked, or with
///     none when the picker ends without an answer.
/// </summary>
public sealed record PendingHuePicker(int Id, Action<GameSession, int?> Callback);
