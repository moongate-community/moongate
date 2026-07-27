using Moongate.Console.Admin.Plugin.Services.Console;
using Moongate.Console.Admin.Plugin.Types;

namespace Moongate.Tests.Console;

public class ConsoleInputTests
{
    [Theory]
    [InlineData("", ConsoleInputKind.Empty)]
    [InlineData("   ", ConsoleInputKind.Empty)]
    [InlineData("help", ConsoleInputKind.Help)]
    [InlineData("HELP", ConsoleInputKind.Help)]
    [InlineData("quit", ConsoleInputKind.Quit)]
    [InlineData("exit", ConsoleInputKind.Quit)]
    [InlineData("broadcast hi", ConsoleInputKind.Command)]
    public void Classify_MapsLinesToKinds(string line, ConsoleInputKind expected)
        => Assert.Equal(expected, ConsoleInput.Classify(line));
}
