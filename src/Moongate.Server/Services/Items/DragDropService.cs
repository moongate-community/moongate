using Moongate.Core.Geometry;
using Moongate.Core.Interfaces;
using Moongate.Core.Primitives;
using Moongate.Network.Packets.Outgoing;
using Moongate.Network.Types;
using Moongate.Persistence.Entities;
using Moongate.Server.Abstractions.Data.Events;
using Moongate.Server.Abstractions.Data.Internal;
using Moongate.Server.Abstractions.Data.Session;
using Moongate.Server.Abstractions.Interfaces.Items;
using Moongate.Server.Abstractions.Interfaces.World;
using Moongate.UO.Data.Items;
using SquidStd.Core.Interfaces.Events;

namespace Moongate.Server.Services.Items;

/// <summary>
/// Owns the drag-and-drop game rules. <see cref="Evaluate" /> is the pure decision core — public and
/// static so the rules are unit-testable without a live session, mirroring
/// <c>MovementService.Evaluate</c>. The rest of the service is orchestration: detach, hold, place,
/// bounce, and the packets that follow.
/// </summary>
public sealed class DragDropService : IDragDropService
{
    /// <summary>How close the player must be to lift something, in tiles. ModernUO uses the same 2.</summary>
    public const int LiftRange = 2;

    private const int MaxStackAmount = 60000;

    /// <summary>Where a bounced item lands in the backpack — the base slot CharacterService uses.</summary>
    private static readonly Point2D BounceSlot = new(44, 65);

    private readonly IItemService _items;
    private readonly IItemFactoryService _itemFactory;
    private readonly IItemTemplateService _templates;
    private readonly IWorldService _world;
    private readonly ILoopAffinity? _loopAffinity;
    private readonly IEventBus? _eventBus;

    public DragDropService(
        IItemService items,
        IItemFactoryService itemFactory,
        IItemTemplateService templates,
        IWorldService world,
        ILoopAffinity? loopAffinity = null,
        IEventBus? eventBus = null
    )
    {
        _items = items;
        _itemFactory = itemFactory;
        _templates = templates;
        _world = world;
        _loopAffinity = loopAffinity;
        _eventBus = eventBus;
    }

    /// <summary>
    /// Decides whether <paramref name="actor" /> may lift an item, in ModernUO's order: already
    /// holding, then range, then whether the item can be moved at all, then whether the actor can
    /// reach it. <paramref name="reachable" /> is computed by the caller, which needs the store to
    /// walk the container chain; everything else here is a plain value.
    /// </summary>
    public static LiftDecision Evaluate(
        MobileEntity actor,
        int itemMapId,
        Point3D itemWorldPosition,
        ItemTemplate? template,
        Serial heldItemId,
        bool reachable
    )
    {
        if (heldItemId != Serial.Zero)
        {
            return new(false, LiftRejectReasonType.AreHolding);
        }

        if (actor.MapId != itemMapId || !actor.Position.InRange(itemWorldPosition, LiftRange))
        {
            return new(false, LiftRejectReasonType.OutOfRange);
        }

        // A template that cannot be resolved is treated as unmovable: better a refused lift than an
        // item whose rules are unknown.
        if (template is null || !template.IsMovable)
        {
            return new(false, LiftRejectReasonType.CannotLift);
        }

        if (!reachable)
        {
            return new(false, LiftRejectReasonType.CannotLift);
        }

        return new(true, LiftRejectReasonType.Inspecific);
    }

    public LiftDecision Lift(
        MobileEntity actor,
        Serial itemId,
        int amount,
        Serial heldItemId,
        out Serial heldId,
        out HeldItemOrigin? origin
    )
    {
        _loopAffinity?.AssertOnLoop("drag_drop.lift");

        heldId = Serial.Zero;
        origin = null;

        if (_items.GetById(itemId) is not { } item)
        {
            return new(false, LiftRejectReasonType.Inspecific);
        }

        var (mapId, position) = WorldLocation(item, actor);
        var decision = Evaluate(
            actor,
            mapId,
            position,
            _templates.GetById(item.TemplateId),
            heldItemId,
            Reachable(item, actor)
        );

        if (!decision.Accepted)
        {
            return decision;
        }

        SplitStack(item, amount);

        // Snapshot before detaching: Detach clears the very fields the origin is made of.
        origin = HeldItemOrigin.From(item);
        heldId = item.Id;

        _items.Detach(item);

        // Everyone who could see it there stops seeing it. ModernUO reaches the same end state by
        // internalizing the item, which triggers the removal through the map-change path.
        _world.SendToPlayersInRange(
            mapId,
            position,
            PlayerSession.MaxViewRange,
            new DeleteObjectPacket(item.Id)
        );

        return decision;
    }

    public void Bounce(MobileEntity actor, Serial itemId, HeldItemOrigin? origin)
    {
        _loopAffinity?.AssertOnLoop("drag_drop.bounce");

        // Already gone — deleted, or merged into a stack by a drop that then failed elsewhere.
        if (_items.GetById(itemId) is not { } item)
        {
            return;
        }

        if (origin is not null && BounceToOrigin(actor, item, origin))
        {
            return;
        }

        if (actor.BackpackId != Serial.Zero && _items.GetById(actor.BackpackId) is { } backpack)
        {
            _items.AddToContainer(backpack, item, BounceSlot);

            return;
        }

        // No origin left and nowhere to carry it: it lands where the player stands.
        PlaceOnGround(item, actor.MapId, actor.Position);
    }

    public LiftDecision Drop(
        MobileEntity actor,
        Serial heldItemId,
        Serial containerId,
        Point3D groundPosition,
        Point2D containerPosition
    )
    {
        _loopAffinity?.AssertOnLoop("drag_drop.drop");

        // The held serial is the authority. A drop naming anything else is a desynced or hostile
        // client, and honouring it would let one move items it never lifted.
        if (heldItemId == Serial.Zero || _items.GetById(heldItemId) is not { } item)
        {
            return new(false, LiftRejectReasonType.Inspecific);
        }

        return containerId == Serial.Zero
            ? DropOnGround(actor, item, groundPosition)
            : DropInContainer(actor, item, containerId, containerPosition);
    }

    /// <summary>
    /// Whether <paramref name="dropped" /> can merge into <paramref name="existing" />: the same thing,
    /// the same colour, both declared stackable, and a total the client can represent.
    /// </summary>
    public static bool CanStack(
        ItemEntity existing,
        ItemEntity dropped,
        ItemTemplate? existingTemplate,
        ItemTemplate? droppedTemplate
    )
        => existing.Id != dropped.Id &&
           existingTemplate?.Stackable == true &&
           droppedTemplate?.Stackable == true &&
           existing.TemplateId == dropped.TemplateId &&
           existing.ItemId == dropped.ItemId &&
           existing.Hue == dropped.Hue &&
           existing.Amount + dropped.Amount <= MaxStackAmount;

    /// <summary>
    /// Puts the item back exactly where it was lifted from, or reports false so the caller can fall
    /// back. ModernUO's Item.Bounce does the same, dropping to the player's feet when the recorded
    /// parent has gone away.
    /// </summary>
    private bool BounceToOrigin(MobileEntity actor, ItemEntity item, HeldItemOrigin origin)
    {
        if (origin.ContainerId != Serial.Zero)
        {
            if (_items.GetById(origin.ContainerId) is not { } container)
            {
                return false;
            }

            _items.AddToContainer(container, item, origin.ContainerPosition);

            return true;
        }

        if (origin.EquippedMobileId != Serial.Zero)
        {
            // Only ever back onto the actor's own layers, and only if the layer is still free —
            // something else may have been equipped while this item was in the air.
            if (origin.EquippedMobileId != actor.Id ||
                origin.EquippedLayer is not { } layer ||
                actor.EquippedItemIds.ContainsKey(layer))
            {
                return false;
            }

            _items.Equip(actor, item, layer);

            return true;
        }

        PlaceOnGround(item, origin.MapId, origin.WorldPosition);

        return true;
    }

    private LiftDecision DropOnGround(MobileEntity actor, ItemEntity item, Point3D position)
    {
        if (!actor.Position.InRange(position, LiftRange))
        {
            return new(false, LiftRejectReasonType.OutOfRange);
        }

        PlaceOnGround(item, actor.MapId, position);

        _eventBus?.Publish(new ItemDroppedEvent(item.Id, actor.Id, Serial.Zero));

        return new(true, LiftRejectReasonType.Inspecific);
    }

    private LiftDecision DropInContainer(
        MobileEntity actor,
        ItemEntity item,
        Serial containerId,
        Point2D position
    )
    {
        // A container inside itself would take its whole contents out of reach for good.
        if (containerId == item.Id || _items.GetById(containerId) is not { } container)
        {
            return new(false, LiftRejectReasonType.CannotLift);
        }

        if (IsDescendantOf(container, item.Id) || !Reachable(container, actor))
        {
            return new(false, LiftRejectReasonType.CannotLift);
        }

        var (containerMapId, containerWorldPosition) = WorldLocation(container, actor);

        if (actor.MapId != containerMapId || !actor.Position.InRange(containerWorldPosition, LiftRange))
        {
            return new(false, LiftRejectReasonType.OutOfRange);
        }

        var stack = FindStack(container, item);

        if (stack is not null)
        {
            stack.Amount += item.Amount;
            _items.Save(stack);
            _items.Delete(item.Id);

            _world.SendToPlayer(actor.Id, ContainerPacket(stack, container.Id, stack.ContainerPosition));

            _eventBus?.Publish(new ItemDroppedEvent(stack.Id, actor.Id, container.Id));

            return new(true, LiftRejectReasonType.Inspecific);
        }

        _items.AddToContainer(container, item, position);

        _world.SendToPlayer(actor.Id, ContainerPacket(item, container.Id, position));

        _eventBus?.Publish(new ItemDroppedEvent(item.Id, actor.Id, container.Id));

        return new(true, LiftRejectReasonType.Inspecific);
    }

    private ItemEntity? FindStack(ItemEntity container, ItemEntity dropped)
    {
        var droppedTemplate = _templates.GetById(dropped.TemplateId);

        foreach (var candidate in _items.GetContents(container.Id))
        {
            if (CanStack(candidate, dropped, _templates.GetById(candidate.TemplateId), droppedTemplate))
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>True when <paramref name="candidate" /> sits anywhere inside <paramref name="ancestorId" />.</summary>
    private bool IsDescendantOf(ItemEntity candidate, Serial ancestorId)
    {
        var current = candidate;

        for (var depth = 0; current.ParentContainerId != Serial.Zero && depth < 32; depth++)
        {
            if (current.ParentContainerId == ancestorId)
            {
                return true;
            }

            if (_items.GetById(current.ParentContainerId) is not { } parent)
            {
                return false;
            }

            current = parent;
        }

        return false;
    }

    private void PlaceOnGround(ItemEntity item, int mapId, Point3D position)
    {
        _items.MoveToWorld(item, mapId, position);

        _world.SendToPlayersInRange(
            mapId,
            position,
            PlayerSession.MaxViewRange,
            new WorldItemPacket(item.Id, (ushort)item.ItemId, (ushort)item.Amount, position, item.Hue)
        );
    }

    private static AddItemToContainerPacket ContainerPacket(ItemEntity item, Serial containerId, Point2D position)
        => new(item.Id, (ushort)item.ItemId, (ushort)item.Amount, position, containerId, item.Hue);

    /// <summary>
    /// Walks up the container chain to the item that is actually somewhere — on the ground or on a
    /// mobile. The guard stops a corrupted cycle from hanging the loop thread.
    /// </summary>
    private ItemEntity Root(ItemEntity item)
    {
        var current = item;

        for (var depth = 0; current.ParentContainerId != Serial.Zero && depth < 32; depth++)
        {
            if (_items.GetById(current.ParentContainerId) is not { } parent)
            {
                break;
            }

            current = parent;
        }

        return current;
    }

    /// <summary>
    /// Where an item is in the world, for the range check: its own position when it lies on the
    /// ground, its root's otherwise. An item worn by the actor is wherever the actor is; one worn by
    /// somebody else keeps the stale coordinates of an equipped entity, which fails the range check —
    /// the outcome we want anyway, since Reachable refuses it too.
    /// </summary>
    private (int MapId, Point3D Position) WorldLocation(ItemEntity item, MobileEntity actor)
    {
        var root = Root(item);

        return root.EquippedMobileId == actor.Id
            ? (actor.MapId, actor.Position)
            : (root.MapId, root.Position);
    }

    /// <summary>
    /// Whether the actor may touch this item at all. Their own worn items and anything inside their
    /// own containers, yes; anything on another mobile, never; anything on the ground, yes — how far
    /// away it may be is the range check's job, not this one's.
    /// </summary>
    private bool Reachable(ItemEntity item, MobileEntity actor)
    {
        var root = Root(item);

        if (root.EquippedMobileId == actor.Id)
        {
            return true;
        }

        return root.EquippedMobileId == Serial.Zero;
    }

    /// <summary>
    /// ModernUO's inversion (Mobile.LiftItemDupe): the ORIGINAL entity keeps travelling on the cursor
    /// with the lifted amount, and the NEW entity is the remainder left behind. The serial the client
    /// is dragging therefore never changes mid-drag.
    /// </summary>
    private void SplitStack(ItemEntity item, int amount)
    {
        var lifted = Math.Clamp(amount, 1, item.Amount);

        if (lifted >= item.Amount)
        {
            return;
        }

        var created = _itemFactory.CreateFromTemplate(
            item.TemplateId,
            amount: item.Amount - lifted,
            hue: item.Hue
        );

        // No template to build the remainder from: leave the stack whole rather than lose the rest.
        if (created.Count == 0)
        {
            return;
        }

        var remainder = created[0];
        _items.Create(remainder);

        if (item.ParentContainerId != Serial.Zero && _items.GetById(item.ParentContainerId) is { } container)
        {
            _items.AddToContainer(container, remainder, item.ContainerPosition);
        }
        else
        {
            _items.MoveToWorld(remainder, item.MapId, item.Position);
        }

        item.Amount = lifted;
        _items.Save(item);
    }
}
