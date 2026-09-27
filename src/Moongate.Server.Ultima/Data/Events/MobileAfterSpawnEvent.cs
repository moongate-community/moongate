using Moongate.Server.Core.Interfaces.Events;
using Moongate.Server.Ultima.Data.Mobiles;

namespace Moongate.Server.Ultima.Data.Events;

/// <summary>
///     Published last by <see cref="Interfaces.IMobileFactoryService.SpawnAsync" />, after the commit, with the mobile
///     and what it wears.
/// </summary>
public sealed record MobileAfterSpawnEvent(SpawnedMobile Spawned) : IMoongateEvent;
