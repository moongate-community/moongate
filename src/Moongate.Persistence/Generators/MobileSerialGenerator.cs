using Moongate.Core.Primitives;
using SquidStd.Persistence.Abstractions.Interfaces.Persistence;

namespace Moongate.Persistence.Generators;

public class MobileSerialGenerator : IIdGenerator<Serial>
{
    public Serial Initial => (Serial)Serial.MinMobile;

    public Serial Next(Serial current)
    {
        return (Serial)(current + 1);
    }


}
