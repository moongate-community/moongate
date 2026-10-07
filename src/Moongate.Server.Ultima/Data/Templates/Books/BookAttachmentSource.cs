using Moongate.Core.Primitives;

namespace Moongate.Server.Ultima.Data.Templates.Books;

public sealed class BookAttachmentSource
{
    public string ItemTemplate { get; set; } = "";
    public DiceSpec? Amount { get; set; }
    public HueSpec? Hue { get; set; }
    public bool Newbie { get; set; }
}
