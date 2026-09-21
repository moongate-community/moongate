using MessagePack;
using Moongate.Api.Attributes;
using Moongate.Api.Interfaces.Contracts;

namespace Moongate.Tests.TestSupport.Api;

[ApiOperation(100), MessagePackObject]
public sealed partial class IncrementRequest : IApiRequest<IncrementResponse>
{
    [Key(0)] public int Value { get; init; }
}
