using Moongate.Server.Core.Data.Persistence;
using Moongate.Server.Core.Interfaces.Services;
using Moongate.Server.Ultima.Interfaces;

namespace Moongate.Tests.TestSupport.Admin;

/// <summary>
///     Answers a broadcast with the number of recipients it is told to, and records the texts.
/// </summary>
internal sealed class FakeBroadcastService : IBroadcastService
{
    public List<string> Texts { get; } = [];

    public int Recipients { get; set; } = 3;

    public Exception? Failure { get; set; }

    public Task<int> BroadcastAsync(string text, CancellationToken cancellationToken = default)
    {
        if (Failure is not null)
        {
            throw Failure;
        }

        Texts.Add(text);

        return Task.FromResult(Recipients);
    }
}

/// <summary>
///     Counts the saves; waits <see cref="Delay" /> inside each.
/// </summary>
internal sealed class FakeWorldSaveService : IWorldSaveService
{
    public int Saves { get; private set; }

    public TimeSpan Delay { get; set; }

    public Exception? Failure { get; set; }

    public Task StartAsync()
    {
        return Task.CompletedTask;
    }

    Task IMoongateStartupService.StopAsync()
    {
        return Task.CompletedTask;
    }

    public void Activate()
    {
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        if (Failure is not null)
        {
            throw Failure;
        }

        await Task.Delay(Delay, cancellationToken);
        Saves++;
    }

    public Task StopAsync(bool saveFinal)
    {
        return Task.CompletedTask;
    }
}

/// <summary>
///     Answers a backup with the result it is given.
/// </summary>
internal sealed class FakeSqlBackupService : ISqlBackupService
{
    public SqlBackupResult Result { get; set; } = new();

    public int Backups { get; private set; }

    public TimeSpan Delay { get; set; }

    public bool Completed { get; private set; }

    public bool SawCancellation { get; private set; }

    public Task StartAsync()
    {
        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        return Task.CompletedTask;
    }

    public async Task<SqlBackupResult> BackupAsync(CancellationToken cancellationToken = default)
    {
        Backups++;
        SawCancellation = cancellationToken.CanBeCanceled;
        await Task.Delay(Delay, CancellationToken.None);
        Completed = true;

        return Result;
    }
}
