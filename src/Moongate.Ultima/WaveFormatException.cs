using System;

namespace Moongate.Ultima;

public class WaveFormatException : Exception
{
    public WaveFormatException()
    {
    }

    public WaveFormatException(string message) : base(message)
    {
    }

    public WaveFormatException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
