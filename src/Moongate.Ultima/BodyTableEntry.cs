namespace Moongate.Ultima;

public sealed class BodyTableEntry
{
    public int OldId { get; set; }
    public int NewId { get; set; }
    public int NewHue { get; set; }

    public BodyTableEntry(int oldId, int newId, int newHue)
    {
        OldId = oldId;
        NewId = newId;
        NewHue = newHue;
    }
}
