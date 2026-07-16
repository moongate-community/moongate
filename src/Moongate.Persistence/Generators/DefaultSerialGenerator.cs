using Moongate.Core.Primitives;
using SquidStd.Persistence.Abstractions.Interfaces.Persistence;

namespace Moongate.Persistence.Generators;

public class DefaultSerialGenerator : IIdGenerator<Serial>
{
    public Serial Initial => (Serial)(Serial.Zero + 1);

    public Serial Next(Serial current)
        => (Serial)(current + 1);
}
