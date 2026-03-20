using Moongate.Server.PacketHandlers.Generators;
using Moongate.Tests.Generators.TestSupport;

namespace Moongate.Tests.Generators.Persistence;

public sealed class PersistenceSnapshotContractGeneratorTests
{
    [Test]
    public void Execute_ShouldGenerateSnapshotMapperAndDescriptor()
    {
        const string source =
            """
            using Moongate.Generators.Annotations.Persistence;

            namespace Sample;

            [MoongatePersistedEntity(1, "sample", 1, typeof(uint))]
            public sealed partial class SampleEntity
            {
                [MoongatePersistedMember(0)]
                public uint Id { get; set; }

                [MoongatePersistedMember(1)]
                public string Name { get; set; } = string.Empty;
            }
            """;

        var result = GeneratorTestHarness.Run(new PersistenceSnapshotContractGenerator(), source);
        var generatedFiles = result.Results
                                   .SelectMany(static generatorResult => generatorResult.GeneratedSources)
                                   .Select(static generatedSource => generatedSource.HintName)
                                   .ToArray();

        Assert.Multiple(
            () =>
            {
                Assert.That(generatedFiles, Contains.Item("SampleEntitySnapshot.g.cs"));
                Assert.That(generatedFiles, Contains.Item("SampleEntitySnapshotMapper.g.cs"));
                Assert.That(generatedFiles, Contains.Item("SampleEntityPersistence.g.cs"));
            }
        );
    }

    [Test]
    public void Execute_ShouldGenerateNestedSnapshotContractsForLists()
    {
        const string source =
            """
            using System.Collections.Generic;
            using Moongate.Generators.Annotations.Persistence;

            namespace Sample;

            [MoongatePersistedEntity]
            public sealed partial class SamplePoint
            {
                [MoongatePersistedMember(0)]
                public int X { get; set; }

                [MoongatePersistedMember(1)]
                public int Y { get; set; }
            }

            [MoongatePersistedEntity(1, "sample", 1, typeof(uint))]
            public sealed partial class SampleEntity
            {
                [MoongatePersistedMember(0)]
                public uint Id { get; set; }

                [MoongatePersistedMember(1)]
                public SamplePoint Position { get; set; } = new();

                [MoongatePersistedMember(2)]
                public List<SamplePoint> History { get; set; } = [];
            }
            """;

        var result = GeneratorTestHarness.Run(new PersistenceSnapshotContractGenerator(), source);
        var generatedSources = result.Results
                                     .SelectMany(static generatorResult => generatorResult.GeneratedSources)
                                     .ToDictionary(static generated => generated.HintName, static generated => generated.SourceText.ToString());

        Assert.Multiple(
            () =>
            {
                Assert.That(generatedSources.Keys, Contains.Item("SamplePointSnapshot.g.cs"));
                Assert.That(generatedSources.Keys, Contains.Item("SampleEntitySnapshot.g.cs"));
                Assert.That(
                    generatedSources["SampleEntitySnapshot.g.cs"],
                    Does.Contain("public Sample.SamplePointSnapshot Position")
                );
                Assert.That(
                    generatedSources["SampleEntitySnapshot.g.cs"],
                    Does.Contain("public Sample.SamplePointSnapshot[] History")
                );
            }
        );
    }
}
