using Moongate.Core.Types;

namespace Moongate.Server.Core.Data.Args;

public class MoongateServerArgs
{
    public string RootDirectory { get; set; }

    public LogLevelType LogLevel { get; set; }

    public bool LogToFile { get; set; }

    public bool LogPackets { get; set; }
}
