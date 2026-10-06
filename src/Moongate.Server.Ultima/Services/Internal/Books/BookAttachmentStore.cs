using System.Text.Json;
using Moongate.Core.Primitives;
using Moongate.Persistence.Interfaces;
using Moongate.Server.Ultima.Data.Internal.Books;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Interfaces.Internal.Books;
using Moongate.Server.Ultima.Types.Books;

namespace Moongate.Server.Ultima.Services.Internal.Books;

internal sealed class BookAttachmentStore : IBookAttachmentStore
{
    private readonly IWorldTransactionService _world;
    private readonly IDataAccess<BookAttachmentClaimEntity> _receipts;

    public BookAttachmentStore(IWorldTransactionService world, IDataAccess<BookAttachmentClaimEntity> receipts)
    {
        _world = world;
        _receipts = receipts;
    }

    public Task CommitAsync(BookAttachmentClaim claim, CancellationToken cancellationToken = default)
    {
        return _world.ExecuteAsync(
            async transaction =>
            {
                var items = transaction.GetDataAccess<ItemEntity>();
                foreach (var parent in claim.Parents)
                {
                    await items.UpsertAsync(parent, cancellationToken);
                }

                await items.UpsertAsync(claim.Letter, cancellationToken);
                foreach (var item in claim.Items)
                {
                    await transaction.InsertAsync(item, cancellationToken);
                }

                await transaction.InsertAsync(claim.Receipt, cancellationToken);
            },
            cancellationToken
        );
    }

    public async Task<BookAttachmentCommitState> ReconcileAsync(
        BookAttachmentClaim claim, CancellationToken cancellationToken = default
    )
    {
        var result = BookAttachmentCommitState.Uncertain;
        await _world.ExecuteAsync(
            async transaction =>
            {
                var receipt = await transaction.GetDataAccess<BookAttachmentClaimEntity>()
                    .GetByIdAsync(claim.Letter.Id, cancellationToken);
                var found = 0;
                var matching = 0;
                var data = transaction.GetDataAccess<ItemEntity>();
                foreach (var expected in claim.Items)
                {
                    var actual = await data.GetByIdAsync(expected.Id, cancellationToken);
                    if (actual is not null)
                    {
                        found++;
                        if (Matches(expected, actual))
                        {
                            matching++;
                        }
                    }
                }

                if (receipt is null && found == 0)
                {
                    result = BookAttachmentCommitState.RolledBack;
                }
                else if (receipt is not null)
                {
                    var sameReceipt = receipt.ClaimantId == claim.Receipt.ClaimantId &&
                                      receipt.ClaimedAt == claim.Receipt.ClaimedAt;
                    if (sameReceipt && matching == claim.Items.Length && matching > 0)
                    {
                        result = BookAttachmentCommitState.Committed;
                    }
                    else if (!sameReceipt && found == 0)
                    {
                        result = BookAttachmentCommitState.AlreadyClaimed;
                    }
                }
            },
            cancellationToken
        );
        return result;
    }

    public async Task<IReadOnlyCollection<Serial>> LoadClaimedIdsAsync(CancellationToken cancellationToken = default)
    {
        return (await _receipts.GetAllAsync(cancellationToken)).Select(receipt => receipt.Id).ToArray();
    }

    private static bool Matches(ItemEntity expected, ItemEntity actual)
    {
        if (expected.TemplateId != actual.TemplateId || expected.ItemId != actual.ItemId || expected.Hue != actual.Hue ||
            expected.Amount != actual.Amount || expected.Rarity != actual.Rarity || expected.Name != actual.Name ||
            expected.Movable != actual.Movable || expected.Visibility != actual.Visibility ||
            expected.DecayAt != actual.DecayAt ||
            expected.ContainerId != actual.ContainerId || expected.GridX != actual.GridX || expected.GridY != actual.GridY ||
            expected.GridIndex != actual.GridIndex || expected.MobileId != actual.MobileId ||
            expected.Layer != actual.Layer ||
            expected.Map != actual.Map || expected.X != actual.X || expected.Y != actual.Y || expected.Z != actual.Z ||
            (expected.Props?.Count ?? 0) != (actual.Props?.Count ?? 0))
        {
            return false;
        }

        foreach (var (key, value) in expected.Props ?? [])
        {
            if (actual.Props is null || !actual.Props.TryGetValue(key, out var stored) ||
                !JsonElement.DeepEquals(JsonSerializer.SerializeToElement(value), JsonSerializer.SerializeToElement(stored)))
            {
                return false;
            }
        }

        return true;
    }
}
