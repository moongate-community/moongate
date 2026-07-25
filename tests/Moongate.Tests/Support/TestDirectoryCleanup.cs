namespace Moongate.Tests.Support;

internal static class TestDirectoryCleanup
{
    public static void TryDelete(string path)
    {
        try
        {
            Directory.Delete(path, true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
