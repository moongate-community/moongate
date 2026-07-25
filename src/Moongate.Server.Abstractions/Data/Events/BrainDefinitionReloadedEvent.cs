using Moongate.Server.Abstractions.Data.AI;
using Moongate.Server.Abstractions.Interfaces.Events;

namespace Moongate.Server.Abstractions.Data.Events;

public sealed record BrainDefinitionReloadedEvent(string BrainId, BrainDescriptor Descriptor) : ILoopAffineEvent;
