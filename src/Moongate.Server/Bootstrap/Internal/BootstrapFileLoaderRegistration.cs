using Moongate.Server.Interfaces.Services.Files;

namespace Moongate.Server.Bootstrap.Internal;

/// <summary>
/// Registers built-in file loaders in the configured file loader service.
/// Loader registrations are source-generated from <c>[RegisterFileLoader]</c> attributes.
/// </summary>
internal static partial class BootstrapFileLoaderRegistration
{
    public static void Register(IFileLoaderService fileLoaderService)
        => RegisterGenerated(fileLoaderService);

    public static void Register(IFileLoaderService fileLoaderService, IEnumerable<Type> pluginLoaderTypes)
    {
        ArgumentNullException.ThrowIfNull(fileLoaderService);
        ArgumentNullException.ThrowIfNull(pluginLoaderTypes);

        foreach (var loaderType in pluginLoaderTypes)
        {
            fileLoaderService.AddFileLoader(loaderType);
        }
    }

    static partial void RegisterGenerated(IFileLoaderService fileLoaderService);
}
