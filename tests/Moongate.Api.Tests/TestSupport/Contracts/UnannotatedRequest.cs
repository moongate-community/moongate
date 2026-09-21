using MessagePack;
using Moongate.Api.Interfaces.Contracts;

namespace Moongate.Api.Tests.TestSupport.Contracts;

[MessagePackObject]
public sealed class UnannotatedRequest : IApiRequest<IncrementResponse>
{
    [Key(0)]
    public int Value { get; init; }
}
