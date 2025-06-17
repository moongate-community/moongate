namespace Moongate.UO.Data.Localization;

public class CliLocEntry
{
    public int Id { get; set; }
    public bool HasArguments { get; set; }
    public string Text { get; set; }

    public override string ToString()
    {
        return $"CliLocEntry(Id: {Id}, HasArguments: {HasArguments}, Text: \"{Text}\")";
    }
}
