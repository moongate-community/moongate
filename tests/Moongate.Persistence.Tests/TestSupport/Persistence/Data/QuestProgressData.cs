namespace Moongate.Persistence.Tests.TestSupport.Persistence.Data;

public sealed class QuestProgressData
{
    public int QuestId { get; set; }
    public string Description { get; set; } = "";
    public bool Completed { get; set; }
    public List<int> Milestones { get; set; } = [];
}
