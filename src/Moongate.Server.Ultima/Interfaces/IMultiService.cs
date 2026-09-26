using System.Diagnostics.CodeAnalysis;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Data.Multis;

namespace Moongate.Server.Ultima.Interfaces;

/// <summary>
///     Gives the layouts of the multis, such as houses and boats, from the client files.
/// </summary>
/// <remarks>
///     Starting reads every multi from <c>MultiCollection.uop</c>, or from <c>multi.idx</c> and <c>multi.mul</c> when
///     the client has no UOP file, and keeps them in memory; it fails with <see cref="FileNotFoundException" /> when
///     neither is there and with <see cref="InvalidDataException" /> when no multi can be read. After that the
///     service only reads memory and can be called from any thread.
/// </remarks>
public interface IMultiService : IMoongateStartupService
{
    /// <summary>
    ///     Gets the number of multis loaded.
    /// </summary>
    int Count { get; }

    /// <summary>
    ///     Gets multi <paramref name="id" />.
    /// </summary>
    /// <exception cref="KeyNotFoundException">
    ///     The client files have no multi with this id.
    /// </exception>
    MultiDefinition GetMulti(int id);

    /// <summary>
    ///     Gets multi <paramref name="id" />, or false when the client files have none with this id.
    /// </summary>
    bool TryGetMulti(int id, [NotNullWhen(true)] out MultiDefinition? multi);
}
