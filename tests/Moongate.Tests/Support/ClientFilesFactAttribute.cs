namespace Moongate.Tests.Support;

/// <summary>
/// A fact that checks the shipped data against a real client, and skips itself where there is no
/// client to check against. CI has no UO files, so these report as skipped there — visibly, rather
/// than as a false pass — while anyone with a client directory gets the check on every run.
/// <para>
/// xUnit here is 2.9.3, which has no dynamic Assert.Skip; setting Skip at discovery time is the v2
/// idiom for a conditional test.
/// </para>
/// </summary>
public sealed class ClientFilesFactAttribute : FactAttribute
{
    public ClientFilesFactAttribute()
    {
        if (!Directory.Exists(ClientFiles.Directory))
        {
            Skip = $"No UO client files at {ClientFiles.Directory}";
        }
    }
}
