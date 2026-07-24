namespace Moongate.Server.Data.Exceptions;

public class UODirectoryNotValidException : Exception
{
    public UODirectoryNotValidException()
    {
    }

    public UODirectoryNotValidException(string message)
        : base(message)
    {
    }

    public UODirectoryNotValidException(string message, Exception inner)
        : base(message, inner)
    {
    }
}
