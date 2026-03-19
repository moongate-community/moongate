namespace Moongate.Server.Interfaces.Services.Scripting;

/// <summary>
/// Loads and renders text templates from the scripts/texts directory.
/// </summary>
public interface ITextTemplateService
{
    /// <summary>
    /// Attempts to render the requested text template.
    /// </summary>
    /// <param name="relativePath">Path relative to scripts/texts.</param>
    /// <param name="model">Optional template model.</param>
    /// <param name="rendered">Rendered output when successful; otherwise empty.</param>
    /// <returns><see langword="true" /> when rendering succeeds; otherwise <see langword="false" />.</returns>
    bool TryRender(string relativePath, IReadOnlyDictionary<string, object?>? model, out string rendered);
}
