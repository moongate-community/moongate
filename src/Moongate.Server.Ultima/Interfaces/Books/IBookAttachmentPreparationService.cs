using Moongate.Server.Ultima.Data.Templates.Books;

namespace Moongate.Server.Ultima.Interfaces.Books;

/// <summary>
///     Prepares immutable rewards without allocating serials or changing the live world.
/// </summary>
public interface IBookAttachmentPreparationService
{
    /// <summary>
    ///     Resolves a batch once for a new physical document; null means no attachments.
    /// </summary>
    string? Prepare(BookTemplateSource source);
}
