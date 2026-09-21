using MessagePack;

namespace Moongate.Tests.TestSupport.Api;

[MessagePackObject]
public sealed class IncrementResponse
{
    [Key(0)]
    public int Value { get; init; }
}
