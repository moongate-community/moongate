using MessagePack;

namespace Moongate.Api.Tests.TestSupport.Contracts;

[MessagePackObject]
public sealed partial class IncrementResponse
{
    [Key(0)] public int Value { get; init; }
}
