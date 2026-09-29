using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Ultima.Data.Targeting;
using Moongate.Server.Ultima.Types.Targeting;

namespace Moongate.Server.Ultima.Data.Internal.Targeting;

/// <summary>
///     The target a player was asked for: the id its answer must carry, what it may pick, and what to call with the
///     result.
/// </summary>
public sealed record PendingTarget(int Id, TargetCursorType Cursor, Action<GameSession, TargetResult> Callback);
