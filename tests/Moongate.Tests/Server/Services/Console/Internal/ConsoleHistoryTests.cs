using Moongate.Server.Services.Console.Internal;

namespace Moongate.Tests.Server.Services.Console.Internal;

public sealed class ConsoleHistoryTests
{
    private readonly ConsoleHistory _history = new();

    [Fact]
    public void Previous_WalksBackFromTheNewestLine_AndStopsAtTheOldest()
    {
        _history.Add("save");
        _history.Add("help");

        Assert.Equal("help", _history.Previous(""));
        Assert.Equal("save", _history.Previous("help"));
        Assert.Equal("save", _history.Previous("save"));
    }

    [Fact]
    public void Next_PastTheNewest_GivesBackTheLineBeingTyped()
    {
        _history.Add("save");
        _history.Add("help");

        _history.Previous("ec");
        _history.Previous("help");

        Assert.Equal("help", _history.Next());
        Assert.Equal("ec", _history.Next());
        Assert.Null(_history.Next());
    }

    [Fact]
    public void AnEmptyHistory_GivesNothing()
    {
        Assert.Null(_history.Previous("echo"));
        Assert.Null(_history.Next());
    }

    [Theory, InlineData(""), InlineData("   ")]
    public void BlankLines_AreNotKept(string line)
    {
        _history.Add(line);

        Assert.Null(_history.Previous(""));
    }

    [Fact]
    public void ALineRepeatedAtOnce_IsKeptOnce()
    {
        _history.Add("save");
        _history.Add("save");

        Assert.Equal("save", _history.Previous(""));
        Assert.Equal("save", _history.Previous("save"));
        Assert.Equal("", _history.Next());
    }

    [Fact]
    public void Add_StartsTheWalkAgainFromTheNewest()
    {
        _history.Add("save");
        _history.Add("help");
        _history.Previous("");
        _history.Previous("help");

        _history.Add("time");

        Assert.Equal("time", _history.Previous(""));
    }

    [Fact]
    public void OnlyTheLastHundredLines_AreKept()
    {
        for (var i = 0; i < 105; i++)
        {
            _history.Add($"echo {i}");
        }

        string? oldest = null;

        for (var i = 0; i < 200; i++)
        {
            oldest = _history.Previous(oldest ?? "");
        }

        Assert.Equal("echo 5", oldest);
    }
}
