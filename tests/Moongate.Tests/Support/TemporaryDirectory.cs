namespace Moongate.Tests.Support;

/// <summary>Temporary directories for tests that need one on disk.</summary>
public static class TemporaryDirectory
{
    /// <summary>Creates a uniquely named directory under the system temp root.</summary>
    public static string Create(string prefix)
    {
        var path = Path.Combine(Path.GetTempPath(), prefix + Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(path);

        return path;
    }

    /// <summary>
    /// Removes a temporary directory, tolerating a failure to do so.
    /// <para>
    /// Deleting in a <c>finally</c> makes the removal part of the test's verdict, and it should not
    /// be: a test that bootstraps a Lua engine over a temp directory can reach its cleanup while the
    /// engine still holds a file handle, and <see cref="Directory.Delete(string, bool)" /> throws.
    /// The assertions have already passed at that point — failing because the operating system has
    /// not caught up is testing the wrong thing, and it is the shape both Lua-bootstrap tests were
    /// intermittently failing in.
    /// </para>
    /// </summary>
    public static void Remove(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }
        catch (IOException)
        {
            // A leftover temp directory is untidy, not a failed test.
        }
        catch (UnauthorizedAccessException)
        {
            // Same: a handle still open somewhere is not the behaviour under test.
        }
    }
}
