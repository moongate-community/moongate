using System.Collections.Immutable;

namespace Moongate.Server.Ultima.Data.Internal.Books;

internal sealed record BookAttachmentPayload
{
    public int Version { get; init; } = 1;
    public ImmutableArray<FrozenBookAttachment> Items { get; init; } = [];
}
