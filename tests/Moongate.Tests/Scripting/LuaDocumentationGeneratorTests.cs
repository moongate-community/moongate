using Moongate.Core.Data.Directories;
using Moongate.Scripting.Data.Internal;
using Moongate.Scripting.Modules;
using Moongate.Server.Modules;
using Moongate.Server.Modules.Builders;
using Moongate.Scripting.Utils;

namespace Moongate.Tests.Scripting;

public class LuaDocumentationGeneratorTests
{
    [Test]
    public void GenerateDocumentation_WhenClassMethodsAreGenerated_ShouldIncludeNamedMethodStubs()
    {
        LuaDocumentationGenerator.ClearCaches();
        LuaDocumentationGenerator.AddClassToGenerate(typeof(LuaGumpBuilder));

        var docs = LuaDocumentationGenerator.GenerateDocumentation(
            "Moongate",
            "0.0.0",
            [],
            [],
            []
        );

        Assert.Multiple(
            () =>
            {
                Assert.That(docs, Does.Contain("---@class LuaGumpBuilder"));
                Assert.That(docs, Does.Contain("function LuaGumpBuilder:resize_pic(...) end"));
                Assert.That(docs, Does.Contain("function LuaGumpBuilder:text(...) end"));
                Assert.That(docs, Does.Contain("function LuaGumpBuilder:build_layout(...) end"));
            }
        );
    }

    [Test]
    public void GenerateDocumentation_WhenModuleIsGenerated_ShouldInitializeTableBeforeFunctions()
    {
        LuaDocumentationGenerator.ClearCaches();

        var docs = LuaDocumentationGenerator.GenerateDocumentation(
            "Moongate",
            "0.0.0",
            [new(typeof(LogModule))],
            [],
            []
        );

        var tableIndex = docs.IndexOf("log = {}", StringComparison.Ordinal);
        var functionIndex = docs.IndexOf("function log.info(", StringComparison.Ordinal);

        Assert.Multiple(
            () =>
            {
                Assert.That(tableIndex, Is.GreaterThanOrEqualTo(0));
                Assert.That(functionIndex, Is.GreaterThanOrEqualTo(0));
                Assert.That(tableIndex, Is.LessThan(functionIndex));
                Assert.That(docs, Does.Not.Contain("log.info = function() end"));
            }
        );
    }

    [Test]
    public void GenerateDocumentation_WhenCommandModuleIsGenerated_ShouldContainRegisterFunction()
    {
        LuaDocumentationGenerator.ClearCaches();

        var docs = LuaDocumentationGenerator.GenerateDocumentation(
            "Moongate",
            "0.0.0",
            [new(typeof(CommandModule))],
            [],
            []
        );

        Assert.Multiple(
            () =>
            {
                Assert.That(docs, Does.Contain("command = {}"));
                Assert.That(docs, Does.Contain("function command.register("));
            }
        );
    }

    [Test]
    public void GenerateDocumentation_WhenConstructorsAreGenerated_ShouldUseClassReturnType()
    {
        LuaDocumentationGenerator.ClearCaches();
        LuaDocumentationGenerator.AddClassToGenerate(typeof(DirectoriesConfig));

        var docs = LuaDocumentationGenerator.GenerateDocumentation(
            "Moongate",
            "0.0.0",
            [],
            [],
            []
        );

        Assert.Multiple(
            () =>
            {
                Assert.That(docs, Does.Contain("--- Constructors:"));
                Assert.That(docs, Does.Contain("):DirectoriesConfig"));
                Assert.That(docs, Does.Not.Contain("):void"));
            }
        );
    }
}
