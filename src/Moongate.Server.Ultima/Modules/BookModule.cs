using Lua;
using Moongate.Core.Primitives;
using Moongate.Scripting.Attributes.Scripts;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Interfaces.Books;

namespace Moongate.Server.Ultima.Modules;

/// <summary>
///     Synchronous document APIs; text is resolved for the specified recipient at creation.
/// </summary>
[ScriptModule("book", "Creates and reads personalized scrolls from templates/books.")]
public sealed class BookModule
{
    private readonly IBookDocumentService _books;
    private readonly IItemService _items;
    private readonly IMobileService _mobiles;

    public BookModule(IBookDocumentService books, IItemService items, IMobileService mobiles)
    {
        _books = books;
        _items = items;
        _mobiles = mobiles;
    }

    [ScriptFunction(helpText: "Gives a document from templates/books/<template_id>.toml to the player's backpack. Resolves $name and ${name} at creation; values supplies declared custom strings, finite numbers or bools. Nil for missing data, invalid values, no backpack or no serial. Saved text stays unchanged when traded or read.")]
    public long? Give(long player, string templateId, LuaTable? values = null)
    {
        return IsSerial(player) && _mobiles.TryGet(new Serial((uint)player), out var recipient) &&
            TryValues(values, out var supplied) && _books.Give(recipient, templateId, supplied) is { } item ? item.Id.Value : null;
    }

    [ScriptFunction(helpText: "Inscribes an existing readable item for the specified player using a book template and optional custom values. False for unknown data, invalid values, held items, stacks or unsupported items; failure leaves all saved fields unchanged.")]
    public bool Write(long item, string templateId, long player, LuaTable? values = null)
    {
        return IsSerial(item) && IsSerial(player) && _items.TryGet(new Serial((uint)item), out var document) &&
            _mobiles.TryGet(new Serial((uint)player), out var recipient) && TryValues(values, out var supplied) &&
            _books.Write(document, recipient, templateId, supplied);
    }

    [ScriptFunction(helpText: "Displays the item's saved plain text without substituting reader variables. Native books submit cover and page packets during the call; true means both were accepted for sending. Scrolls opened from Lua are queued for the next game-loop turn; true means queued, with reader/item identity, the original session and item access checked again before delivery. False means refused, including invalid data, inaccessible items, size limits or unavailable queues.")]
    public bool Open(long item, long player)
    {
        return IsSerial(item) && IsSerial(player) && _items.TryGet(new Serial((uint)item), out var document) &&
            _mobiles.TryGet(new Serial((uint)player), out var reader) && _books.Open(document, reader);
    }

    private static bool IsSerial(long serial)
    {
        return serial is > 0 and <= uint.MaxValue;
    }

    private static bool TryValues(LuaTable? values, out IReadOnlyDictionary<string, object?> supplied)
    {
        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        supplied = result;
        if (values is null)
        {
            return true;
        }

        foreach (var (key, value) in values)
        {
            if (!key.TryRead<string>(out var name))
            {
                return false;
            }

            if (value.TryRead<string>(out var text))
            {
                result[name] = text;
            }
            else if (value.TryRead<double>(out var number) && double.IsFinite(number))
            {
                result[name] = number;
            }
            else if (value.TryRead<bool>(out var flag))
            {
                result[name] = flag;
            }
            else
            {
                return false;
            }
        }

        return true;
    }
}
