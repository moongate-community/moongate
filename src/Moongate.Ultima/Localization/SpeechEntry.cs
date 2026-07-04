using System.ComponentModel;

namespace Moongate.Ultima.Localization;

public sealed class SpeechEntry
{
    public short Id { get; }
    public string KeyWord { get; set; }

    [Browsable(false)]
    public int Order { get; }

    public SpeechEntry(short id, string keyword, int order)
    {
        Id = id;
        KeyWord = keyword;
        Order = order;
    }
}
