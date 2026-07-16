using Moongate.Persistence.Entities;

namespace Moongate.Server.Data.Internal.Mobiles;

/// <summary>The result of building a mobile from a template: the unpersisted mobile and its equipment to apply.</summary>
public sealed record MobileSpawn(MobileEntity Mobile, IReadOnlyList<ResolvedEquipment> Equipment);
