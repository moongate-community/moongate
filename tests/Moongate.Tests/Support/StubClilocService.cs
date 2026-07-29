using Moongate.Server.Abstractions.Interfaces.Localization;

namespace Moongate.Tests.Support;

/// <summary>
/// Answers with a fixed text for any cliloc, or null for every one when constructed empty — which is
/// what a shard with no client files looks like.
/// </summary>
public sealed class StubClilocService : IClilocService
{
    private readonly string? _text;

    public StubClilocService(string? text = null)
    {
        _text = text;
    }

    public string? Text(int cliloc)
        => _text;
}
