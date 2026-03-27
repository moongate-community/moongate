using Moongate.Abstractions.Interfaces.Services.Base;

namespace Moongate.Server.Interfaces.Services.Files;

/// <summary>
/// Manages registration and execution of startup file loaders.
/// </summary>
public interface IFileLoaderService : IMoongateService
{
    /// <summary>
    /// Registers a file loader type if not already present in the execution pipeline.
    /// </summary>
    /// <typeparam name="T">The file loader type.</typeparam>
    void AddFileLoader<T>() where T : IFileLoader;

    /// <summary>
    /// Registers a file loader type if not already present in the execution pipeline.
    /// </summary>
    /// <param name="loaderType">The file loader type.</param>
    void AddFileLoader(Type loaderType);

    /// <summary>
    /// Executes all registered file loaders in their registration order.
    /// </summary>
    Task ExecuteLoadersAsync();

    /// <summary>
    /// Loads a single file through the appropriate registered loader.
    /// </summary>
    /// <param name="filePath">The file path to load.</param>
    Task LoadSingleAsync(string filePath);

    /// <summary>
    /// Loads a single file through the specified loader type.
    /// </summary>
    /// <typeparam name="T">The file loader type.</typeparam>
    /// <param name="filePath">The file path to load.</param>
    Task LoadSingleAsync<T>(string filePath) where T : IFileLoader;

    /// <summary>
    /// Loads a single file through the specified loader type.
    /// </summary>
    /// <param name="loaderType">The file loader type.</param>
    /// <param name="filePath">The file path to load.</param>
    Task LoadSingleAsync(Type loaderType, string filePath);
}
