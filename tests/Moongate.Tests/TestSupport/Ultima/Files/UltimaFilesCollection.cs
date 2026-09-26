namespace Moongate.Tests.TestSupport.Ultima.Files;

/// <summary>
///     Names the test collection for tests that point the process-wide <c>Moongate.Ultima.Io.Files</c> at a client
///     directory, so they never run at the same time.
/// </summary>
public static class UltimaFilesCollection
{
    public const string Name = "Ultima client files";
}
