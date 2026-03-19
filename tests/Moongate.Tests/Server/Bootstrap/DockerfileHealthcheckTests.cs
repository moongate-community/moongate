namespace Moongate.Tests.Server.Bootstrap;

public class DockerfileHealthcheckTests
{
    [Test]
    public void Dockerfile_ShouldDeclareHealthcheckAgainstHttpHealthEndpoint()
    {
        var repoRoot = ResolveRepositoryRoot();
        var dockerfilePath = Path.Combine(repoRoot, "Dockerfile");
        var dockerfileContents = File.ReadAllText(dockerfilePath);

        Assert.That(dockerfileContents, Does.Contain("HEALTHCHECK"));
        Assert.That(dockerfileContents, Does.Contain("http://127.0.0.1:8088/health"));
        Assert.That(dockerfileContents, Does.Contain("grep -q '^ok$'"));
    }

    private static string ResolveRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "global.json")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Could not resolve repository root from test base directory.");
    }
}
