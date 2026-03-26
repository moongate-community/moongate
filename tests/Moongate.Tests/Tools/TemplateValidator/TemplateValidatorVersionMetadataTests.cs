using System.Reflection;
using System.Xml.Linq;
using Moongate.TemplateValidator.Commands;

namespace Moongate.Tests.Tools.TemplateValidator;

public sealed class TemplateValidatorVersionMetadataTests
{
    [Test]
    public void TemplateValidatorAssemblyInformationalVersion_ShouldMatchRepositoryVersion()
    {
        var repositoryRoot = GetRepositoryRoot();
        var directoryBuildPropsPath = Path.Combine(repositoryRoot, "Directory.Build.props");
        var document = XDocument.Load(directoryBuildPropsPath);
        var expectedVersion = document.Root?
            .Elements("PropertyGroup")
            .Elements("Version")
            .Select(static element => element.Value.Trim())
            .FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value));

        var assembly = typeof(TemplateValidateCommand).Assembly;
        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        Assert.Multiple(
            () =>
            {
                Assert.That(expectedVersion, Is.Not.Null.And.Not.Empty);
                Assert.That(informationalVersion, Is.EqualTo(expectedVersion));
            }
        );
    }

    private static string GetRepositoryRoot()
        => Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", ".."));
}
