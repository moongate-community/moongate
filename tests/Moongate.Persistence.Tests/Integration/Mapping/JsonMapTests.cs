using Moongate.Persistence.Services;
using Moongate.Persistence.Tests.TestSupport.Persistence;
using Moongate.Persistence.Tests.TestSupport.Persistence.Data;
using Moongate.Persistence.Types.Persistence;

namespace Moongate.Persistence.Tests.Integration.Mapping;

[Collection(PostgreSqlCollection.Name)]
public sealed class JsonMapTests
{
    private readonly PostgreSqlFixture _postgres;

    public JsonMapTests(PostgreSqlFixture postgres)
    {
        _postgres = postgres;
    }

    [Theory, InlineData(PersistenceDatabaseTarget.Accounts), InlineData(PersistenceDatabaseTarget.Realm)]
    public async Task UpsertAsync_CustomCollectionAndObject_RoundTripAsJsonbAfterReopen(PersistenceDatabaseTarget target)
    {
        await using var database = await _postgres.CreateDatabaseAsync();
        var entity = new JsonProfileEntity
        {
            QuestProgress =
            [
                new() { QuestId = 10, Description = "L'elfo dice \"ciao\" — 日本語", Milestones = [2, 5] },
                new() { QuestId = 25, Completed = true }
            ],
            ActiveQuest = new() { QuestId = 10, Description = "active", Milestones = [2] }
        };

        await using (var owner = CreateOwner(database, target))
        {
            var store = owner.RegisterEntity<JsonProfileEntity>(target: target);
            await owner.InitializeAsync();
            await store.UpsertAsync(entity);
            Assert.True(entity.Id.IsValid);
        }

        await using var reopened = CreateOwner(database, target);
        var reopenedStore = reopened.RegisterEntity<JsonProfileEntity>(target: target);
        await reopened.InitializeAsync();
        var loaded = Assert.IsType<JsonProfileEntity>(await reopenedStore.GetByIdAsync(entity.Id));
        Assert.Equal(entity.Id, loaded.Id);
        Assert.Collection(
            Assert.IsType<List<QuestProgressData>>(loaded.QuestProgress),
            first =>
            {
                Assert.Equal(10, first.QuestId);
                Assert.Equal("L'elfo dice \"ciao\" — 日本語", first.Description);
                Assert.False(first.Completed);
                Assert.Equal([2, 5], first.Milestones);
            },
            second =>
            {
                Assert.Equal(25, second.QuestId);
                Assert.True(second.Completed);
                Assert.Empty(second.Milestones);
            }
        );
        var active = Assert.IsType<QuestProgressData>(loaded.ActiveQuest);
        Assert.Equal(10, active.QuestId);
        Assert.Equal("active", active.Description);
        Assert.Equal([2], active.Milestones);
        Assert.Equal(
            2L,
            await database.ScalarAsync<long>(
                "SELECT count(*) FROM information_schema.columns WHERE table_schema = 'json_mapping' " +
                "AND table_name = 'profiles' AND column_name IN ('quest_progress', 'active_quest') AND data_type = 'jsonb'"
            )
        );
        Assert.Equal(
            "10",
            await database.ScalarAsync<string>(
                "SELECT quest_progress->0->>'QuestId' FROM json_mapping.profiles"
            )
        );
        Assert.Empty(await reopened.PreviewSchemaAsync());
    }

    [Fact]
    public async Task UpsertAsync_ModifiedDetachedJson_ReplacesStoredValueOnlyAfterExplicitSave()
    {
        await using var database = await _postgres.CreateDatabaseAsync();
        await using var owner = CreateOwner(database);
        var store = owner.RegisterEntity<JsonProfileEntity>(target: PersistenceDatabaseTarget.Realm);
        await owner.InitializeAsync();
        var entity = new JsonProfileEntity { QuestProgress = [new() { QuestId = 10, Milestones = [1] }] };
        await store.UpsertAsync(entity);
        var copy = Assert.IsType<JsonProfileEntity>(await store.GetByIdAsync(entity.Id));
        var quests = Assert.IsType<List<QuestProgressData>>(copy.QuestProgress);
        quests[0].Completed = true;
        quests[0].Milestones.Add(2);
        quests.Add(new() { QuestId = 20 });

        var unchanged = Assert.IsType<JsonProfileEntity>(await store.GetByIdAsync(entity.Id));
        var original = Assert.Single(Assert.IsType<List<QuestProgressData>>(unchanged.QuestProgress));
        Assert.False(original.Completed);
        Assert.Equal([1], original.Milestones);

        await store.UpsertAsync(copy);
        var updated = Assert.IsType<JsonProfileEntity>(await store.GetByIdAsync(entity.Id));
        var saved = Assert.IsType<List<QuestProgressData>>(updated.QuestProgress);
        Assert.Equal(2, saved.Count);
        Assert.True(saved[0].Completed);
        Assert.Equal([1, 2], saved[0].Milestones);
        Assert.Equal(20, saved[1].QuestId);
        copy.QuestProgress = [];
        await store.UpsertAsync(copy);
        var empty = Assert.IsType<JsonProfileEntity>(await store.GetByIdAsync(entity.Id));
        Assert.Empty(Assert.IsType<List<QuestProgressData>>(empty.QuestProgress));
        copy.QuestProgress = null;
        await store.UpsertAsync(copy);
        var cleared = Assert.IsType<JsonProfileEntity>(await store.GetByIdAsync(entity.Id));
        Assert.Null(cleared.QuestProgress);
        Assert.True(await database.ScalarAsync<bool>("SELECT quest_progress IS NULL FROM json_mapping.profiles"));
        Assert.Equal(1L, await database.ScalarAsync<long>("SELECT count(*) FROM json_mapping.profiles"));
    }

    [Fact]
    public async Task UpsertAsync_EmptyAndNullCollections_PreserveDistinctDatabaseValues()
    {
        await using var database = await _postgres.CreateDatabaseAsync();
        await using var owner = CreateOwner(database);
        var store = owner.RegisterEntity<JsonProfileEntity>(target: PersistenceDatabaseTarget.Realm);
        await owner.InitializeAsync();
        foreach (var useNull in new[] { false, true })
        {
            var entity = new JsonProfileEntity { QuestProgress = useNull ? null : [] };
            await store.UpsertAsync(entity);
            var loaded = Assert.IsType<JsonProfileEntity>(await store.GetByIdAsync(entity.Id));

            if (useNull)
            {
                Assert.Null(loaded.QuestProgress);
            }
            else
            {
                Assert.Empty(Assert.IsType<List<QuestProgressData>>(loaded.QuestProgress));
            }

            Assert.Null(loaded.ActiveQuest);
            Assert.Equal(
                useNull,
                await database.ScalarAsync<bool>(
                    $"SELECT quest_progress IS NULL FROM json_mapping.profiles WHERE id = {entity.Id.Value}"
                )
            );
            Assert.Equal(
                useNull ? null : "[]",
                await database.ScalarAsync<string>(
                    $"SELECT quest_progress::text FROM json_mapping.profiles WHERE id = {entity.Id.Value}"
                )
            );
        }
    }

    [Fact]
    public async Task SaveAllAsync_DeepSnapshot_PersistsJsonCapturedBeforeLiveChanges()
    {
        await using var database = await _postgres.CreateDatabaseAsync();
        await using var owner = CreateOwner(database);
        var live = new JsonProfileEntity { Id = new(42), QuestProgress = [new() { QuestId = 10, Milestones = [1] }] };
        var store = owner.RegisterEntity<JsonProfileEntity>(
            () => [live],
            entity => new()
            {
                Id = entity.Id,
                QuestProgress = entity.QuestProgress?.Select(quest => new QuestProgressData
                        {
                            QuestId = quest.QuestId,
                            Description = quest.Description,
                            Completed = quest.Completed,
                            Milestones = [.. quest.Milestones]
                        }
                    )
                    .ToList()
            },
            PersistenceDatabaseTarget.Realm
        );
        await owner.InitializeAsync();
        await owner.SaveAllAsync((capture, _) =>
            {
                capture();
                live.QuestProgress[0].Milestones.Add(99);
                live.QuestProgress.Clear();

                return Task.CompletedTask;
            }
        );

        var saved = Assert.IsType<JsonProfileEntity>(await store.GetByIdAsync(live.Id));
        var quest = Assert.Single(Assert.IsType<List<QuestProgressData>>(saved.QuestProgress));
        Assert.Equal(10, quest.QuestId);
        Assert.Equal([1], quest.Milestones);
        Assert.Empty(live.QuestProgress);
    }

    private static MoongatePersistenceService CreateOwner(
        PostgreSqlTestDatabase database,
        PersistenceDatabaseTarget target = PersistenceDatabaseTarget.Realm
    )
    {
        return new(new([new(target, database.ConnectionString)], autoSynchronizeSchema: true));
    }
}
