using MessagePack;

namespace Moongate.Api.TestHost.Data;

[MessagePackObject]
public sealed class IncrementResponse
{
    [Key(0)]
    public int Value { get; init; }
}
