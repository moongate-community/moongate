using System.Collections.Immutable;
using Moongate.Server.Ultima.Services.Books;
using Moongate.Core.Primitives;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Core.Interfaces.Persistence;
using Moongate.Server.Ultima.Data.Internal.Books;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Extensions;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Interfaces.Books;
using Moongate.Server.Ultima.Interfaces.Items;
using Moongate.Server.Ultima.Interfaces.Internal.Books;
using Moongate.Server.Ultima.Services.Internal;
using Moongate.Server.Ultima.Services.Internal.Books;
using Moongate.Server.Ultima.Types.Books;
using Moongate.Server.Ultima.Utils;
using Moongate.Ultima.Types;
using Serilog;

namespace Moongate.Server.Ultima.Services.Internal.Books;

internal sealed class BookAttachmentService : IBookAttachmentService
{
    public const int ClaimLabelMessage = 30169;
    public const int SuccessMessage = 30170;
    public const int CapacityMessage = 30171;
    public const int UnavailableMessage = 30172;
    public const int BusyMessage = 30173;
    public const int FailureMessage = 30174;
    private static readonly ILogger Logger = Log.ForContext<BookAttachmentService>();
    private readonly IItemService _items;
    private readonly IMobileService _mobiles;
    private readonly ISessionService _sessions;
    private readonly IItemTemplateService _templates;
    private readonly ITileDataService _tiles;
    private readonly IItemHandlingService _handling;
    private readonly IWeightService _weights;
    private readonly IContainerCapacityService _capacity;
    private readonly IItemSerialPool _serials;
    private readonly IGameLoopService _loop;
    private readonly IInventoryReservationService _reservations;
    private readonly IPersistenceOperationBarrier _barrier;
    private readonly IBookAttachmentStore _store;
    private readonly IContainerLayoutService? _layouts;
    private readonly HashSet<Serial> _claimed = [];
    private readonly Lock _gate = new();
    private readonly HashSet<Task> _pending = [];
    private bool _accepting;

    public BookAttachmentService(IItemService items, IMobileService mobiles, ISessionService sessions,
        IItemTemplateService templates, ITileDataService tiles, IItemHandlingService handling,
        IWeightService weights, IItemSerialPool serials, IGameLoopService loop,
        IInventoryReservationService reservations, IPersistenceOperationBarrier barrier, IContainerCapacityService capacity, IBookAttachmentStore store,
        IContainerLayoutService? layouts = null)
    {
        _items = items;
        _mobiles = mobiles;
        _sessions = sessions;
        _templates = templates;
        _tiles = tiles;
        _handling = handling;
        _weights = weights;
        _capacity = capacity;
        _serials = serials;
        _loop = loop;
        _reservations = reservations;
        _barrier = barrier;
        _store = store;
        _layouts = layouts;
    }

    public bool CanClaim(ItemEntity letter, GameSession session)
    {
        return _loop.IsOnLoopThread && Volatile.Read(ref _accepting) &&
            !_reservations.IsReserved(session.CharacterId) && Eligible(letter, session, out _, out _);
    }

    public Task<BookAttachmentClaimResultType> ClaimAsync(Serial letterId, GameSession session)
    {
        if (!_loop.IsOnLoopThread || !Volatile.Read(ref _accepting))
        {
            return Task.FromResult(BookAttachmentClaimResultType.Unavailable);
        }
        if (_reservations.IsReserved(session.CharacterId))
        {
            return Task.FromResult(BookAttachmentClaimResultType.Busy);
        }
        if (!_items.TryGet(letterId, out var letter) || !Eligible(letter, session, out _, out _) ||
            !_mobiles.TryGet(session.CharacterId, out var character))
        {
            return Task.FromResult(BookAttachmentClaimResultType.Unavailable);
        }

        var settled = new TaskCompletionSource<BookAttachmentClaimResultType>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_reservations.TryReserve(character.Id, settled.Task))
        {
            return Task.FromResult(BookAttachmentClaimResultType.Busy);
        }
        Task admitted;
        var result = BookAttachmentClaimResultType.Failed;
        try
        {
            // Admission is synchronous; every callback step, including the first loop capture, runs off-loop.
            admitted = _barrier.ExecuteAsync(_ => Task.Run(async () => result = await WithdrawAsync(letter, character, session)), CancellationToken.None);
        }
        catch (Exception exception)
        {
            _reservations.Release(character.Id);
            Logger.Warning(exception, "Attachment withdrawal admission refused for {Letter}", letterId);
            settled.SetResult(BookAttachmentClaimResultType.Failed);
            return settled.Task;
        }
        lock (_gate)
        {
            _pending.Add(settled.Task);
        }
        _ = ObserveAsync();
        return settled.Task;

        async Task ObserveAsync()
        {
            try
            {
                await admitted;
                settled.TrySetResult(result);
            }
            catch (Exception exception)
            {
                // An unsafe result retains the reservation. Logout and final capture must also fail closed.
                Logger.Fatal(exception, "Unsafe attachment settlement for {Letter}; inventory remains excluded", letterId);
                settled.TrySetException(exception);
            }
            finally
            {
                lock (_gate)
                {
                    _pending.Remove(settled.Task);
                }
            }
        }
    }

    public async Task StartAsync()
    {
        var ids = await _store.LoadClaimedIdsAsync();
        await OnLoopAsync(() =>
        {
            _claimed.UnionWith(ids);
            Volatile.Write(ref _accepting, true);
        });
    }

    public async Task StopAsync()
    {
        // Serialize admission closure with ClaimAsync on the owner loop before taking the drain snapshot.
        await OnLoopAsync(() => Volatile.Write(ref _accepting, false));
        Task[] pending;
        lock (_gate)
        {
            pending = _pending.ToArray();
        }
        await Task.WhenAll(pending);
    }

    private async Task<BookAttachmentClaimResultType> WithdrawAsync(ItemEntity letter, MobileEntity character, GameSession session)
    {
        BookAttachmentClaim? claim = null;
        var result = BookAttachmentClaimResultType.Unavailable;
        await OnLoopAsync(() => _reservations.Apply(character.Id, () =>
        {
            if (!_mobiles.TryGet(character.Id, out var live) || !ReferenceEquals(live, character) ||
                !Eligible(letter, session, out var parents, out var batch))
            {
                return;
            }
            var backpack = parents![0];
            var rewards = batch!.Items.Select(BookAttachmentCodec.Materialize).ToList();
            var contents = _items.GetContents(backpack.Id).ToList();
            var occupied = contents.Where(item => item.GridIndex is >= 0 and < ContainerSlotUtils.SlotCount)
                                   .Select(item => item.GridIndex!.Value).ToHashSet();
            if (contents.Count + rewards.Count > ContainerSlotUtils.SlotCount ||
                ContainerSlotUtils.SlotCount - occupied.Count < rewards.Count || !_capacity.HasRoomFor(backpack, rewards.Count) ||
                !_weights.Holds(backpack, rewards))
            {
                result = BookAttachmentClaimResultType.NoCapacity;
                return;
            }
            foreach (var reward in rewards)
            {
                if (!_serials.TryTake(out var serial))
                {
                    result = BookAttachmentClaimResultType.Failed;
                    return;
                }
                if (!serial.IsItem || _items.TryGet(serial, out _))
                {
                    throw new InvalidOperationException("Reserved attachment serial is invalid or already live.");
                }
                reward.Id = serial;
                reward.PutInContainer(backpack.Id, _layouts?.RandomGridPosition(backpack.ItemId) ?? new(44, 65), ContainerSlotUtils.FirstFree(contents));
                contents.Add(reward);
            }
            claim = new()
            {
                Letter = letter.Snapshot(), Parents = parents.Select(item => item.Snapshot()).ToImmutableArray(),
                Items = rewards.ToImmutableArray(),
                Receipt = new() { Id = letter.Id, ClaimantId = character.Id, ClaimedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }
            };
        }));
        if (claim is null)
        {
            await OnLoopAsync(() => _reservations.Release(character.Id));
            return result;
        }

        var state = BookAttachmentCommitState.Committed;
        try
        {
            await _store.CommitAsync(claim);
        }
        catch (Exception exception)
        {
            Logger.Warning(exception, "Reconciling attachment withdrawal for {Letter}", letter.Id);
            state = await _store.ReconcileAsync(claim);
        }
        if (state == BookAttachmentCommitState.Uncertain)
        {
            throw new InvalidOperationException($"Cannot establish durable attachment outcome for {letter.Id}.");
        }
        await OnLoopAsync(() =>
        {
            if (state == BookAttachmentCommitState.Committed)
            {
                _reservations.Apply(character.Id, () =>
                {
                    // No scripts or client notifications in this scope. Partial application is a critical fault.
                    _items.Add(claim.Items);
                    _claimed.Add(letter.Id);
                });
                result = BookAttachmentClaimResultType.Claimed;
            }
            else if (state == BookAttachmentCommitState.AlreadyClaimed)
            {
                _claimed.Add(letter.Id);
                result = BookAttachmentClaimResultType.Unavailable;
            }
            else
            {
                result = BookAttachmentClaimResultType.Failed;
            }
            _reservations.Release(character.Id);
            if (state == BookAttachmentCommitState.Committed && CurrentSession(session))
            {
                try
                {
                    foreach (var reward in claim.Items)
                    {
                        _handling.Refresh(reward);
                    }
                }
                catch (Exception exception)
                {
                    Logger.Warning(exception, "Attachment delivery succeeded but refreshing session {Session} failed", session.SessionId);
                }
            }
        });
        return result;
    }

    private bool Eligible(ItemEntity letter, GameSession session, out List<ItemEntity>? parents, out BookAttachmentPayload? batch)
    {
        parents = null;
        batch = null;
        if (!CurrentSession(session) || !_mobiles.TryGet(session.CharacterId, out _) || _claimed.Contains(letter.Id) ||
            !_items.TryGet(letter.Id, out var live) || !ReferenceEquals(live, letter) || letter.Amount != 1 ||
            !_templates.TryGet(letter.TemplateId, out var template) || template.Stackable != false ||
            !BookTextValidation.IsReadableScript(template.ScriptId) ||
            letter.Props?.GetValueOrDefault(BookAttachmentCodec.PropKey) is not string payload ||
            !BookAttachmentCodec.TryDecode(payload, out batch) ||
            batch!.Items.Any(reward => !_templates.TryGet(reward.TemplateId, out var current) ||
                reward.Amount > 1 && !current.EffectiveStackable(_tiles)))
        {
            return false;
        }
        var ancestors = new List<ItemEntity>();
        var seen = new HashSet<Serial>();
        var currentItem = letter;
        while (seen.Add(currentItem.Id))
        {
            if (_handling.IsHeld(currentItem))
            {
                return false;
            }
            if (currentItem.MobileId is { } owner)
            {
                if (owner != session.CharacterId || currentItem.Layer != LayerType.Backpack || ancestors.Count == 0)
                {
                    return false;
                }
                ancestors.Reverse();
                parents = ancestors;
                return true;
            }
            if (currentItem.ContainerId is not { } parent || !_items.TryGet(parent, out currentItem!))
            {
                return false;
            }
            ancestors.Add(currentItem);
        }
        return false;
    }

    private bool CurrentSession(GameSession session)
    {
        return session.NetworkSession.Client is { IsConnected: true } && session.CharacterId.IsValid &&
            _sessions.TryGetByCharacterId(session.CharacterId, out var current) && ReferenceEquals(current, session);
    }

    private async Task OnLoopAsync(Action action)
    {
        var work = new LoopActionWorkItem(action);
        try
        {
            await _loop.PostAsync(work);
        }
        catch (InvalidOperationException) when (_loop.Completion.IsCompleted)
        {
            await _loop.Completion;
            throw;
        }
        await Task.WhenAny(work.Completion, _loop.Completion);
        if (!work.Completion.IsCompleted)
        {
            await _loop.Completion;
            throw new InvalidOperationException("The game loop stopped before attachment settlement.");
        }
        await work.Completion;
    }
}
