using Moongate.Server.Ultima.Data.Books;
using Moongate.Server.Ultima.Data.Templates.Books;

namespace Moongate.Server.Ultima.Interfaces.Books;

/// <summary>
///     Finds loaded document sources and renders their creation-time snapshots.
/// </summary>
public interface IBookTemplateService
{
    /// <summary>
    ///     Finds a template by its case-sensitive filename stem.
    /// </summary>
    bool TryGet(string id, out BookTemplate? template);

    /// <summary>
    ///     Renders all fields atomically; invalid or missing supplied values return false.
    /// </summary>
    bool TryRender(string id, TextTemplateContext context, string language,
        IReadOnlyDictionary<string, object?>? values, out RenderedBook? rendered);
}
