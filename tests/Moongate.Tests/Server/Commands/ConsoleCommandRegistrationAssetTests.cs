using System.Text.RegularExpressions;

namespace Moongate.Tests.Server.Commands;

public sealed class ConsoleCommandRegistrationAssetTests
{
    private static readonly Regex RegisterConsoleCommandRegex = new(
        "RegisterConsoleCommand\\(\\s*\"(?<commandNames>[^\"]+)\"",
        RegexOptions.Compiled | RegexOptions.CultureInvariant
    );

    [Test]
    public void BuiltInRegisterConsoleCommandAttributes_ShouldNotContainDotPrefixedAliases()
    {
        var commandsRoot = Path.Combine(GetRepositoryRoot(), "src", "Moongate.Server", "Commands");
        var offenders = Directory
            .GetFiles(commandsRoot, "*.cs", SearchOption.AllDirectories)
            .SelectMany(
                static path => RegisterConsoleCommandRegex
                    .Matches(File.ReadAllText(path))
                    .Select(
                        match => new
                        {
                            Path = path,
                            CommandNames = match.Groups["commandNames"].Value
                        }
                    )
            )
            .SelectMany(
                entry => entry.CommandNames
                    .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Where(static alias => alias.Length > 0 && alias[0] == '.')
                    .Select(alias => (entry.Path, Alias: alias))
            )
            .OrderBy(static offender => offender.Path, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static offender => offender.Alias, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.That(
            offenders,
            Is.Empty,
            () => $"Dot-prefixed RegisterConsoleCommand aliases found:{Environment.NewLine}" +
                  string.Join(
                      Environment.NewLine,
                      offenders.Select(
                          static offender => $"{Path.GetRelativePath(GetRepositoryRoot(), offender.Path)} => {offender.Alias}"
                      )
                  )
        );
    }

    private static string GetRepositoryRoot()
        => Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", ".."));
}
