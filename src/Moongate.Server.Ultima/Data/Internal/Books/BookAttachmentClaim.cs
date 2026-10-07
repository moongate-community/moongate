using System.Collections.Immutable;
using Moongate.Server.Ultima.Entities.World;

namespace Moongate.Server.Ultima.Data.Internal.Books;

internal sealed record BookAttachmentClaim
{
    public required ItemEntity Letter { get; init; }
    public ImmutableArray<ItemEntity> Parents { get; init; } = [];
    public ImmutableArray<ItemEntity> Items { get; init; } = [];
    public required BookAttachmentClaimEntity Receipt { get; init; }
}
