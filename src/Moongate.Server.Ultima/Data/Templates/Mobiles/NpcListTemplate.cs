namespace Moongate.Server.Ultima.Data.Templates.Mobiles;

/// <summary>
///     A list of mobile templates a spawn picks from at random, such as the animals of a forest; converted from UOX3's
///     <c>[NPCLIST name]</c> blocks.
/// </summary>
public class NpcListTemplate
{
    public string Id { get; set; } = string.Empty;

    public List<NpcListEntry> Entries { get; set; } = [];
}
