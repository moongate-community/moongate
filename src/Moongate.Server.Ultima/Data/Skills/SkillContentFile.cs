namespace Moongate.Server.Ultima.Data.Skills;

/// <summary>
///     The root of <c>skills.toml</c>: an array of tables under <c>skill</c>. The property name must match the table
///     name, otherwise the file reads as an empty list without any error.
/// </summary>
internal sealed class SkillContentFile
{
    public List<SkillContent> Skill { get; set; } = [];
}
