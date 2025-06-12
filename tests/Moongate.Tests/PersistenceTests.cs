using System.Text.Json;
using Moongate.Core.Directories;
using Moongate.Core.Persistence.Interfaces.Entities;
using Moongate.Core.Persistence.Services;
using Moongate.Core.Server.Types;

namespace Moongate.Tests;

[TestFixture]
public class PersistenceTests
{
    private DirectoriesConfig _directoriesConfig;

    private MoongateEntityFileService _entityFileService;
    private string _tmpDirectory;

    [OneTimeSetUp]
    public void Setup()
    {
        _tmpDirectory = Path.Combine(Path.GetTempPath(), "MoongateTests");
        if (!Directory.Exists(_tmpDirectory))
        {
            Directory.CreateDirectory(_tmpDirectory);
        }

        _directoriesConfig = new DirectoriesConfig(_tmpDirectory, Enum.GetNames<DirectoryType>());
        _entityFileService = new MoongateEntityFileService(
            _directoriesConfig,
            new TestReaderWriter(),
            new TestReaderWriter()
        );
    }

    [OneTimeTearDown]
    public void Cleanup()
    {
        if (Directory.Exists(_tmpDirectory))
        {
            Directory.Delete(_tmpDirectory, true);
        }
    }

    [Test]
    public Task TestSaveFilesAsync()
    {
        var randomEntities = Enumerable.Range(1, 100)
            .Select(i => new TestEntity(Guid.NewGuid().ToString(), $"Entity {i}", i * 10))
            .ToList();

        return _entityFileService.SaveEntitiesAsync("test_entities.mga", randomEntities);
    }

    [Test]
    public async Task TestLoadFilesAsync()
    {
        var randomEntities = Enumerable.Range(1, 10_000)
            .Select(i => new TestEntity(Guid.NewGuid().ToString(), $"Entity {i}", i * 10))
            .ToList();

        await _entityFileService.SaveEntitiesAsync("test_entities_2.mga", randomEntities);

        var loadedEntities = await _entityFileService.LoadEntitiesAsync<TestEntity>("test_entities_2.mga");

        Assert.That(loadedEntities, Is.Not.Null);
        Assert.That(loadedEntities.Count, Is.EqualTo(randomEntities.Count));
        Assert.That(loadedEntities.Select(e => e.Id).Distinct().Count(), Is.EqualTo(randomEntities.Count));
    }
}

public class TestEntity
{
    public string Id { get; set; }
    public string Name { get; set; }
    public int Value { get; set; }

    public TestEntity(string id, string name, int value)
    {
        Id = id;
        Name = name;
        Value = value;
    }

    public TestEntity()
    {
    } // Parameterless constructor for deserialization
}

internal class TestReaderWriter : IEntityWriter, IEntityReader
{
    public byte[] SerializeEntity<T>(T entity) where T : class
    {
        return JsonSerializer.SerializeToUtf8Bytes(entity);
    }


    public TEntity DeserializeEntity<TEntity>(byte[] data, Type entityType) where TEntity : class
    {
        return (TEntity)JsonSerializer.Deserialize(data, entityType)
               ?? throw new InvalidOperationException("Deserialization failed");
    }
}
