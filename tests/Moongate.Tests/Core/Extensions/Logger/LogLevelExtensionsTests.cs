using Moongate.Core.Extensions.Logger;
using Moongate.Core.Types;
using Serilog.Events;

namespace Moongate.Tests.Core.Extensions.Logger;

public sealed class LogLevelExtensionsTests
{
    [Theory,
     InlineData(LogLevelType.Trace, LogEventLevel.Verbose),
     InlineData(LogLevelType.Debug, LogEventLevel.Debug),
     InlineData(LogLevelType.Information, LogEventLevel.Information),
     InlineData(LogLevelType.Warning, LogEventLevel.Warning),
     InlineData(LogLevelType.Error, LogEventLevel.Error),
     InlineData(LogLevelType.Critical, LogEventLevel.Fatal),
     InlineData(LogLevelType.None, LogEventLevel.Information),
     InlineData((LogLevelType)255, LogEventLevel.Information)]
    public void ToSerilogLogLevel_MapsSeverityAndDefaults(LogLevelType level, LogEventLevel expected)
        => Assert.Equal(expected, level.ToSerilogLogLevel());
}
