using Moongate.Scripting.Interfaces;
using Moongate.Server.Core.Interfaces.Services;
using Serilog;

namespace Moongate.Server.Ultima.Services.Internal;

/// <summary>
///     Loads the <c>*.lua</c> files directly in a subdirectory of the scripts directory, in name order, on the game
///     loop.
///     A file that fails to compile or run is reported by the engine and skipped: the server starts with the others.
/// </summary>
public static class ScriptDirectoryLoader
{
    /// <summary>
    ///     Loads <c>&lt;scripts&gt;/&lt;subdirectory&gt;/*.lua</c>; a missing subdirectory loads nothing.
    /// </summary>
    /// <returns>
    ///     How many files were found.
    /// </returns>
    public static async Task<int> LoadAsync(
        IScriptEngine engine,
        IGameLoopService loop,
        string scriptsDirectory,
        string subdirectory,
        ILogger logger
    )
    {
        var directory = Path.Combine(scriptsDirectory, subdirectory);

        if (!Directory.Exists(directory))
        {
            logger.Debug("No {Subdirectory} scripts: {Directory} does not exist", subdirectory, directory);

            return 0;
        }

        var files = Directory.GetFiles(directory, "*.lua", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .Order(StringComparer.Ordinal)
            .ToList();
        var work = new LoopActionWorkItem(() =>
            {
                foreach (var file in files)
                {
                    try
                    {
                        engine.LoadFile($"{subdirectory}/{file}");
                    }
                    catch (FileNotFoundException exception)
                    {
                        logger.Warning(
                            exception,
                            "Script {File} disappeared before it was loaded",
                            $"{subdirectory}/{file}"
                        );
                    }
                    catch (InvalidOperationException)
                    {
                        // The engine has reported the broken script; the server starts with the others.
                    }
                }
            }
        );

        await loop.PostAsync(work);
        await work.Completion;
        logger.Information("Loaded {Count} scripts from {Subdirectory}", files.Count, subdirectory);

        return files.Count;
    }
}
