using MessagePack;

namespace Moongate.Tests.TestSupport.Api;

[MessagePackObject]
public sealed partial class IncrementResponse
{
    [Key(0)] public int Value { get; init; }
}
