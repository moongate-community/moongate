using System.Xml.Linq;

namespace Moongate.Tests.Server.Bootstrap;

public class GeneratorToolchainCompatibilityTests
{
    [Test]
    public void GeneratorsProject_ShouldUseRoslynPackagesCompatibleWithDockerSdkCompiler()
    {
        var repoRoot = ResolveRepositoryRoot();
        var projectPath = Path.Combine(repoRoot, "src", "Moongate.Generators", "Moongate.Generators.csproj");
        var document = XDocument.Load(projectPath);

        var packageVersions = document
                              .Descendants("PackageReference")
                              .Where(
                                  element =>
                                      element.Attribute("Include")?.Value is "Microsoft.CodeAnalysis.Analyzers" or
                                                                             "Microsoft.CodeAnalysis.CSharp"
                              )
                              .Select(element => element.Attribute("Version")?.Value)
                              .ToArray();

        Assert.That(packageVersions, Has.Length.EqualTo(2));
        Assert.That(packageVersions, Is.All.EqualTo("5.3.0"));
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
