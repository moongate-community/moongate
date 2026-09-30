using Moongate.Server.Ultima.Types.Mobiles;

namespace Moongate.UoxItemConverter.Internal;

/// <summary>
///     Reads where each body moves from UOX3 <c>creatures.dfn</c>: <c>MOVEMENT=WATER</c> or <c>MOVEMENT=BOTH</c>;
///     a body on land only, the default, is left out.
/// </summary>
internal static class UoxCreatureMovements
{
    private const string HeaderPrefix = "CREATURE ";

    public static IReadOnlyDictionary<int, MobileMovementType> Load(string creaturesPath)
    {
        var movements = new Dictionary<int, MobileMovementType>();

        if (!File.Exists(creaturesPath))
        {
            return movements;
        }

        foreach (var block in DfnParser.Parse(File.ReadAllLines(creaturesPath)))
        {
            if (!block.Header.StartsWith(HeaderPrefix, StringComparison.OrdinalIgnoreCase) ||
                !UoxNumber.TryParse(block.Header[HeaderPrefix.Length..], out var body) ||
                !block.Fields.TryGetValue("MOVEMENT", out var movement))
            {
                continue;
            }

            switch (movement.Trim().ToUpperInvariant())
            {
                case "WATER":
                    movements[body] = MobileMovementType.Water;

                    break;
                case "BOTH":
                    movements[body] = MobileMovementType.Both;

                    break;
            }
        }

        return movements;
    }
}
