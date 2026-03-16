using Moongate.Network.Packets.Data.Packets;
using Moongate.Network.Packets.Incoming.Books;
using Moongate.Server.Data.Items;
using Moongate.Server.Data.Internal.Scripting;
using Moongate.Server.Data.Session;
using Moongate.Server.Handlers;
using Moongate.Server.Interfaces.Items;
using Moongate.Server.Interfaces.Services.Entities;
using Moongate.Server.Interfaces.Services.Items;
using Moongate.Server.Interfaces.Services.Packets;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Types;

namespace Moongate.Server.Services.Items;

public class ItemBookService : IItemBookService
{
    private const int BookLinesPerPage = 8;
    private const int DefaultWritableBookPages = 20;

    private readonly IItemService _itemService;
    private readonly IMobileService _mobileService;
    private readonly IOutgoingPacketQueue _outgoingPacketQueue;

    public ItemBookService(
        IItemService itemService,
        IMobileService mobileService,
        IOutgoingPacketQueue outgoingPacketQueue
    )
    {
        _itemService = itemService;
        _mobileService = mobileService;
        _outgoingPacketQueue = outgoingPacketQueue;
    }

    public async Task<bool> TryEnqueueBookAsync(
        GameSession session,
        UOItemEntity item,
        CancellationToken cancellationToken = default
    )
    {
        _ = cancellationToken;

        if (!TryReadBook(item, out var title, out var author, out var content))
        {
            return false;
        }

        var pageCount = GetBookPageCount(item, content);
        var pages = BuildBookPages(content, pageCount);
        var header = new BookHeaderNewPacket
        {
            BookSerial = item.Id.Value,
            Flag1 = true,
            IsWritable = IsWritableBook(item),
            PageCount = (ushort)pageCount,
            Title = title,
            Author = author
        };

        var packet = new BookPagesPacket
        {
            BookSerial = item.Id.Value,
            PageCount = (ushort)pageCount
        };

        for (var i = 0; i < pages.Count; i++)
        {
            var page = new BookPageEntry
            {
                PageNumber = (ushort)(i + 1),
                LineCount = (ushort)pages[i].Count
            };

            page.Lines.AddRange(pages[i]);
            packet.Pages.Add(page);
        }

        Enqueue(session, header);
        Enqueue(session, packet);

        return true;
    }

    public async Task<bool> HandleBookPagesAsync(
        GameSession session,
        BookPagesPacket packet,
        CancellationToken cancellationToken = default
    )
    {
        _ = cancellationToken;
        var item = await _itemService.GetItemAsync((Serial)packet.BookSerial);

        if (item is null || !TryReadBook(item, out _, out _, out var content))
        {
            return true;
        }

        var pageCount = GetBookPageCount(item, content);

        if (packet.Pages.Any(static page => !page.IsPageRequest))
        {
            if (!IsWritableBook(item) || !await CanWriteBookAsync(session, item))
            {
                return true;
            }

            ApplyBookPageUpdates(item, packet.Pages);
            await _itemService.UpsertItemAsync(item);
            Enqueue(session, ToolTipHandler.CreateItemPropertyList(item));

            return true;
        }

        var requestedPages = BuildRequestedBookPages(content, packet.Pages, pageCount);

        if (requestedPages.Count == 0)
        {
            return true;
        }

        var response = new BookPagesPacket
        {
            BookSerial = packet.BookSerial,
            PageCount = (ushort)requestedPages.Count
        };

        response.Pages.AddRange(requestedPages);
        Enqueue(session, response);

        return true;
    }

    public async Task<bool> HandleBookHeaderAsync(
        GameSession session,
        BookHeaderNewPacket packet,
        CancellationToken cancellationToken = default
    )
    {
        _ = cancellationToken;
        var item = await _itemService.GetItemAsync((Serial)packet.BookSerial);

        if (item is null || !IsWritableBook(item) || !await CanWriteBookAsync(session, item))
        {
            return true;
        }

        item.SetCustomString(ItemCustomParamKeys.Book.Title, SanitizeBookText(packet.Title));
        item.SetCustomString(ItemCustomParamKeys.Book.Author, SanitizeBookText(packet.Author));
        await _itemService.UpsertItemAsync(item);
        Enqueue(session, ToolTipHandler.CreateItemPropertyList(item));

        return true;
    }

    private void Enqueue(GameSession session, Moongate.Network.Packets.Interfaces.IGameNetworkPacket packet)
        => _outgoingPacketQueue.Enqueue(session.SessionId, packet);

    private static bool TryReadBook(UOItemEntity item, out string title, out string author, out string content)
    {
        title = string.Empty;
        author = string.Empty;
        content = string.Empty;

        if (!item.TryGetCustomString(ItemCustomParamKeys.Book.Title, out title) ||
            !item.TryGetCustomString(ItemCustomParamKeys.Book.Author, out author) ||
            !item.TryGetCustomString(ItemCustomParamKeys.Book.Content, out content))
        {
            return false;
        }

        return true;
    }

    private static bool IsWritableBook(UOItemEntity item)
    {
        if (item.TryGetCustomBoolean(ItemCustomParamKeys.Book.Writable, out var writable))
        {
            return writable;
        }

        return item.TryGetCustomString(ItemCustomParamKeys.Book.Writable, out var stringValue) &&
               bool.TryParse(stringValue, out writable) &&
               writable;
    }

    private async Task<bool> CanWriteBookAsync(GameSession session, UOItemEntity item)
    {
        if (session.CharacterId == Serial.Zero)
        {
            return false;
        }

        if (item.EquippedMobileId == session.CharacterId)
        {
            return true;
        }

        var mobile = await _mobileService.GetAsync(session.CharacterId);

        if (mobile is null)
        {
            return false;
        }

        var backpackId = ResolveBackpackId(mobile);

        if (backpackId == Serial.Zero)
        {
            return false;
        }

        var currentContainerId = item.ParentContainerId;

        while (currentContainerId != Serial.Zero)
        {
            if (currentContainerId == backpackId)
            {
                return true;
            }

            var container = await _itemService.GetItemAsync(currentContainerId);

            if (container is null)
            {
                return false;
            }

            if (container.EquippedMobileId == session.CharacterId)
            {
                return true;
            }

            currentContainerId = container.ParentContainerId;
        }

        return false;
    }

    private static Serial ResolveBackpackId(UOMobileEntity mobile)
    {
        if (mobile.BackpackId != Serial.Zero)
        {
            return mobile.BackpackId;
        }

        return mobile.EquippedItemIds.TryGetValue(ItemLayerType.Backpack, out var equippedBackpackId)
                   ? equippedBackpackId
                   : Serial.Zero;
    }

    private static List<BookPageEntry> BuildRequestedBookPages(
        string content,
        IReadOnlyList<BookPageEntry> requestedPages,
        int pageCount
    )
    {
        var allPages = BuildBookPages(content, pageCount);
        var responsePages = new List<BookPageEntry>();

        foreach (var requestedPage in requestedPages)
        {
            if (!requestedPage.IsPageRequest)
            {
                continue;
            }

            var pageIndex = requestedPage.PageNumber - 1;

            if (pageIndex < 0 || pageIndex >= allPages.Count)
            {
                continue;
            }

            var responsePage = new BookPageEntry
            {
                PageNumber = requestedPage.PageNumber,
                LineCount = (ushort)allPages[pageIndex].Count
            };

            responsePage.Lines.AddRange(allPages[pageIndex]);
            responsePages.Add(responsePage);
        }

        return responsePages;
    }

    private static List<List<string>> BuildBookPages(string content, int minimumPageCount = 1)
    {
        var normalized = string.IsNullOrWhiteSpace(content)
                             ? string.Empty
                             : content.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        var lines = normalized.Length == 0 ? [] : normalized.Split('\n').ToList();

        var pages = new List<List<string>>();

        for (var index = 0; index < lines.Count; index += BookLinesPerPage)
        {
            pages.Add(lines.Skip(index).Take(BookLinesPerPage).ToList());
        }

        while (pages.Count < Math.Max(1, minimumPageCount))
        {
            pages.Add([]);
        }

        return pages;
    }

    private static void ApplyBookPageUpdates(UOItemEntity item, IReadOnlyList<BookPageEntry> updatedPages)
    {
        item.TryGetCustomString(ItemCustomParamKeys.Book.Content, out var content);
        var lines = SplitBookContent(content).ToList();

        foreach (var page in updatedPages.Where(static page => !page.IsPageRequest))
        {
            var startLineIndex = Math.Max(0, page.PageNumber - 1) * BookLinesPerPage;
            var lineCount = page.LineCount == 0 ? page.Lines.Count : Math.Min(page.LineCount, page.Lines.Count);

            while (lines.Count < startLineIndex)
            {
                lines.Add(string.Empty);
            }

            for (var i = 0; i < lineCount; i++)
            {
                var targetIndex = startLineIndex + i;

                while (lines.Count <= targetIndex)
                {
                    lines.Add(string.Empty);
                }

                lines[targetIndex] = page.Lines[i];
            }
        }

        item.SetCustomString(ItemCustomParamKeys.Book.Content, string.Join('\n', lines));
    }

    private static IEnumerable<string> SplitBookContent(string? content)
    {
        var normalized = string.IsNullOrWhiteSpace(content)
                             ? string.Empty
                             : content.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');

        return normalized.Length == 0 ? [] : normalized.Split('\n');
    }

    private static int GetBookPageCount(UOItemEntity item, string content)
    {
        if (item.TryGetCustomInteger(ItemCustomParamKeys.Book.Pages, out var configuredPages) && configuredPages > 0)
        {
            return (int)configuredPages;
        }

        if (IsWritableBook(item))
        {
            return DefaultWritableBookPages;
        }

        return Math.Max(1, BuildBookPages(content).Count);
    }

    private static string SanitizeBookText(string value)
        => new string(value.Select(static c => char.IsControl(c) ? ' ' : c).ToArray()).TrimEnd();
}
