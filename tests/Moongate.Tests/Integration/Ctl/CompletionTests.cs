using System.Text.RegularExpressions;
using Moongate.Tests.TestSupport.Ctl;
using Moongate.Tests.TestSupport.Directories;

namespace Moongate.Tests.Integration.Ctl;

public sealed class CompletionTests
{
    [Theory, InlineData("bash", "complete -F _mgctl mgctl"), InlineData("zsh", "#compdef mgctl"),
     InlineData("fish", "complete -c mgctl")]
    public async Task Completion_PrintsTheScriptOfTheShell_WithUnixLineEndings(string shell, string expected)
    {
        var result = await CtlProcess.RunAsync("completion", shell);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains(expected, result.Output);
        Assert.DoesNotContain('\r', result.Output);
    }

    [Fact]
    public async Task Completion_AnUnknownShell_FailsNamingTheShells()
    {
        var result = await CtlProcess.RunAsync("completion", "tcsh");

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("bash, zsh, fish", result.Output);
    }

    [ToolFact("bash")]
    public async Task Bash_TheFirstWord_OffersEveryCommand()
    {
        Assert.Equal(["--help", "--version", "completion", "convert", "init", "migrate"], await CompleteAsync("mgctl", ""));
        Assert.Equal(["completion", "convert"], await CompleteAsync("mgctl", "co"));
    }

    [ToolFact("bash")]
    public async Task Bash_TheSecondWordOfAGroup_OffersItsCommands()
    {
        Assert.Equal(["apply", "status"], await CompleteAsync("mgctl", "migrate", ""));
        Assert.Equal(
            ["modernuo-books", "modernuo-chests", "modernuo-locations", "modernuo-signs", "modernuo-spawns", "modernuo-teleporters", "uox"],
            await CompleteAsync("mgctl", "convert", "")
        );
        Assert.Equal(["bash", "fish", "zsh"], await CompleteAsync("mgctl", "completion", ""));
    }

    // The script must offer what the commands really take: a new command or option shows up here.
    [ToolFact("bash")]
    public async Task Bash_EveryCommandOfTheHelp_OffersExactlyItsOptions()
    {
        var help = await CtlProcess.RunAsync("--help");
        var commands = Regex.Matches(help.Output, @"^  ([a-z-]+(?: [a-z-]+)?) {2,}", RegexOptions.Multiline)
                            .Select(match => match.Groups[1].Value)
                            .ToList();
        Assert.Contains("migrate status", commands);
        Assert.Contains("completion", commands);

        foreach (var command in commands)
        {
            var words = command.Split(' ');
            var commandHelp = await CtlProcess.RunAsync([..words, "--help"]);
            var expected = Regex.Matches(commandHelp.Output, @"^  (--[a-z-]+)", RegexOptions.Multiline)
                                .Select(match => match.Groups[1].Value)
                                .Append("--help")
                                .Order(StringComparer.Ordinal)
                                .ToList();

            Assert.Equal(expected, await CompleteAsync(["mgctl", ..words, "--"]));
        }
    }

    [ToolFact("bash")]
    public async Task Bash_AfterAnOption_OffersItsValues()
    {
        using var directory = new TemporaryDirectory();
        Directory.CreateDirectory(Path.Combine(directory.Path, "my root"));
        File.WriteAllText(Path.Combine(directory.Path, "signs.cfg"), "");

        Assert.Equal(["auth", "world"], await CompleteAsync("mgctl", "migrate", "apply", "--target", ""));
        Assert.Equal(["world"], await CompleteAsync("mgctl", "migrate", "status", "--target", "w"));
        // A directory only; one with a space is one reply (bash quotes it when it inserts it).
        Assert.Equal(
            [Path.Combine(directory.Path, "my root")],
            await CompleteAsync("mgctl", "migrate", "apply", "--root-directory", directory.Path + "/")
        );
        Assert.Equal(
            [Path.Combine(directory.Path, "my root"), Path.Combine(directory.Path, "signs.cfg")],
            await CompleteAsync("mgctl", "convert", "modernuo-signs", "--source", directory.Path + "/")
        );
        Assert.Equal([Path.Combine(directory.Path, "my root")], await CompleteAsync("mgctl", "init", directory.Path + "/"));
        // Free text: nothing to offer.
        Assert.Empty(await CompleteAsync("mgctl", "convert", "modernuo-spawns", "--maps", ""));
    }

    [ToolFact("zsh")]
    public async Task Zsh_TheScript_IsValidZsh()
    {
        using var directory = new TemporaryDirectory();
        var script = Path.Combine(directory.Path, "_mgctl");
        File.WriteAllText(script, (await CtlProcess.RunAsync("completion", "zsh")).Output);

        var result = await ToolProcess.RunAsync("zsh", "-n", script);

        Assert.True(result.ExitCode == 0, result.Output);
    }

    // What bash offers for the last word of the line, sorted.
    private static async Task<List<string>> CompleteAsync(params string[] words)
    {
        using var directory = new TemporaryDirectory();
        var script = Path.Combine(directory.Path, "mgctl.bash");
        File.WriteAllText(script, (await CtlProcess.RunAsync("completion", "bash")).Output);
        var line = string.Join(' ', words.Select(word => "'" + word.Replace("'", "'\\''") + "'"));
        var driver =
            $"source '{script}'; COMP_WORDS=({line}); COMP_CWORD={words.Length - 1}; _mgctl; " +
            "[ ${#COMPREPLY[@]} -eq 0 ] || printf '%s\\n' \"${COMPREPLY[@]}\"";
        var result = await ToolProcess.RunAsync("bash", "-c", driver);
        Assert.True(result.ExitCode == 0, result.Output);

        return result.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries).Order(StringComparer.Ordinal).ToList();
    }
}
