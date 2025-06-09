using Moongate.Core.Server.Types;

namespace Moongate.Core.Server.Data.Options;

public class MoongateArgsOptions
{
    public LogLevelType LogLevel { get; set; } = LogLevelType.Information;

    public string RootDirectory { get; set; } = string.Empty;
}
