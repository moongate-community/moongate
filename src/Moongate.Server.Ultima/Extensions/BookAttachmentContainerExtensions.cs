using DryIoc;
using Moongate.Persistence.Extensions;
using Moongate.Server.Core.Extensions;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces.Books;
using Moongate.Server.Ultima.Interfaces.Items;
using Moongate.Server.Ultima.Interfaces.Internal.Books;
using Moongate.Server.Ultima.Services.Books;
using Moongate.Server.Ultima.Services.Items;
using Moongate.Server.Ultima.Services.Internal.Books;
namespace Moongate.Server.Ultima.Extensions;
/// <summary>Registers attachment delivery against the shared server barrier and authoritative receipts.</summary>
public static class BookAttachmentContainerExtensions
{
    /// <summary>Adds insert-only receipts without a live world-save source and drains claims before logout saving.</summary>
    public static Container AddBookAttachments(this Container container)
    {
        container.AddPersistenceWorld<BookAttachmentClaimEntity>();
        container.Register<IInventoryReservationService, InventoryReservationService>(Reuse.Singleton);
        container.Register<IInventoryMutationGuard, InventoryMutationGuard>(Reuse.Singleton);
        container.Register<IBookAttachmentStore, BookAttachmentStore>(Reuse.Singleton);
        container.AddMoongateService<IBookAttachmentService, BookAttachmentService>(55);
        return container;
    }
}
