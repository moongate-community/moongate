namespace Moongate.Server.Ultima.Types.Books;

internal enum BookAttachmentCommitState
{
    Committed,
    RolledBack,
    AlreadyClaimed,
    Uncertain
}
