using Moongate.Server.Abstractions.Interfaces.Events;

namespace Moongate.Server.Abstractions.Data.Events;

/// <summary>
/// Published on the game-loop thread once the world is loaded and ready. Forwarded to Lua as
/// <c>world_ready</c>; scripts use it for bootstrap spawns without <c>game.post</c>.
/// </summary>
public sealed record WorldReadyEvent : ILoopAffineEvent;
