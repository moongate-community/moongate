using Moongate.Core.Primitives;
using SquidStd.Persistence.Abstractions.Interfaces.Persistence;

namespace Moongate.Persistence.Generators;

public class ItemSerialGenerator : IIdGenerator<Serial>
{
    public Serial Initial => (Serial)Serial.MinItem;

    public Serial Next(Serial current)
        => (Serial)(current + 1);
}
