namespace Moongate.Server.Interfaces.Services.Files;

/// <summary>
/// Defines an asynchronous loader for startup data files.
/// </summary>
public interface IFileLoader
{
    /// <summary>
    /// Loads the underlying data source.
    /// </summary>
    Task LoadAsync();

    /// <summary>
    /// Loads a single data file.
    /// </summary>
    /// <param name="filePath">The file path to load.</param>
    Task LoadSingleAsync(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        
        return LoadAsync();
    }
}
