namespace Moongate.Server.Ultima.Data.Templates.Mobiles;

/// <summary>
///     One file under <c>templates/npc_lists/</c>: a <c>[[npc_list]]</c> array of <see cref="NpcListTemplate" />.
/// </summary>
public class NpcListTemplateFile
{
    public List<NpcListTemplate> NpcList { get; set; } = [];
}
