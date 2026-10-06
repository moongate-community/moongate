using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Data.Internal.Books;
using Moongate.Server.Ultima.Entities.World;

namespace Moongate.Server.Ultima.Services.Internal.Books;

internal static class BookAttachmentCodec
{
    public const string PropKey = "book.attachments";
    public const int MaximumBytes = 65536;
    private const int MaximumDepth = 8;
    private const int MaximumProps = 64;

    private static readonly JsonSerializerOptions Options = new()
    {
        MaxDepth = MaximumDepth, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public static bool TryDecode(string? payload, out BookAttachmentPayload? batch)
    {
        batch = null;
        if (string.IsNullOrEmpty(payload) || payload.Length > MaximumBytes ||
            Encoding.UTF8.GetByteCount(payload) > MaximumBytes)
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(payload, new() { MaxDepth = MaximumDepth });
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !UniqueKeys(root) ||
                !root.TryGetProperty("Version", out var version) || !version.TryGetInt32(out var number) || number != 1)
            {
                return false;
            }

            var decoded = JsonSerializer.Deserialize<BookAttachmentPayload>(payload, Options);
            if (decoded is null || decoded.Version != 1 || decoded.Items.IsDefaultOrEmpty ||
                decoded.Items.Length > BookAttachmentValidation.MaximumItems || decoded.Items.Any(item => !Valid(item)))
            {
                return false;
            }

            batch = decoded;
            return true;
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException or InvalidOperationException
                                              or NotSupportedException)
        {
            return false;
        }
    }

    public static string Encode(BookAttachmentPayload batch)
    {
        var payload = JsonSerializer.Serialize(batch, Options);
        if (!TryDecode(payload, out _))
        {
            throw new InvalidDataException("Attachment snapshot is invalid or exceeds its bounded payload limits.");
        }

        return payload;
    }

    public static FrozenBookAttachment Freeze(ItemEntity item)
    {
        return new()
        {
            TemplateId = item.TemplateId, ItemId = item.ItemId, Hue = item.Hue.Value, Amount = item.Amount,
            Rarity = item.Rarity, Name = item.Name, Movable = item.Movable, Visibility = item.Visibility,
            DecayAt = item.DecayAt,
            Props = (item.Props ?? []).ToImmutableDictionary(
                pair => pair.Key,
                pair => JsonSerializer.SerializeToElement(pair.Value)
            )
        };
    }

    public static ItemEntity Materialize(FrozenBookAttachment item)
    {
        var result = new ItemEntity
        {
            TemplateId = item.TemplateId, ItemId = item.ItemId, Hue = new Hue(item.Hue), Amount = item.Amount,
            Rarity = item.Rarity, Name = item.Name, Movable = item.Movable, Visibility = item.Visibility,
            DecayAt = item.DecayAt
        };
        foreach (var (key, value) in item.Props)
        {
            object scalar = value.ValueKind switch
            {
                JsonValueKind.String => value.GetString()!,
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Number when value.TryGetInt64(out var integer) => integer,
                JsonValueKind.Number when value.TryGetDecimal(out var real) => real,
                JsonValueKind.Number => value.GetDouble(),
                _ => throw new InvalidDataException("Attachment props must be scalars.")
            };
            result.SetProp(key, scalar);
        }

        return result;
    }

    private static bool Valid(FrozenBookAttachment? item)
    {
        return item is not null && !string.IsNullOrWhiteSpace(item.TemplateId) && item.TemplateId.Length <= 256 &&
               !item.TemplateId.Any(char.IsControl) && item.ItemId is > 0 and <= ushort.MaxValue &&
               item.Amount is >= 1 and <= BookAttachmentValidation.MaximumAmount && Enum.IsDefined(item.Rarity) &&
               (item.Visibility is null || Enum.IsDefined(item.Visibility.Value)) && item.Props is not null &&
               item.Props.Count <= MaximumProps && item.Props.All(pair => !string.IsNullOrWhiteSpace(pair.Key) &&
                                                                          pair.Key.Length <= 128 && Scalar(pair.Value)
               );
    }

    private static bool Scalar(JsonElement value)
    {
        return value.ValueKind is JsonValueKind.String or JsonValueKind.True or JsonValueKind.False ||
               (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number) && double.IsFinite(number));
    }

    private static bool UniqueKeys(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject())
            {
                if (!names.Add(property.Name) || !UniqueKeys(property.Value))
                {
                    return false;
                }
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (var entry in value.EnumerateArray())
            {
                if (!UniqueKeys(entry))
                {
                    return false;
                }
            }
        }

        return true;
    }
}
