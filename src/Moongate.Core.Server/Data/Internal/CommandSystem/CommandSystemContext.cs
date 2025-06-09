using Moongate.Core.Server.Types;

namespace Moongate.Core.Server.Data.Internal.CommandSystem;

public class CommandSystemContext
{
    public delegate void PrintDelegate(string message, params object[] args);

    public event PrintDelegate? OnPrint;

    public CommandSourceType SourceType { get; set; }
    public string? Command { get; set; }
    public string[]? Arguments { get; set; }

    public void Print(string message, params object[] args)
    {
        OnPrint?.Invoke(message, args);
    }
}
