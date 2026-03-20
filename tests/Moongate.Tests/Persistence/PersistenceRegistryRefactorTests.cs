using System.Reflection;
using Moongate.Persistence.Data.Persistence;
using Moongate.Persistence.Services.Persistence;
using Moongate.Tests.TestSupport;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.Persistence.Types;

namespace Moongate.Tests.Persistence;

public class PersistenceRegistryRefactorTests
{
    [Test]
    public void WorldSnapshot_ShouldExposeEntityBucketsProperty()
    {
        var property = typeof(WorldSnapshot).GetProperty("EntityBuckets", BindingFlags.Public | BindingFlags.Instance);

        Assert.That(property, Is.Not.Null);
    }

    [Test]
    public void JournalEntry_ShouldExposeGenericTypeAndOperationMetadata()
    {
        var typeIdProperty = typeof(JournalEntry).GetProperty("TypeId", BindingFlags.Public | BindingFlags.Instance);
        var operationProperty = typeof(JournalEntry).GetProperty("Operation", BindingFlags.Public | BindingFlags.Instance);
        var legacyOperationProperty = typeof(JournalEntry).GetProperty("OperationType", BindingFlags.Public | BindingFlags.Instance);

        Assert.Multiple(
            () =>
            {
                Assert.That(typeIdProperty, Is.Not.Null);
                Assert.That(operationProperty, Is.Not.Null);
                Assert.That(legacyOperationProperty, Is.Null);
            }
        );
    }

    [Test]
    public void WorldSnapshot_ShouldNotExposeLegacyTypedSnapshotArrays()
    {
        var accountsProperty = typeof(WorldSnapshot).GetProperty("Accounts", BindingFlags.Public | BindingFlags.Instance);
        var mobilesProperty = typeof(WorldSnapshot).GetProperty("Mobiles", BindingFlags.Public | BindingFlags.Instance);
        var itemsProperty = typeof(WorldSnapshot).GetProperty("Items", BindingFlags.Public | BindingFlags.Instance);

        Assert.Multiple(
            () =>
            {
                Assert.That(accountsProperty, Is.Null);
                Assert.That(mobilesProperty, Is.Null);
                Assert.That(itemsProperty, Is.Null);
            }
        );
    }

    [Test]
    public void PersistenceUnitOfWork_ShouldExposeGenericRepositoryAccessor()
    {
        var method = typeof(PersistenceUnitOfWork).GetMethods(BindingFlags.Public | BindingFlags.Instance)
                                                 .SingleOrDefault(
                                                     candidate => candidate.Name == "GetRepository" &&
                                                                  candidate.IsGenericMethodDefinition &&
                                                                  candidate.GetGenericArguments().Length == 2
                                                 );

        Assert.That(method, Is.Not.Null);
    }

    [Test]
    public async Task CaptureSnapshotAsync_ShouldPopulateEntityBucketsForPersistedEntities()
    {
        using var tempDirectory = new TempDirectory();
        var unitOfWork = CreateUnitOfWork(tempDirectory.Path);
        await unitOfWork.InitializeAsync();

        await unitOfWork.Accounts.UpsertAsync(
            new UOAccountEntity
            {
                Id = (Serial)0x00000044,
                Username = "bucket-user",
                PasswordHash = "pw"
            }
        );

        var captured = await unitOfWork.CaptureSnapshotAsync();

        Assert.That(captured.Snapshot.EntityBuckets, Is.Not.Empty);
    }

    [Test]
    public async Task AccountUpsert_ShouldWriteGenericJournalMetadata()
    {
        using var tempDirectory = new TempDirectory();
        var unitOfWork = CreateUnitOfWork(tempDirectory.Path);
        await unitOfWork.InitializeAsync();

        await unitOfWork.Accounts.UpsertAsync(
            new UOAccountEntity
            {
                Id = (Serial)0x00000045,
                Username = "journal-user",
                PasswordHash = "pw"
            }
        );

        using var journalService = new BinaryJournalService(Path.Combine(tempDirectory.Path, "world.journal.bin"), false);
        var entries = await journalService.ReadAllAsync();
        var entry = entries.Single();

        Assert.Multiple(
            () =>
            {
                Assert.That(entry.TypeId, Is.Not.EqualTo(0));
                Assert.That(entry.Operation, Is.EqualTo(JournalEntityOperationType.Upsert));
            }
        );
    }

    private static PersistenceUnitOfWork CreateUnitOfWork(string directory)
        => new(
            new PersistenceOptions(
                Path.Combine(directory, "world.snapshot.bin"),
                Path.Combine(directory, "world.journal.bin"),
                false
            )
        );
}
