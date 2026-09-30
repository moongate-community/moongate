namespace Moongate.Server.Ultima.Data.Templates.Spawns;

/// <summary>
///     One file under <c>templates/spawns/&lt;map&gt;/</c>: a <c>[[spawn]]</c> array of <see cref="SpawnTemplate" />.
/// </summary>
public class SpawnTemplateFile
{
    public List<SpawnTemplate> Spawn { get; set; } = [];
}
