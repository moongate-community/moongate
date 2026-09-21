using MessagePack;

namespace Moongate.Api.TestHost.Data;

[MessagePackObject]
public sealed partial class IncrementResponse
{
    [Key(0)] public int Value { get; init; }
}
