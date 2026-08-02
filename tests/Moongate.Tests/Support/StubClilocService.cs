using Moongate.Server.Abstractions.Interfaces.Localization;

namespace Moongate.Tests.Support;

/// <summary>
/// Answers with a fixed text for any cliloc, or null for every one when constructed empty — which is
/// what a shard with no client files looks like. Seed <see cref="Entries" /> instead when a test cares
/// which cliloc says what.
/// </summary>
public sealed class StubClilocService : IClilocService
{
    private readonly string? _text;
    private readonly Dictionary<int, string> _entries;

    public StubClilocService(string? text = null)
    {
        _text = text;
        _entries = [];
    }

    private StubClilocService((int Cliloc, string Text)[] entries)
    {
        _text = null;
        _entries = entries.ToDictionary(entry => entry.Cliloc, entry => entry.Text);
    }

    /// <summary>A table of its own: only the seeded clilocs answer, every other one is null.</summary>
    public static StubClilocService Entries(params (int Cliloc, string Text)[] entries)
        => new(entries);

    public string? Text(int cliloc)
        => _entries.TryGetValue(cliloc, out var text) ? text : _text;
}
