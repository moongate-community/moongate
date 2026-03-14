using Moongate.Persistence.Data.Persistence;
using Moongate.Persistence.Interfaces.Persistence;
using Moongate.Persistence.Services.Persistence;
using Moongate.Persistence.Types;
using Moongate.Tests.TestSupport;

namespace Moongate.Tests.Persistence;

public sealed class BinaryJournalServiceTests
{
    [Test]
    public async Task TrimThroughSequenceAsync_ShouldRetainEntriesAfterInclusiveCutoff()
    {
        using var tempDirectory = new TempDirectory();
        using var journal = new BinaryJournalService(Path.Combine(tempDirectory.Path, "world.journal.bin"), false);

        await journal.AppendAsync(CreateEntry(1));
        await journal.AppendAsync(CreateEntry(2));
        await journal.AppendAsync(CreateEntry(3));
        await journal.AppendAsync(CreateEntry(4));

        await journal.TrimThroughSequenceAsync(2);

        var entries = await journal.ReadAllAsync();

        Assert.That(entries.Select(static x => x.SequenceId), Is.EqualTo(new long[] { 3, 4 }));
    }

    private static JournalEntry CreateEntry(long sequenceId)
        => new()
        {
            SequenceId = sequenceId,
            TimestampUnixMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            OperationType = PersistenceOperationType.UpsertItem,
            Payload = [1, 2, 3]
        };
}
