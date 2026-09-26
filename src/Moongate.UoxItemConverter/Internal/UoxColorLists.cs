using Moongate.Core.Primitives;

namespace Moongate.UoxItemConverter.Internal;

/// <summary>
///     Reads UOX3 <c>colors.dfn</c>: each <c>[RANDOMCOLOR n]</c> list becomes a hue range when its hues are one
///     unbroken run, which is all a <see cref="HueSpec" /> can hold; any other list is null.
/// </summary>
internal static class UoxColorLists
{
    private const string HeaderPrefix = "RANDOMCOLOR ";

    public static IReadOnlyDictionary<int, HueSpec?> Load(string colorsPath)
    {
        var lists = new Dictionary<int, HueSpec?>();

        if (!File.Exists(colorsPath))
        {
            return lists;
        }

        foreach (var block in DfnParser.Parse(File.ReadAllLines(colorsPath)))
        {
            if (!block.Header.StartsWith(HeaderPrefix, StringComparison.OrdinalIgnoreCase) ||
                !int.TryParse(block.Header[HeaderPrefix.Length..].Trim(), out var number))
            {
                continue;
            }

            var hues = new List<int>();

            foreach (var entry in block.Entries)
            {
                if (!MobileTemplateBuilder.TryParseNumber(entry, out var hue))
                {
                    hues.Clear();

                    break;
                }

                hues.Add(hue);
            }

            lists[number] = ToRange(hues);
        }

        return lists;
    }

    private static HueSpec? ToRange(List<int> hues)
    {
        if (hues.Count == 0)
        {
            return null;
        }

        var (min, max) = (hues.Min(), hues.Max());

        if (hues.Distinct().Count() != max - min + 1)
        {
            return null;
        }

        return min == max ? HueSpec.FromValue(min) : HueSpec.Parse($"0x{min:X4}-0x{max:X4}");
    }
}
