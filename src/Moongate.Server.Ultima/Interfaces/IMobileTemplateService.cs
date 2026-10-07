using System.Diagnostics.CodeAnalysis;
using Moongate.Server.Ultima.Data.Templates.Mobiles;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     Serves the mobile templates of <c>templates/mobiles/</c>, with <c>base_id</c> already resolved.
/// </summary>
public interface IMobileTemplateService
{
    /// <summary>
    ///     Gets how many templates were loaded.
    /// </summary>
    int Count { get; }

    /// <summary>
    ///     Gets the template with id <paramref name="id" />, matching case.
    /// </summary>
    bool TryGet(string id, [NotNullWhen(true)] out MobileTemplate? template);

    /// <summary>
    ///     Gets the template with id <paramref name="id" />, matching case.
    /// </summary>
    /// <exception cref="KeyNotFoundException">
    ///     No template has that id.
    /// </exception>
    MobileTemplate Get(string id);
}
