using Moongate.UO.Data.Ids;

namespace Moongate.UO.Data.Persistence.Entities;

public record ItemReference(Serial Id, int ItemId, int Hue);
