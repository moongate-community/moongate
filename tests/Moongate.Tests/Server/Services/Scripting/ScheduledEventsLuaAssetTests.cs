namespace Moongate.Tests.Server.Services.Scripting;

public sealed class ScheduledEventsLuaAssetTests
{
    [Test]
    public void CommonScheduledEventsScript_ShouldExist()
    {
        var repositoryRoot = GetRepositoryRoot();
        var scriptPath = Path.Combine(repositoryRoot, "moongate_data", "scripts", "common", "scheduled_events.lua");

        Assert.That(File.Exists(scriptPath), Is.True, $"Missing scheduled events helper script: {scriptPath}");
    }

    [Test]
    public void CommonScheduledEventsScript_ShouldRegisterScheduledEventDefinitions()
    {
        var repositoryRoot = GetRepositoryRoot();
        var scriptPath = Path.Combine(repositoryRoot, "moongate_data", "scripts", "common", "scheduled_events.lua");
        var script = File.ReadAllText(scriptPath);

        Assert.Multiple(
            () =>
            {
                Assert.That(script, Does.Contain("local runtime = scheduled_events"));
                Assert.That(script, Does.Contain("function M.event(id, definition)"));
                Assert.That(script, Does.Contain("runtime.register(id, definition)"));
            }
        );
    }

    private static string GetRepositoryRoot()
        => Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", ".."));
}
