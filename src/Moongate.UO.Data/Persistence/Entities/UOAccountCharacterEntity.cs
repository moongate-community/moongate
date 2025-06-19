using Moongate.UO.Data.Ids;

namespace Moongate.UO.Data.Persistence.Entities;

public class UOAccountCharacterEntity
{
    public int Slot { get; set; }

    public string Name { get; set; }

    public Serial MobileId { get; set; }
}
