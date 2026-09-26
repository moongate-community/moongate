using Moongate.Server.Ultima.Data.Templates.Mobiles;

namespace Moongate.UoxItemConverter.Internal;

/// <summary>
///     Reads the sounds of each body from UOX3 <c>creatures.dfn</c> (<c>[CREATURE 0x11]</c> blocks).
/// </summary>
internal static class UoxCreatureSounds
{
    private const string HeaderPrefix = "CREATURE ";

    public static IReadOnlyDictionary<int, MobileSounds> Load(string creaturesPath)
    {
        var sounds = new Dictionary<int, MobileSounds>();

        if (!File.Exists(creaturesPath))
        {
            return sounds;
        }

        foreach (var block in DfnParser.Parse(File.ReadAllLines(creaturesPath)))
        {
            if (!block.Header.StartsWith(HeaderPrefix, StringComparison.OrdinalIgnoreCase) ||
                !MobileTemplateBuilder.TryParseNumber(block.Header[HeaderPrefix.Length..], out var body))
            {
                continue;
            }

            var creature = new MobileSounds
            {
                StartAttack = Sound(block, "SOUND_STARTATTACK"),
                Idle = Sound(block, "SOUND_IDLE"),
                Attack = Sound(block, "SOUND_ATTACK"),
                Hurt = Sound(block, "SOUND_DEFEND"),
                Death = Sound(block, "SOUND_DIE")
            };

            if (creature.StartAttack is not null || creature.Idle is not null || creature.Attack is not null ||
                creature.Hurt is not null || creature.Death is not null)
            {
                sounds[body] = creature;
            }
        }

        return sounds;
    }

    private static int? Sound(DfnBlock block, string key)
    {
        return block.Fields.TryGetValue(key, out var text) && MobileTemplateBuilder.TryParseNumber(text, out var sound)
            ? sound
            : null;
    }
}
