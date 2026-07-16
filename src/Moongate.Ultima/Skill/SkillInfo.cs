namespace Moongate.Ultima.Skill;

public sealed class SkillInfo
{
    private string _name;

    public int Index { get; set; }

    public bool IsAction { get; set; }

    public string Name
    {
        get => _name;
        set => _name = value ?? string.Empty;
    }

    public int Extra { get; }

    public SkillInfo(int nr, string name, bool action, int extra)
    {
        Index = nr;
        _name = name;
        IsAction = action;
        Extra = extra;
    }
}
