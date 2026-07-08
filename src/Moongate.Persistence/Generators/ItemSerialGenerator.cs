using Moongate.Core.Primitives;
using SquidStd.Persistence.Abstractions.Interfaces.Persistence;

namespace Moongate.Persistence.Generators;

public class ItemSerialGenerator : IIdGenerator<Serial>
{
    public Serial Next(Serial current)
    {
        return (Serial)(current + 1);
    }

    public Serial Initial => (Serial)Serial.MinItem;
}
