using Moongate.Server.Core.Extensions;
using Moongate.Server.Ultima.Data.Gumps;
using Moongate.Server.Ultima.Types.Books;
using Moongate.Server.Ultima.Interfaces.Items;
using Moongate.Scripting.Interfaces;
using Moongate.Server.Core.Data.Sessions;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Data.Books;
using Moongate.Server.Ultima.Data.Config;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Interfaces.Books;
using Moongate.Server.Ultima.Packets.Books;
using Moongate.Server.Ultima.Services.Internal;
using Moongate.Server.Ultima.Services.Internal.Books;
using Serilog;

namespace Moongate.Server.Ultima.Services.Books;

/// <inheritdoc />
public sealed class BookDocumentService : IBookDocumentService
{
    private readonly IBookTemplateService _templates;
    private readonly BookContextFactory _contexts;
    private readonly IItemService _items;
    private readonly IMobileService _mobiles;
    private readonly IItemHandlingService _handling;
    private readonly IItemTemplateService _itemTemplates;
    private readonly ISessionService _sessions;
    private readonly IBankService _bank;
    private readonly IGumpService _gumps;
    private readonly IGameLoopService _loop;
    private readonly Lazy<IScriptEngine> _scripts;
    private readonly LocalizationConfig _localization;
    private readonly IBookAttachmentPreparationService? _attachments;
    private readonly IInventoryMutationGuard? _inventory;
    private readonly IBookAttachmentService? _claims;
    private readonly ILocalizationService? _messages;
    private readonly ISpeechService? _speech;
    private readonly IPacketSendService? _sender;
    private readonly ILogger _logger = Log.ForContext<BookDocumentService>();

    public BookDocumentService(IBookTemplateService templates, BookContextFactory contexts, IItemService items, IMobileService mobiles,
        IItemHandlingService handling, IItemTemplateService itemTemplates, ISessionService sessions, IBankService bank,
        IGumpService gumps, IGameLoopService loop, Lazy<IScriptEngine> scripts, LocalizationConfig localization, IBookAttachmentPreparationService? attachments = null, IInventoryMutationGuard? inventory = null, IBookAttachmentService? claims = null, ILocalizationService? messages = null, ISpeechService? speech = null, IPacketSendService? sender = null)
    {
        _sender = sender;
        _claims = claims;
        _messages = messages;
        _speech = speech;
        _attachments = attachments;
        _inventory = inventory;
        _templates = templates;
        _contexts = contexts;
        _items = items;
        _mobiles = mobiles;
        _handling = handling;
        _itemTemplates = itemTemplates;
        _sessions = sessions;
        _bank = bank;
        _gumps = gumps;
        _loop = loop;
        _scripts = scripts;
        _localization = localization;
    }

    public ItemEntity? Give(MobileEntity recipient, string templateId, IReadOnlyDictionary<string, object?>? values = null, string? recordedPlayerName = null)
    {
        if (!_loop.IsOnLoopThread || !IsLive(recipient) || _inventory?.AllowsOwner(recipient.Id) == false ||
            !_templates.TryRender(templateId, _contexts.Capture(recipient, recordedPlayerName), _localization.Language, values, out var rendered) ||
            rendered is null)
        {
            return null;
        }

        string? payload;
        try
        {
            if (!_templates.TryGet(rendered.TemplateId, out var source) ||
                (source.Attachments.Count > 0 && _attachments is null))
            {
                return null;
            }
            payload = _attachments?.Prepare(source);
        }
        catch (Exception exception) when (exception is InvalidDataException or KeyNotFoundException or ArgumentException)
        {
            _logger.Warning(exception, "Cannot prepare document attachments for {Template}", templateId);
            return null;
        }

        var item = _handling.Give(recipient, rendered.ItemTemplateId);
        if (item is null)
        {
            return null;
        }

        if (payload is not null)
        {
            item.SetProp(BookAttachmentCodec.PropKey, payload);
        }
        Apply(item, rendered);
        return item;
    }

    public bool Write(ItemEntity item, MobileEntity recipient, string templateId, IReadOnlyDictionary<string, object?>? values = null, string? recordedPlayerName = null)
    {
        if (!_loop.IsOnLoopThread || !IsLive(recipient) || !IsReadable(item) || _inventory?.Allows(item) == false ||
            !_templates.TryRender(templateId, _contexts.Capture(recipient, recordedPlayerName), _localization.Language, values, out var rendered) ||
            rendered is null)
        {
            return false;
        }

        if (item.Props?.TryGetValue(BookAttachmentCodec.PropKey, out var payload) == true)
        {
            if (payload is not string text || !BookAttachmentCodec.TryDecode(text, out _))
            {
                return false;
            }
        }
        else if (!_templates.TryGet(rendered.TemplateId, out var source) || source.Attachments.Count > 0)
        {
            return false;
        }
        Apply(item, rendered);
        return true;
    }

    public bool Open(ItemEntity item, MobileEntity reader)
    {
        if (!_loop.IsOnLoopThread || !IsLive(reader) ||
            !_sessions.TryGetByCharacterId(reader.Id, out var session) || !CanRead(session, reader, item))
        {
            return false;
        }

        // A book opens the client's own book; anything else the parchment.
        if (IsBook(item))
        {
            return OpenBook(session, item);
        }

        if (!TryBuild(session, reader, item, out _))
        {
            return false;
        }

        if (_scripts.Value.IsRunningScript)
        {
            return _loop.TryPost(new LoopActionWorkItem(() => OpenNow(session, reader, item)));
        }

        return OpenNow(session, reader, item);
    }

    private bool IsBook(ItemEntity item)
    {
        return _itemTemplates.TryGet(item.TemplateId, out var template) && template.ScriptId == BookTextValidation.BookScript;
    }

    // The cover (0xD4), then every page (0x66): the client asks for nothing more.
    private bool OpenBook(GameSession session, ItemEntity item)
    {
        if (_sender is null || item.GetProp<string?>("book.content") is not { } content)
        {
            return false;
        }

        BookPagesPacket pages;

        try
        {
            if (!BookPagination.TryPaginate(content, out var paginated))
            {
                _logger.Warning("Cannot open book item {Item}: its text needs more than {Pages} pages", item.Id, BookPagination.MaxPages);

                return false;
            }

            pages = new(item.Id, paginated);
        }
        catch (ArgumentException)
        {
            _logger.Warning("Cannot open book item {Item}: its pages do not fit one packet", item.Id);

            return false;
        }

        var header = new BookHeaderPacket(item.Id, pages.PageCount, item.GetProp("book.title", item.Name ?? ""), item.GetProp("book.author", ""));

        return _sender.TrySend(session.SessionId, header) && _sender.TrySend(session.SessionId, pages);
    }

    private bool OpenNow(GameSession session, MobileEntity reader, ItemEntity item)
    {
        if (!IsLive(reader) || !_sessions.TryGetByCharacterId(reader.Id, out var current) ||
            !ReferenceEquals(current, session) || !CanRead(session, reader, item) || !TryBuild(session, reader, item, out var gump) || gump is null)
        {
            return false;
        }

        _gumps.Open(session, gump);
        return true;
    }

    private bool TryBuild(GameSession session, MobileEntity reader, ItemEntity item, out Moongate.Server.Ultima.Data.Gumps.GumpInstance? gump)
    {
        var content = item.GetProp<string?>("book.content") ?? item.GetProp<string?>("jail.text");
        gump = null;
        if (content is null)
        {
            return false;
        }

        var claimLabel = _claims?.CanClaim(item, session) == true
            ? _messages.Text(BookAttachmentService.ClaimLabelMessage, "Claim attachments") : null;
        if (!BookGumpRenderer.TryBuild(item.GetProp("book.title", item.Name ?? ""), item.GetProp("book.author", ""), content,
                claimLabel, (answering, response) => HandleClaim(session, reader, item, answering, response), out gump))
        {
            _logger.Warning("Cannot display document item {Item}: invalid fields or packet capacity", item.Id);
            return false;
        }

        return true;
    }

    private void HandleClaim(GameSession original, MobileEntity reader, ItemEntity letter, GameSession answering, GumpResponse response)
    {
        if (response.ButtonId != 1 || !ReferenceEquals(original, answering) || _claims is null ||
            !IsLive(reader) || !_sessions.TryGetByCharacterId(reader.Id, out var current) || !ReferenceEquals(current, original) ||
            !_items.TryGet(letter.Id, out var live) || !ReferenceEquals(live, letter) || !_claims.CanClaim(letter, original))
        {
            return;
        }
        _ = ObserveClaimAsync(_claims.ClaimAsync(letter.Id, original), original, reader, letter);
    }

    private async Task ObserveClaimAsync(Task<BookAttachmentClaimResultType> pending, GameSession original, MobileEntity reader, ItemEntity letter)
    {
        try
        {
            var result = await pending;
            _loop.TryPost(new LoopActionWorkItem(() =>
            {
                if (!IsLive(reader) || !_sessions.TryGetByCharacterId(reader.Id, out var current) || !ReferenceEquals(current, original) ||
                    !_items.TryGet(letter.Id, out var live) || !ReferenceEquals(live, letter) || !CanRead(original, reader, letter))
                {
                    return;
                }
                var (id, fallback) = result switch
                {
                    BookAttachmentClaimResultType.Claimed => (BookAttachmentService.SuccessMessage, "The attachments are in your backpack."),
                    BookAttachmentClaimResultType.NoCapacity => (BookAttachmentService.CapacityMessage, "Your backpack has no room for all the attachments."),
                    BookAttachmentClaimResultType.Busy => (BookAttachmentService.BusyMessage, "Your backpack is busy. Try again shortly."),
                    BookAttachmentClaimResultType.Unavailable => (BookAttachmentService.UnavailableMessage, "These attachments cannot be claimed."),
                    _ => (BookAttachmentService.FailureMessage, "The attachments could not be delivered. Try again shortly.")
                };
                _speech?.Tell(reader, _messages.Text(id, fallback));
                OpenNow(original, reader, letter);
            }));
        }
        catch (Exception exception)
        {
            _logger.Error(exception, "Attachment claim for letter {Letter} did not settle safely", letter.Id);
        }
    }

    private bool CanRead(GameSession session, MobileEntity reader, ItemEntity item)
    {
        if (session.NetworkSession.Client is not { IsConnected: true } || !IsReadable(item) || !_bank.CanAccess(session, reader, item))
        {
            return false;
        }

        if (_items.GetOwner(item) is { } owner)
        {
            return owner == reader.Id;
        }

        return _items.GetGroundRoot(item) is { } root && !_handling.IsHeld(root) &&
            _items.IsLyingOnGround(root) && _items.CanReach(reader, root);
    }

    private bool IsLive(MobileEntity mobile)
    {
        return _mobiles.TryGet(mobile.Id, out var live) && ReferenceEquals(live, mobile);
    }

    private bool IsReadable(ItemEntity item)
    {
        return _items.TryGet(item.Id, out var live) && ReferenceEquals(live, item) && item.Amount == 1 &&
            !IsHeldInChain(item) && _itemTemplates.TryGet(item.TemplateId, out var template) &&
            template.Stackable == false && BookTextValidation.IsReadableScript(template.ScriptId);
    }

    private bool IsHeldInChain(ItemEntity item)
    {
        var seen = new HashSet<Moongate.Core.Primitives.Serial>();
        var current = item;
        while (true)
        {
            if (!seen.Add(current.Id) || _handling.IsHeld(current))
            {
                return true;
            }

            if (current.ContainerId is not { } parent || !_items.TryGet(parent, out var container))
            {
                return false;
            }

            current = container;
        }
    }

    private void Apply(ItemEntity item, RenderedBook rendered)
    {
        BookDocumentText.Apply(item, rendered);
        _handling.Refresh(item);
    }
}
