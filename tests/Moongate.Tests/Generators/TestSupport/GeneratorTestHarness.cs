using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Moongate.Generators.Annotations.Persistence;

namespace Moongate.Tests.Generators.TestSupport;

internal static class GeneratorTestHarness
{
    public static GeneratorDriverRunResult Run(IIncrementalGenerator generator, string source)
    {
        ArgumentNullException.ThrowIfNull(generator);
        ArgumentException.ThrowIfNullOrWhiteSpace(source);

        _ = typeof(MoongatePersistedEntityAttribute);

        var syntaxTree = CSharpSyntaxTree.ParseText(
            source,
            new CSharpParseOptions(LanguageVersion.Latest)
        );
        var references = AppDomain.CurrentDomain
                                  .GetAssemblies()
                                  .Where(static assembly => !assembly.IsDynamic && !string.IsNullOrWhiteSpace(assembly.Location))
                                  .Select(static assembly => assembly.Location)
                                  .Append(typeof(MoongatePersistedEntityAttribute).Assembly.Location)
                                  .Distinct(StringComparer.OrdinalIgnoreCase)
                                  .Select(static location => MetadataReference.CreateFromFile(location))
                                  .Cast<MetadataReference>()
                                  .ToArray();
        var compilation = CSharpCompilation.Create(
            assemblyName: "GeneratorTests",
            syntaxTrees: [syntaxTree],
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );
        var compilationErrors = compilation.GetDiagnostics()
                                           .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
                                           .ToArray();

        if (compilationErrors.Length > 0)
        {
            throw new InvalidOperationException(
                $"Generator test compilation failed:{Environment.NewLine}{string.Join(Environment.NewLine, compilationErrors.Select(static diagnostic => diagnostic.ToString()))}"
            );
        }

        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator.AsSourceGenerator());
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _);
        var result = driver.GetRunResult();
        var generatorResult = result.Results.SingleOrDefault();

        if (generatorResult.Exception is not null)
        {
            throw new InvalidOperationException(
                $"Generator threw an exception: {generatorResult.Exception}",
                generatorResult.Exception
            );
        }

        return result;
    }
}
