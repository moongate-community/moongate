using Moongate.Persistence.Data.Persistence;
using Moongate.Persistence.Services.Persistence;
using Moongate.Tests.TestSupport;
using Moongate.UO.Data.Geometry;
using Moongate.UO.Data.Ids;
using Moongate.UO.Data.Persistence.Entities;
using Moongate.UO.Data.Skills;
using Moongate.UO.Data.Types;

namespace Moongate.Tests.Persistence;

public class PersistenceUnitOfWorkTests
{
    private static readonly string[] ExpectedAccountUsernames = ["admin", "alpha"];

    [Test]
    public async Task Accounts_ShouldUseSerialAsEntityId()
    {
        using var tempDirectory = new TempDirectory();
        var unitOfWork = CreateUnitOfWork(tempDirectory.Path);
        await unitOfWork.InitializeAsync();

        var account = new UOAccountEntity
        {
            Id = (Serial)0x00000001,
            Username = "tester",
            PasswordHash = "hash"
        };

        await unitOfWork.Accounts.UpsertAsync(account);

        var loadedById = await unitOfWork.Accounts.GetByIdAsync((Serial)0x00000001);
        var loadedByName = await unitOfWork.Accounts.GetByUsernameAsync("tester");

        Assert.Multiple(
            () =>
            {
                Assert.That(loadedById, Is.Not.Null);
                Assert.That(loadedByName, Is.Not.Null);
                Assert.That(loadedById!.Id, Is.EqualTo((Serial)0x00000001));
                Assert.That(loadedByName!.Id, Is.EqualTo((Serial)0x00000001));
            }
        );
    }

    [Test]
    public async Task AddAsync_ShouldRejectDuplicateUsername()
    {
        using var tempDirectory = new TempDirectory();
        var unitOfWork = CreateUnitOfWork(tempDirectory.Path);
        await unitOfWork.InitializeAsync();

        var first = await unitOfWork.Accounts.AddAsync(
                        new()
                        {
                            Id = (Serial)0x00000005,
                            Username = "dupe-user",
                            PasswordHash = "pw"
                        }
                    );

        var second = await unitOfWork.Accounts.AddAsync(
                         new()
                         {
                             Id = (Serial)0x00000006,
                             Username = "dupe-user",
                             PasswordHash = "pw"
                         }
                     );

        Assert.Multiple(
            () =>
            {
                Assert.That(first, Is.True);
                Assert.That(second, Is.False);
            }
        );
    }

    [Test]
    public async Task AllocateNextIds_AfterReload_ShouldContinueFromMaxPersistedIds()
    {
        using var tempDirectory = new TempDirectory();
        var firstUnitOfWork = CreateUnitOfWork(tempDirectory.Path);
        await firstUnitOfWork.InitializeAsync();

        await firstUnitOfWork.Accounts.UpsertAsync(
            new()
            {
                Id = (Serial)0x00000010,
                Username = "allocator-account",
                PasswordHash = "pw"
            }
        );

        await firstUnitOfWork.Mobiles.UpsertAsync(
            new()
            {
                Id = (Serial)0x00000020,
                IsPlayer = true,
                IsAlive = true
            }
        );

        await firstUnitOfWork.Items.UpsertAsync(
            new()
            {
                Id = (Serial)(Serial.ItemOffset + 10),
                ItemId = 0x0EED
            }
        );

        await firstUnitOfWork.SaveSnapshotAsync();

        var secondUnitOfWork = CreateUnitOfWork(tempDirectory.Path);
        await secondUnitOfWork.InitializeAsync();

        var nextAccountId = secondUnitOfWork.AllocateNextAccountId();
        var nextMobileId = secondUnitOfWork.AllocateNextMobileId();
        var nextItemId = secondUnitOfWork.AllocateNextItemId();

        Assert.Multiple(
            () =>
            {
                Assert.That(nextAccountId, Is.EqualTo((Serial)0x00000011));
                Assert.That(nextMobileId, Is.EqualTo((Serial)0x00000021));
                Assert.That(nextItemId, Is.EqualTo((Serial)(Serial.ItemOffset + 11)));
            }
        );
    }

    [Test]
    public async Task AllocateNextIds_ShouldReturnProgressiveValuesPerEntityType()
    {
        using var tempDirectory = new TempDirectory();
        var unitOfWork = CreateUnitOfWork(tempDirectory.Path);
        await unitOfWork.InitializeAsync();

        var account1 = unitOfWork.AllocateNextAccountId();
        var account2 = unitOfWork.AllocateNextAccountId();
        var mobile1 = unitOfWork.AllocateNextMobileId();
        var mobile2 = unitOfWork.AllocateNextMobileId();
        var item1 = unitOfWork.AllocateNextItemId();
        var item2 = unitOfWork.AllocateNextItemId();

        Assert.Multiple(
            () =>
            {
                Assert.That(account1, Is.EqualTo((Serial)0x00000001));
                Assert.That(account2, Is.EqualTo((Serial)0x00000002));
                Assert.That(mobile1, Is.EqualTo((Serial)0x00000001));
                Assert.That(mobile2, Is.EqualTo((Serial)0x00000002));
                Assert.That(item1, Is.EqualTo((Serial)Serial.ItemOffset));
                Assert.That(item2, Is.EqualTo((Serial)(Serial.ItemOffset + 1)));
            }
        );
    }

    [Test]
    public async Task BulletinBoardMessages_ShouldPersistAcrossSnapshotReload()
    {
        using var tempDirectory = new TempDirectory();
        var firstUnitOfWork = CreateUnitOfWork(tempDirectory.Path);
        await firstUnitOfWork.InitializeAsync();

        var message = new BulletinBoardMessageEntity
        {
            MessageId = (Serial)(Serial.ItemOffset + 50),
            BoardId = (Serial)0x40000055u,
            ParentId = Serial.Zero,
            OwnerCharacterId = (Serial)0x00000042u,
            Author = "Poster",
            Subject = "Hello",
            PostedAtUtc = new(2026, 3, 13, 12, 30, 0, DateTimeKind.Utc)
        };
        message.BodyLines.AddRange(["alpha", "beta"]);

        await firstUnitOfWork.BulletinBoardMessages.UpsertAsync(message);
        await firstUnitOfWork.SaveSnapshotAsync();

        var secondUnitOfWork = CreateUnitOfWork(tempDirectory.Path);
        await secondUnitOfWork.InitializeAsync();

        var restored = await secondUnitOfWork.BulletinBoardMessages.GetByIdAsync(message.MessageId);

        Assert.Multiple(
            () =>
            {
                Assert.That(restored, Is.Not.Null);
                Assert.That(restored!.BoardId, Is.EqualTo(message.BoardId));
                Assert.That(restored.Subject, Is.EqualTo("Hello"));
                Assert.That(restored.BodyLines, Is.EqualTo(new[] { "alpha", "beta" }));
            }
        );
    }

    [Test]
    public async Task HelpTickets_ShouldPersistAcrossSnapshotReload()
    {
        using var tempDirectory = new TempDirectory();
        var firstUnitOfWork = CreateUnitOfWork(tempDirectory.Path);
        await firstUnitOfWork.InitializeAsync();

        var ticket = new HelpTicketEntity
        {
            Id = (Serial)(Serial.ItemOffset + 75),
            SenderCharacterId = (Serial)0x00000042u,
            SenderAccountId = (Serial)0x00000010u,
            Category = HelpTicketCategory.Question,
            Message = "I am stuck behind the innkeeper counter.",
            MapId = 0,
            Location = new Point3D(1443, 1692, 0),
            Status = HelpTicketStatus.Open,
            CreatedAtUtc = new(2026, 3, 19, 9, 30, 0, DateTimeKind.Utc),
            LastUpdatedAtUtc = new(2026, 3, 19, 9, 30, 0, DateTimeKind.Utc)
        };

        await firstUnitOfWork.HelpTickets.UpsertAsync(ticket);
        await firstUnitOfWork.SaveSnapshotAsync();

        var secondUnitOfWork = CreateUnitOfWork(tempDirectory.Path);
        await secondUnitOfWork.InitializeAsync();

        var restored = await secondUnitOfWork.HelpTickets.GetByIdAsync(ticket.Id);

        Assert.Multiple(
            () =>
            {
                Assert.That(restored, Is.Not.Null);
                Assert.That(restored!.SenderCharacterId, Is.EqualTo(ticket.SenderCharacterId));
                Assert.That(restored.SenderAccountId, Is.EqualTo(ticket.SenderAccountId));
                Assert.That(restored.Category, Is.EqualTo(HelpTicketCategory.Question));
                Assert.That(restored.Message, Is.EqualTo("I am stuck behind the innkeeper counter."));
                Assert.That(restored.MapId, Is.EqualTo(0));
                Assert.That(restored.Location, Is.EqualTo(new Point3D(1443, 1692, 0)));
                Assert.That(restored.Status, Is.EqualTo(HelpTicketStatus.Open));
                Assert.That(restored.CreatedAtUtc, Is.EqualTo(ticket.CreatedAtUtc));
                Assert.That(restored.LastUpdatedAtUtc, Is.EqualTo(ticket.LastUpdatedAtUtc));
            }
        );
    }

    [Test]
    public async Task CaptureSnapshotAsync_ShouldReturnSnapshotAndCapturedSequenceId()
    {
        using var tempDirectory = new TempDirectory();
        var unitOfWork = CreateUnitOfWork(tempDirectory.Path);
        await unitOfWork.InitializeAsync();

        await unitOfWork.Accounts.UpsertAsync(
            new()
            {
                Id = (Serial)0x00000010,
                Username = "capture-user",
                PasswordHash = "pw"
            }
        );

        await unitOfWork.Mobiles.UpsertAsync(new() { Id = (Serial)0x00000020, IsPlayer = true, IsAlive = true });
        await unitOfWork.Items.UpsertAsync(new() { Id = (Serial)(Serial.ItemOffset + 5), ItemId = 0x0EED });

        var captured = await unitOfWork.CaptureSnapshotAsync();

        Assert.Multiple(
            () =>
            {
                Assert.That(captured.Snapshot.Accounts, Has.Length.EqualTo(1));
                Assert.That(captured.Snapshot.Mobiles, Has.Length.EqualTo(1));
                Assert.That(captured.Snapshot.Items, Has.Length.EqualTo(1));
                Assert.That(captured.CapturedLastSequenceId, Is.GreaterThan(0));
            }
        );
    }

    [Test]
    public async Task ConcurrentAccountUpserts_ShouldRemainConsistentAfterReload()
    {
        using var tempDirectory = new TempDirectory();
        var unitOfWork = CreateUnitOfWork(tempDirectory.Path);
        await unitOfWork.InitializeAsync();

        const int taskCount = 16;
        const int writesPerTask = 50;

        var tasks = Enumerable.Range(0, taskCount)
                              .Select(
                                  taskIndex => Task.Run(
                                      async () =>
                                      {
                                          for (var i = 0; i < writesPerTask; i++)
                                          {
                                              var globalIndex = taskIndex * writesPerTask + i;

                                              await unitOfWork.Accounts.UpsertAsync(
                                                  new()
                                                  {
                                                      Id = (Serial)(uint)(0x00010000 + globalIndex),
                                                      Username = $"concurrent-{globalIndex}",
                                                      PasswordHash = "pw"
                                                  }
                                              );
                                          }
                                      }
                                  )
                              )
                              .ToArray();

        await Task.WhenAll(tasks);
        await unitOfWork.SaveSnapshotAsync();

        var reloadedUnitOfWork = CreateUnitOfWork(tempDirectory.Path);
        await reloadedUnitOfWork.InitializeAsync();

        var accounts = await reloadedUnitOfWork.Accounts.GetAllAsync();

        Assert.That(accounts.Count, Is.EqualTo(taskCount * writesPerTask));

        for (var i = 0; i < taskCount * writesPerTask; i++)
        {
            var username = $"concurrent-{i}";
            var loaded = await reloadedUnitOfWork.Accounts.GetByUsernameAsync(username);

            Assert.That(loaded, Is.Not.Null, $"Missing account '{username}' after concurrent writes.");
        }
    }

    [Test]
    public async Task ConcurrentAddAndRemove_OnSingleUnitOfWork_ShouldRemainConsistentAfterReload()
    {
        using var tempDirectory = new TempDirectory();
        var unitOfWork = CreateUnitOfWork(tempDirectory.Path);
        await unitOfWork.InitializeAsync();

        const int workerCount = 8;
        const int recordsPerWorker = 50;

        var tasks = Enumerable.Range(0, workerCount)
                              .Select(
                                  workerIndex => Task.Run(
                                      async () =>
                                      {
                                          for (var i = 0; i < recordsPerWorker; i++)
                                          {
                                              var idValue = 0x000A0000u + (uint)(workerIndex * recordsPerWorker + i);
                                              var id = (Serial)idValue;
                                              var username = $"temp-{idValue}";

                                              var added = await unitOfWork.Accounts.AddAsync(
                                                              new()
                                                              {
                                                                  Id = id,
                                                                  Username = username,
                                                                  PasswordHash = "pw"
                                                              }
                                                          );

                                              Assert.That(added, Is.True);

                                              var removed = await unitOfWork.Accounts.RemoveAsync(id);
                                              Assert.That(removed, Is.True);
                                          }
                                      }
                                  )
                              )
                              .ToArray();

        await Task.WhenAll(tasks);
        await unitOfWork.SaveSnapshotAsync();

        var reloadedUnitOfWork = CreateUnitOfWork(tempDirectory.Path);
        await reloadedUnitOfWork.InitializeAsync();

        var count = await reloadedUnitOfWork.Accounts.CountAsync();
        Assert.That(count, Is.Zero);
    }

    [Test]
    public async Task ConcurrentWritersAcrossMultipleUnitOfWorkInstances_ShouldRemainConsistentAfterReload()
    {
        using var tempDirectory = new TempDirectory();
        var firstUnitOfWork = CreateUnitOfWork(tempDirectory.Path);
        var secondUnitOfWork = CreateUnitOfWork(tempDirectory.Path);
        await firstUnitOfWork.InitializeAsync();
        await secondUnitOfWork.InitializeAsync();

        const int writesPerWriter = 150;

        var firstWriterTask = WriteAccountsWithRetryAsync(firstUnitOfWork, 1_000, writesPerWriter);
        var secondWriterTask = WriteAccountsWithRetryAsync(secondUnitOfWork, 2_000, writesPerWriter);

        await Task.WhenAll(firstWriterTask, secondWriterTask);

        var reloadedUnitOfWork = CreateUnitOfWork(tempDirectory.Path);
        await reloadedUnitOfWork.InitializeAsync();

        var accounts = await reloadedUnitOfWork.Accounts.GetAllAsync();
        Assert.That(accounts.Count, Is.EqualTo(writesPerWriter * 2));

        for (var i = 0; i < writesPerWriter; i++)
        {
            var firstUsername = $"multi-uow-{1_000 + i}";
            var secondUsername = $"multi-uow-{2_000 + i}";

            Assert.That(
                await reloadedUnitOfWork.Accounts.GetByUsernameAsync(firstUsername),
                Is.Not.Null,
                $"Missing account '{firstUsername}' after multi-instance writes."
            );
            Assert.That(
                await reloadedUnitOfWork.Accounts.GetByUsernameAsync(secondUsername),
                Is.Not.Null,
                $"Missing account '{secondUsername}' after multi-instance writes."
            );
        }
    }

    [Test]
    public async Task CountAsync_OnRepositories_ShouldReturnExpectedValues()
    {
        using var tempDirectory = new TempDirectory();
        var unitOfWork = CreateUnitOfWork(tempDirectory.Path);
        await unitOfWork.InitializeAsync();

        await unitOfWork.Accounts.UpsertAsync(new() { Id = (Serial)0x1201, Username = "count-a", PasswordHash = "pw" });
        await unitOfWork.Accounts.UpsertAsync(new() { Id = (Serial)0x1202, Username = "count-b", PasswordHash = "pw" });
        await unitOfWork.Mobiles.UpsertAsync(new() { Id = (Serial)0x2201, IsPlayer = true, IsAlive = true });
        await unitOfWork.Items.UpsertAsync(new() { Id = (Serial)0x3201, ItemId = 0x0EED });
        await unitOfWork.Items.UpsertAsync(new() { Id = (Serial)0x3202, ItemId = 0x0F3F });
        await unitOfWork.Items.UpsertAsync(new() { Id = (Serial)0x3203, ItemId = 0x0EED });

        var accountCount = await unitOfWork.Accounts.CountAsync();
        var mobileCount = await unitOfWork.Mobiles.CountAsync();
        var itemCount = await unitOfWork.Items.CountAsync();

        Assert.Multiple(
            () =>
            {
                Assert.That(accountCount, Is.EqualTo(2));
                Assert.That(mobileCount, Is.EqualTo(1));
                Assert.That(itemCount, Is.EqualTo(3));
            }
        );
    }

    [Test]
    public async Task ExistsAsync_OnAccounts_ShouldReturnExpectedValue()
    {
        using var tempDirectory = new TempDirectory();
        var unitOfWork = CreateUnitOfWork(tempDirectory.Path);
        await unitOfWork.InitializeAsync();

        await unitOfWork.Accounts.UpsertAsync(new() { Id = (Serial)0x1101, Username = "exists-user", PasswordHash = "pw" });

        var exists = await unitOfWork.Accounts.ExistsAsync(account => account.Username == "exists-user");
        var notExists = await unitOfWork.Accounts.ExistsAsync(account => account.Username == "missing-user");

        Assert.Multiple(
            () =>
            {
                Assert.That(exists, Is.True);
                Assert.That(notExists, Is.False);
            }
        );
    }

    [Test]
    public async Task InitializeAsync_ShouldPreserveAccountEmailAcrossSnapshotAndJournalReplay()
    {
        using var tempDirectory = new TempDirectory();
        var unitOfWork = CreateUnitOfWork(tempDirectory.Path);
        await unitOfWork.InitializeAsync();

        await unitOfWork.Accounts.UpsertAsync(
            new()
            {
                Id = (Serial)0x00000050,
                Username = "snapshot-email",
                PasswordHash = "pw",
                Email = "snapshot@moongate.local"
            }
        );

        await unitOfWork.SaveSnapshotAsync();

        await unitOfWork.Accounts.UpsertAsync(
            new()
            {
                Id = (Serial)0x00000051,
                Username = "journal-email",
                PasswordHash = "pw",
                Email = "journal@moongate.local"
            }
        );

        var secondUnitOfWork = CreateUnitOfWork(tempDirectory.Path);
        await secondUnitOfWork.InitializeAsync();

        var snapshotAccount = await secondUnitOfWork.Accounts.GetByUsernameAsync("snapshot-email");
        var journalAccount = await secondUnitOfWork.Accounts.GetByUsernameAsync("journal-email");

        Assert.Multiple(
            () =>
            {
                Assert.That(snapshotAccount, Is.Not.Null);
                Assert.That(journalAccount, Is.Not.Null);
                Assert.That(snapshotAccount!.Email, Is.EqualTo("snapshot@moongate.local"));
                Assert.That(journalAccount!.Email, Is.EqualTo("journal@moongate.local"));
            }
        );
    }

    [Test]
    public async Task InitializeAsync_ShouldPreserveAccountLockStateAcrossSnapshotAndJournalReplay()
    {
        using var tempDirectory = new TempDirectory();
        var unitOfWork = CreateUnitOfWork(tempDirectory.Path);
        await unitOfWork.InitializeAsync();

        await unitOfWork.Accounts.UpsertAsync(
            new()
            {
                Id = (Serial)0x00000040,
                Username = "snapshot-locked",
                PasswordHash = "pw",
                IsLocked = true
            }
        );

        await unitOfWork.SaveSnapshotAsync();

        await unitOfWork.Accounts.UpsertAsync(
            new()
            {
                Id = (Serial)0x00000041,
                Username = "journal-unlocked",
                PasswordHash = "pw",
                IsLocked = false
            }
        );

        var secondUnitOfWork = CreateUnitOfWork(tempDirectory.Path);
        await secondUnitOfWork.InitializeAsync();

        var snapshotAccount = await secondUnitOfWork.Accounts.GetByUsernameAsync("snapshot-locked");
        var journalAccount = await secondUnitOfWork.Accounts.GetByUsernameAsync("journal-unlocked");

        Assert.Multiple(
            () =>
            {
                Assert.That(snapshotAccount, Is.Not.Null);
                Assert.That(journalAccount, Is.Not.Null);
                Assert.That(snapshotAccount!.IsLocked, Is.True);
                Assert.That(journalAccount!.IsLocked, Is.False);
            }
        );
    }

    [Test]
    public async Task InitializeAsync_ShouldPreserveAccountTypeAcrossSnapshotAndJournalReplay()
    {
        using var tempDirectory = new TempDirectory();
        var unitOfWork = CreateUnitOfWork(tempDirectory.Path);
        await unitOfWork.InitializeAsync();

        await unitOfWork.Accounts.UpsertAsync(
            new()
            {
                Id = (Serial)0x00000030,
                Username = "snapshot-privileged",
                PasswordHash = "pw",
                AccountType = AccountType.GameMaster
            }
        );

        await unitOfWork.SaveSnapshotAsync();

        await unitOfWork.Accounts.UpsertAsync(
            new()
            {
                Id = (Serial)0x00000031,
                Username = "journal-privileged",
                PasswordHash = "pw",
                AccountType = AccountType.Administrator
            }
        );

        var secondUnitOfWork = CreateUnitOfWork(tempDirectory.Path);
        await secondUnitOfWork.InitializeAsync();

        var snapshotAccount = await secondUnitOfWork.Accounts.GetByUsernameAsync("snapshot-privileged");
        var journalAccount = await secondUnitOfWork.Accounts.GetByUsernameAsync("journal-privileged");

        Assert.Multiple(
            () =>
            {
                Assert.That(snapshotAccount, Is.Not.Null);
                Assert.That(journalAccount, Is.Not.Null);
                Assert.That(snapshotAccount!.AccountType, Is.EqualTo(AccountType.GameMaster));
                Assert.That(journalAccount!.AccountType, Is.EqualTo(AccountType.Administrator));
            }
        );
    }

    [Test]
    public async Task InitializeAsync_ShouldPreserveItemNameAcrossSnapshotAndJournalReplay()
    {
        using var tempDirectory = new TempDirectory();
        var unitOfWork = CreateUnitOfWork(tempDirectory.Path);
        await unitOfWork.InitializeAsync();

        await unitOfWork.Items.UpsertAsync(
            new()
            {
                Id = (Serial)0x40001000,
                Name = "Snapshot Item",
                Weight = 2,
                Amount = 11,
                IsStackable = true,
                Rarity = ItemRarity.Uncommon,
                ItemId = 0x0EED
            }
        );

        await unitOfWork.SaveSnapshotAsync();

        await unitOfWork.Items.UpsertAsync(
            new()
            {
                Id = (Serial)0x40001001,
                Name = "Journal Item",
                Weight = 3,
                Amount = 7,
                IsStackable = false,
                Rarity = ItemRarity.Epic,
                ItemId = 0x0F3F
            }
        );

        var secondUnitOfWork = CreateUnitOfWork(tempDirectory.Path);
        await secondUnitOfWork.InitializeAsync();

        var snapshotItem = await secondUnitOfWork.Items.GetByIdAsync((Serial)0x40001000);
        var journalItem = await secondUnitOfWork.Items.GetByIdAsync((Serial)0x40001001);

        Assert.Multiple(
            () =>
            {
                Assert.That(snapshotItem, Is.Not.Null);
                Assert.That(journalItem, Is.Not.Null);
                Assert.That(snapshotItem!.Name, Is.EqualTo("Snapshot Item"));
                Assert.That(journalItem!.Name, Is.EqualTo("Journal Item"));
                Assert.That(snapshotItem.Weight, Is.EqualTo(2));
                Assert.That(journalItem.Weight, Is.EqualTo(3));
                Assert.That(snapshotItem.Amount, Is.EqualTo(11));
                Assert.That(journalItem.Amount, Is.EqualTo(7));
                Assert.That(snapshotItem.IsStackable, Is.True);
                Assert.That(journalItem.IsStackable, Is.False);
                Assert.That(snapshotItem.Rarity, Is.EqualTo(ItemRarity.Uncommon));
                Assert.That(journalItem.Rarity, Is.EqualTo(ItemRarity.Epic));
            }
        );
    }

    [Test]
    public async Task InitializeAsync_ShouldReplayJournalEntriesAfterSnapshot()
    {
        using var tempDirectory = new TempDirectory();
        var unitOfWork = CreateUnitOfWork(tempDirectory.Path);
        await unitOfWork.InitializeAsync();

        await unitOfWork.Accounts.UpsertAsync(
            new()
            {
                Id = (Serial)0x00000003,
                Username = "before-snapshot",
                PasswordHash = "pw"
            }
        );

        await unitOfWork.SaveSnapshotAsync();

        await unitOfWork.Accounts.UpsertAsync(
            new()
            {
                Id = (Serial)0x00000004,
                Username = "after-snapshot",
                PasswordHash = "pw"
            }
        );

        var secondUnitOfWork = CreateUnitOfWork(tempDirectory.Path);
        await secondUnitOfWork.InitializeAsync();

        Assert.That(await secondUnitOfWork.Accounts.GetByUsernameAsync("before-snapshot"), Is.Not.Null);
        Assert.That(await secondUnitOfWork.Accounts.GetByUsernameAsync("after-snapshot"), Is.Not.Null);
    }

    [Test]
    public async Task InitializeAsync_WhenFileLockEnabled_ShouldPreventSecondUnitOfWorkFromOpeningSameSaveFiles()
    {
        using var tempDirectory = new TempDirectory();
        using var firstUnitOfWork = CreateUnitOfWork(tempDirectory.Path, true);
        await firstUnitOfWork.InitializeAsync();

        Assert.That(
            async () =>
            {
                using var secondUnitOfWork = CreateUnitOfWork(tempDirectory.Path, true);
                await secondUnitOfWork.InitializeAsync();
            },
            Throws.TypeOf<IOException>()
        );
    }

    [Test]
    public async Task QueryAsync_OnAccounts_ShouldProjectMatchingResults()
    {
        using var tempDirectory = new TempDirectory();
        var unitOfWork = CreateUnitOfWork(tempDirectory.Path);
        await unitOfWork.InitializeAsync();

        await unitOfWork.Accounts.UpsertAsync(new() { Id = (Serial)0x1001, Username = "alpha", PasswordHash = "pw" });
        await unitOfWork.Accounts.UpsertAsync(new() { Id = (Serial)0x1002, Username = "beta", PasswordHash = "pw" });
        await unitOfWork.Accounts.UpsertAsync(new() { Id = (Serial)0x1003, Username = "admin", PasswordHash = "pw" });

        var usernames = await unitOfWork.Accounts.QueryAsync(
                            account => account.Username.StartsWith('a'),
                            account => account.Username
                        );

        Assert.That(usernames.OrderBy(x => x).ToArray(), Is.EqualTo(ExpectedAccountUsernames));
    }

    [Test]
    public async Task QueryAsync_OnItems_ShouldFilterAndProject()
    {
        using var tempDirectory = new TempDirectory();
        var unitOfWork = CreateUnitOfWork(tempDirectory.Path);
        await unitOfWork.InitializeAsync();

        await unitOfWork.Items.UpsertAsync(new() { Id = (Serial)0x3001, ItemId = 0x0EED });
        await unitOfWork.Items.UpsertAsync(new() { Id = (Serial)0x3002, ItemId = 0x0F3F });

        var coinIds = await unitOfWork.Items.QueryAsync(
                          item => item.ItemId == 0x0EED,
                          item => item.Id
                      );

        Assert.That(coinIds.ToArray(), Is.EqualTo(new[] { (Serial)0x3001 }));
    }

    [Test]
    public async Task QueryAsync_OnMobiles_ShouldReturnOnlyPlayers()
    {
        using var tempDirectory = new TempDirectory();
        var unitOfWork = CreateUnitOfWork(tempDirectory.Path);
        await unitOfWork.InitializeAsync();

        await unitOfWork.Mobiles.UpsertAsync(new() { Id = (Serial)0x2001, IsPlayer = true, IsAlive = true });
        await unitOfWork.Mobiles.UpsertAsync(new() { Id = (Serial)0x2002, IsPlayer = false, IsAlive = true });

        var playerIds = await unitOfWork.Mobiles.QueryAsync(
                            mobile => mobile.IsPlayer,
                            mobile => mobile.Id
                        );

        Assert.That(playerIds.ToArray(), Is.EqualTo(new[] { (Serial)0x2001 }));
    }

    [Test]
    public async Task SaveCapturedSnapshotAsync_ShouldPreserveJournalEntriesWrittenAfterCapture()
    {
        using var tempDirectory = new TempDirectory();
        var unitOfWork = CreateUnitOfWork(tempDirectory.Path);
        await unitOfWork.InitializeAsync();

        await unitOfWork.Accounts.UpsertAsync(
            new()
            {
                Id = (Serial)0x00000050,
                Username = "captured-user",
                PasswordHash = "pw"
            }
        );

        var captured = await unitOfWork.CaptureSnapshotAsync();

        await unitOfWork.Accounts.UpsertAsync(
            new()
            {
                Id = (Serial)0x00000051,
                Username = "after-capture",
                PasswordHash = "pw"
            }
        );

        await unitOfWork.SaveCapturedSnapshotAsync(captured);

        var reloadedUnitOfWork = CreateUnitOfWork(tempDirectory.Path);
        await reloadedUnitOfWork.InitializeAsync();

        var capturedAccount = await reloadedUnitOfWork.Accounts.GetByUsernameAsync("captured-user");
        var afterCaptureAccount = await reloadedUnitOfWork.Accounts.GetByUsernameAsync("after-capture");

        Assert.Multiple(
            () =>
            {
                Assert.That(capturedAccount, Is.Not.Null);
                Assert.That(afterCaptureAccount, Is.Not.Null);
            }
        );
    }

    [Test]
    public async Task SaveSnapshotAsync_ShouldPersistAllEntities()
    {
        SkillInfo.Table =
        [
            new(
                0,
                "Alchemy",
                0,
                0,
                100,
                "Alchemist",
                0,
                0,
                0,
                1,
                "Alchemy",
                Stat.Intelligence,
                Stat.Intelligence
            ),
            new(
                25,
                "Magery",
                0,
                0,
                100,
                "Wizard",
                0,
                0,
                0,
                1,
                "Magery",
                Stat.Intelligence,
                Stat.Intelligence
            )
        ];
        using var tempDirectory = new TempDirectory();
        var unitOfWork = CreateUnitOfWork(tempDirectory.Path);
        await unitOfWork.InitializeAsync();

        await unitOfWork.Accounts.UpsertAsync(
            new()
            {
                Id = (Serial)0x00000002,
                Username = "snapshot-user",
                PasswordHash = "pw"
            }
        );

        await unitOfWork.Mobiles.UpsertAsync(
            new()
            {
                Id = (Serial)0x00000010,
                AccountId = (Serial)0x00000002,
                Name = "snapshot-mobile",
                Title = "the brave",
                MapId = 1,
                Direction = DirectionType.East,
                IsPlayer = true,
                IsAlive = true,
                Gender = GenderType.Female,
                RaceIndex = 1,
                ProfessionId = 2,
                SkinHue = 0x0455,
                HairStyle = 0x0203,
                HairHue = 0x0304,
                FacialHairStyle = 0x0000,
                FacialHairHue = 0x0000,
                BaseStats = new()
                {
                    Strength = 60,
                    Dexterity = 50,
                    Intelligence = 40
                },
                Resources = new()
                {
                    Hits = 60,
                    Mana = 40,
                    Stamina = 50,
                    MaxHits = 60,
                    MaxMana = 40,
                    MaxStamina = 50
                },
                SkillPoints = 8,
                StatPoints = 6,
                StatCap = 250,
                Followers = 2,
                FollowersMax = 5,
                Weight = 33,
                MaxWeight = 400,
                MinWeaponDamage = 11,
                MaxWeaponDamage = 15,
                Tithing = 777,
                BaseResistances = new()
                {
                    Fire = 15,
                    Cold = 11,
                    Poison = 9,
                    Energy = 13
                },
                BaseLuck = 42,
                EquipmentModifiers = new()
                {
                    StrengthBonus = 5,
                    FireResist = 2,
                    Luck = 10,
                    HitChanceIncrease = 8
                },
                RuntimeModifiers = new()
                {
                    StrengthBonus = -1,
                    FireResist = 4,
                    Luck = 20,
                    DefenseChanceIncrease = 7
                },
                ModifierCaps = new()
                {
                    PhysicalResist = 70,
                    FireResist = 71,
                    ColdResist = 72,
                    PoisonResist = 73,
                    EnergyResist = 74,
                    DefenseChanceIncrease = 45
                },
                BackpackId = (Serial)0x40000020,
                EquippedItemIds = new()
                {
                    [ItemLayerType.Shirt] = (Serial)0x40000021,
                    [ItemLayerType.Pants] = (Serial)0x40000022,
                    [ItemLayerType.Shoes] = (Serial)0x40000023
                },
                IsWarMode = false,
                IsHidden = false,
                IsFrozen = false,
                IsPoisoned = false,
                IsBlessed = false,
                MountedMobileId = (Serial)0x00000011,
                MountedDisplayItemId = 0x3E9F,
                Notoriety = Notoriety.Innocent,
                CreatedUtc = new(2026, 2, 19, 12, 0, 0, DateTimeKind.Utc),
                LastLoginUtc = new(2026, 2, 19, 13, 0, 0, DateTimeKind.Utc),
                Location = new(10, 20, 0)
            }
        );
        var persistedMobile = await unitOfWork.Mobiles.GetByIdAsync((Serial)0x00000010);
        Assert.That(persistedMobile, Is.Not.Null);
        persistedMobile!.SetSkill(UOSkillName.Alchemy, 500, cap: 900, lockState: UOSkillLock.Locked);
        persistedMobile.SetSkill(UOSkillName.Magery, 725, 700, 1000, UOSkillLock.Down);
        await unitOfWork.Mobiles.UpsertAsync(persistedMobile);
        await unitOfWork.Mobiles.UpsertAsync(
            new()
            {
                Id = (Serial)0x00000011,
                AccountId = Serial.Zero,
                Name = "snapshot-mount",
                Title = "the horse",
                MapId = 1,
                Location = new(10, 20, 0),
                RiderMobileId = (Serial)0x00000010,
                CreatedUtc = new(2026, 2, 19, 12, 0, 0, DateTimeKind.Utc),
                LastLoginUtc = new(2026, 2, 19, 13, 0, 0, DateTimeKind.Utc)
            }
        );

        await unitOfWork.Items.UpsertAsync(
            new()
            {
                Id = (Serial)0x40000010,
                Name = "Fancy Shirt",
                Weight = 7,
                Amount = 3,
                IsStackable = false,
                Rarity = ItemRarity.Rare,
                ItemId = 0x0EED,
                Hue = 0x0481,
                ScriptId = "items.fancy_shirt",
                Direction = DirectionType.West,
                MapId = 1,
                Location = new(10, 20, 0),
                CombatStats = new()
                {
                    MinStrength = 40,
                    DamageMin = 11,
                    DamageMax = 13,
                    Defense = 15,
                    AttackSpeed = 30,
                    RangeMin = 1,
                    RangeMax = 2,
                    MaxDurability = 45,
                    CurrentDurability = 40
                },
                Modifiers = new()
                {
                    StrengthBonus = 5,
                    PhysicalResist = 12,
                    FireResist = 8,
                    HitChanceIncrease = 10,
                    DefenseChanceIncrease = 7,
                    Luck = 100,
                    SpellChanneling = 1,
                    UsesRemaining = 25
                },
                ParentContainerId = (Serial)0x40000020,
                ContainerPosition = new(42, 84),
                EquippedMobileId = (Serial)0x00000010,
                EquippedLayer = ItemLayerType.Shirt,
                ContainedItemIds = [(Serial)0x40000030, (Serial)0x40000031]
            }
        );
        var persistedItem = await unitOfWork.Items.GetByIdAsync((Serial)0x40000010);
        Assert.That(persistedItem, Is.Not.Null);
        persistedItem!.SetCustomInteger("charges", 7);
        persistedItem.SetCustomBoolean("blessed", true);
        persistedItem.SetCustomDouble("power_scale", 1.5d);
        persistedItem.SetCustomString("sign_text", "Tommy's home");
        await unitOfWork.Items.UpsertAsync(persistedItem);

        await unitOfWork.SaveSnapshotAsync();

        var secondUnitOfWork = CreateUnitOfWork(tempDirectory.Path);
        await secondUnitOfWork.InitializeAsync();

        var loadedAccount = await secondUnitOfWork.Accounts.GetByUsernameAsync("snapshot-user");
        var loadedMobile = await secondUnitOfWork.Mobiles.GetByIdAsync((Serial)0x00000010);
        var loadedItem = await secondUnitOfWork.Items.GetByIdAsync((Serial)0x40000010);

        Assert.Multiple(
            () =>
            {
                Assert.That(loadedAccount, Is.Not.Null);
                Assert.That(loadedMobile, Is.Not.Null);
                Assert.That(loadedItem, Is.Not.Null);

                Assert.That(loadedMobile!.AccountId, Is.EqualTo((Serial)0x00000002));
                Assert.That(loadedMobile.Name, Is.EqualTo("snapshot-mobile"));
                Assert.That(loadedMobile.Title, Is.EqualTo("the brave"));
                Assert.That(loadedMobile.MapId, Is.EqualTo(1));
                Assert.That(loadedMobile.Direction, Is.EqualTo(DirectionType.East));
                Assert.That(loadedMobile.Gender, Is.EqualTo(GenderType.Female));
                Assert.That(loadedMobile.RaceIndex, Is.EqualTo(1));
                Assert.That(loadedMobile.ProfessionId, Is.EqualTo(2));
                Assert.That(loadedMobile.SkinHue, Is.EqualTo(0x0455));
                Assert.That(loadedMobile.HairStyle, Is.EqualTo(0x0203));
                Assert.That(loadedMobile.HairHue, Is.EqualTo(0x0304));
                Assert.That(loadedMobile.BaseStats.Strength, Is.EqualTo(60));
                Assert.That(loadedMobile.BaseStats.Dexterity, Is.EqualTo(50));
                Assert.That(loadedMobile.BaseStats.Intelligence, Is.EqualTo(40));
                Assert.That(loadedMobile.Strength, Is.EqualTo(60));
                Assert.That(loadedMobile.Dexterity, Is.EqualTo(50));
                Assert.That(loadedMobile.Intelligence, Is.EqualTo(40));
                Assert.That(loadedMobile.Resources.Hits, Is.EqualTo(60));
                Assert.That(loadedMobile.Resources.Mana, Is.EqualTo(40));
                Assert.That(loadedMobile.Resources.Stamina, Is.EqualTo(50));
                Assert.That(loadedMobile.Resources.MaxHits, Is.EqualTo(60));
                Assert.That(loadedMobile.Resources.MaxMana, Is.EqualTo(40));
                Assert.That(loadedMobile.Resources.MaxStamina, Is.EqualTo(50));
                Assert.That(loadedMobile.Hits, Is.EqualTo(60));
                Assert.That(loadedMobile.Mana, Is.EqualTo(40));
                Assert.That(loadedMobile.Stamina, Is.EqualTo(50));
                Assert.That(loadedMobile.MaxHits, Is.EqualTo(60));
                Assert.That(loadedMobile.MaxMana, Is.EqualTo(40));
                Assert.That(loadedMobile.MaxStamina, Is.EqualTo(50));
                Assert.That(loadedMobile.SkillPoints, Is.EqualTo(8));
                Assert.That(loadedMobile.StatPoints, Is.EqualTo(6));
                Assert.That(loadedMobile.StatCap, Is.EqualTo(250));
                Assert.That(loadedMobile.Followers, Is.EqualTo(2));
                Assert.That(loadedMobile.FollowersMax, Is.EqualTo(5));
                Assert.That(loadedMobile.Weight, Is.EqualTo(33));
                Assert.That(loadedMobile.MaxWeight, Is.EqualTo(400));
                Assert.That(loadedMobile.MinWeaponDamage, Is.EqualTo(11));
                Assert.That(loadedMobile.MaxWeaponDamage, Is.EqualTo(15));
                Assert.That(loadedMobile.Tithing, Is.EqualTo(777));
                Assert.That(loadedMobile.BaseResistances.Fire, Is.EqualTo(15));
                Assert.That(loadedMobile.BaseResistances.Cold, Is.EqualTo(11));
                Assert.That(loadedMobile.BaseResistances.Poison, Is.EqualTo(9));
                Assert.That(loadedMobile.BaseResistances.Energy, Is.EqualTo(13));
                Assert.That(loadedMobile.FireResistance, Is.EqualTo(15));
                Assert.That(loadedMobile.ColdResistance, Is.EqualTo(11));
                Assert.That(loadedMobile.PoisonResistance, Is.EqualTo(9));
                Assert.That(loadedMobile.EnergyResistance, Is.EqualTo(13));
                Assert.That(loadedMobile.BaseLuck, Is.EqualTo(42));
                Assert.That(loadedMobile.Luck, Is.EqualTo(42));
                Assert.That(loadedMobile.EquipmentModifiers, Is.Not.Null);
                Assert.That(loadedMobile.EquipmentModifiers!.StrengthBonus, Is.EqualTo(5));
                Assert.That(loadedMobile.EquipmentModifiers.FireResist, Is.EqualTo(2));
                Assert.That(loadedMobile.EquipmentModifiers.Luck, Is.EqualTo(10));
                Assert.That(loadedMobile.EquipmentModifiers.HitChanceIncrease, Is.EqualTo(8));
                Assert.That(loadedMobile.RuntimeModifiers, Is.Not.Null);
                Assert.That(loadedMobile.RuntimeModifiers!.StrengthBonus, Is.EqualTo(-1));
                Assert.That(loadedMobile.RuntimeModifiers.FireResist, Is.EqualTo(4));
                Assert.That(loadedMobile.RuntimeModifiers.Luck, Is.EqualTo(20));
                Assert.That(loadedMobile.RuntimeModifiers.DefenseChanceIncrease, Is.EqualTo(7));
                Assert.That(loadedMobile.ModifierCaps.PhysicalResist, Is.EqualTo(70));
                Assert.That(loadedMobile.ModifierCaps.FireResist, Is.EqualTo(71));
                Assert.That(loadedMobile.ModifierCaps.DefenseChanceIncrease, Is.EqualTo(45));
                Assert.That(loadedMobile.EffectiveStrength, Is.EqualTo(64));
                Assert.That(loadedMobile.EffectiveFireResistance, Is.EqualTo(21));
                Assert.That(loadedMobile.EffectiveLuck, Is.EqualTo(72));
                Assert.That(loadedMobile.MountedMobileId, Is.EqualTo((Serial)0x00000011));
                Assert.That(loadedMobile.MountedDisplayItemId, Is.EqualTo(0x3E9F));
                Assert.That(loadedMobile.IsMounted, Is.True);
                Assert.That(loadedMobile.BackpackId, Is.EqualTo((Serial)0x40000020));
                Assert.That(loadedMobile.EquippedItemIds[ItemLayerType.Shirt], Is.EqualTo((Serial)0x40000021));
                Assert.That(loadedMobile.EquippedItemIds[ItemLayerType.Pants], Is.EqualTo((Serial)0x40000022));
                Assert.That(loadedMobile.EquippedItemIds[ItemLayerType.Shoes], Is.EqualTo((Serial)0x40000023));
                Assert.That(loadedMobile.Notoriety, Is.EqualTo(Notoriety.Innocent));
                Assert.That(loadedMobile.CreatedUtc, Is.EqualTo(new DateTime(2026, 2, 19, 12, 0, 0, DateTimeKind.Utc)));
                Assert.That(loadedMobile.LastLoginUtc, Is.EqualTo(new DateTime(2026, 2, 19, 13, 0, 0, DateTimeKind.Utc)));
                Assert.That(loadedMobile.Skills[UOSkillName.Alchemy].Value, Is.EqualTo(500));
                Assert.That(loadedMobile.Skills[UOSkillName.Alchemy].Cap, Is.EqualTo(900));
                Assert.That(loadedMobile.Skills[UOSkillName.Alchemy].Lock, Is.EqualTo(UOSkillLock.Locked));
                Assert.That(loadedMobile.Skills[UOSkillName.Magery].Value, Is.EqualTo(725));
                Assert.That(loadedMobile.Skills[UOSkillName.Magery].Base, Is.EqualTo(700));
                Assert.That(loadedMobile.Skills[UOSkillName.Magery].Lock, Is.EqualTo(UOSkillLock.Down));
                Assert.That(loadedItem!.ParentContainerId, Is.EqualTo((Serial)0x40000020));
                Assert.That(loadedItem.ContainerPosition.X, Is.EqualTo(42));
                Assert.That(loadedItem.ContainerPosition.Y, Is.EqualTo(84));
                Assert.That(loadedItem.Name, Is.EqualTo("Fancy Shirt"));
                Assert.That(loadedItem.Weight, Is.EqualTo(7));
                Assert.That(loadedItem.Amount, Is.EqualTo(3));
                Assert.That(loadedItem.IsStackable, Is.False);
                Assert.That(loadedItem.Rarity, Is.EqualTo(ItemRarity.Rare));
                Assert.That(loadedItem.Hue, Is.EqualTo(0x0481));
                Assert.That(loadedItem.ScriptId, Is.EqualTo("items.fancy_shirt"));
                Assert.That(loadedItem.Direction, Is.EqualTo(DirectionType.West));
                Assert.That(loadedItem.MapId, Is.EqualTo(1));
                Assert.That(loadedItem.EquippedMobileId, Is.EqualTo((Serial)0x00000010));
                Assert.That(loadedItem.EquippedLayer, Is.EqualTo(ItemLayerType.Shirt));
                Assert.That(loadedItem.CombatStats, Is.Not.Null);
                Assert.That(loadedItem.CombatStats!.MinStrength, Is.EqualTo(40));
                Assert.That(loadedItem.CombatStats.DamageMin, Is.EqualTo(11));
                Assert.That(loadedItem.CombatStats.DamageMax, Is.EqualTo(13));
                Assert.That(loadedItem.CombatStats.Defense, Is.EqualTo(15));
                Assert.That(loadedItem.CombatStats.AttackSpeed, Is.EqualTo(30));
                Assert.That(loadedItem.CombatStats.RangeMin, Is.EqualTo(1));
                Assert.That(loadedItem.CombatStats.RangeMax, Is.EqualTo(2));
                Assert.That(loadedItem.CombatStats.MaxDurability, Is.EqualTo(45));
                Assert.That(loadedItem.CombatStats.CurrentDurability, Is.EqualTo(40));
                Assert.That(loadedItem.Modifiers, Is.Not.Null);
                Assert.That(loadedItem.Modifiers!.StrengthBonus, Is.EqualTo(5));
                Assert.That(loadedItem.Modifiers.PhysicalResist, Is.EqualTo(12));
                Assert.That(loadedItem.Modifiers.FireResist, Is.EqualTo(8));
                Assert.That(loadedItem.Modifiers.HitChanceIncrease, Is.EqualTo(10));
                Assert.That(loadedItem.Modifiers.DefenseChanceIncrease, Is.EqualTo(7));
                Assert.That(loadedItem.Modifiers.Luck, Is.EqualTo(100));
                Assert.That(loadedItem.Modifiers.SpellChanneling, Is.EqualTo(1));
                Assert.That(loadedItem.Modifiers.UsesRemaining, Is.EqualTo(25));
                Assert.That(loadedItem.ContainedItemIds, Has.Count.EqualTo(2));
                Assert.That(loadedItem.ContainedItemIds[0], Is.EqualTo((Serial)0x40000030));
                Assert.That(loadedItem.ContainedItemIds[1], Is.EqualTo((Serial)0x40000031));
                Assert.That(loadedItem.TryGetCustomInteger("charges", out var charges), Is.True);
                Assert.That(charges, Is.EqualTo(7));
                Assert.That(loadedItem.TryGetCustomBoolean("blessed", out var blessed), Is.True);
                Assert.That(blessed, Is.True);
                Assert.That(loadedItem.TryGetCustomDouble("power_scale", out var powerScale), Is.True);
                Assert.That(powerScale, Is.EqualTo(1.5d));
                Assert.That(loadedItem.TryGetCustomString("sign_text", out var signText), Is.True);
                Assert.That(signText, Is.EqualTo("Tommy's home"));
            }
        );
    }

    private static PersistenceUnitOfWork CreateUnitOfWork(string directory, bool enableFileLock = false)
    {
        var options = new PersistenceOptions(
            Path.Combine(directory, "world.snapshot.bin"),
            Path.Combine(directory, "world.journal.bin"),
            enableFileLock
        );

        return new(options);
    }

    private static async Task WriteAccountsWithRetryAsync(
        PersistenceUnitOfWork unitOfWork,
        int startIndex,
        int count,
        int maxRetries = 20
    )
    {
        for (var i = 0; i < count; i++)
        {
            var currentIndex = startIndex + i;
            var account = new UOAccountEntity
            {
                Id = (Serial)(uint)(0x00050000 + currentIndex),
                Username = $"multi-uow-{currentIndex}",
                PasswordHash = "pw"
            };

            var retries = 0;

            while (true)
            {
                try
                {
                    await unitOfWork.Accounts.UpsertAsync(account);

                    break;
                }
                catch (IOException) when (retries < maxRetries)
                {
                    retries++;
                    await Task.Delay(5);
                }
            }
        }
    }
}
