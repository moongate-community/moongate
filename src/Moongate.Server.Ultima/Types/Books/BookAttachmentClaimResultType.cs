namespace Moongate.Server.Ultima.Types.Books;
/// <summary>The safely settled outcome of withdrawing a letter's attachments.</summary>
public enum BookAttachmentClaimResultType
{
    Claimed,
    Unavailable,
    NoCapacity,
    Busy,
    Failed
}
