using Moongate.Core.Primitives;
using Moongate.Persistence.Interfaces;

namespace Moongate.Persistence.Entities;

public class MobileEntity : ISerialIdEntity
{
    public Serial Id { get; set; }

    public string Name { get; set; }
}
