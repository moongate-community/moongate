using Moongate.Core.Geometry;
using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Ultima.Types;

namespace Moongate.Tests.TestSupport.Ultima.Mobiles;

/// <summary>
///     Records spawns and removals: a spawn returns <see cref="Spawned" /> (or throws <see cref="SpawnFailure" />), a
///     removal returns <see cref="Removes" />.
/// </summary>
public sealed class StubNpcService : INpcService
{
    public MobileEntity Spawned { get; set; } = new() { Id = new(0x00000100), Name = "Orc", TemplateId = "orc" };

    public Exception? SpawnFailure { get; set; }

    public bool Removes { get; set; } = true;

    public List<(string TemplateId, MapType Map, Point3D Location)> Spawns { get; } = [];

    public List<Serial> Removals { get; } = [];

    /// <summary>
    ///     Gets or sets how to tell the game loop thread: on it, a spawn or a removal throws as the real service does,
    ///     which posts to the loop and waits.
    /// </summary>
    public Func<bool>? OnLoopThread { get; set; }

    /// <summary>
    ///     Gets or sets what each spawn waits for before it completes; null means it completes at once.
    /// </summary>
    public TaskCompletionSource? Gate { get; set; }

    /// <summary>
    ///     Completes when the first spawn has been asked for, so a test can wait for a spawn that runs on another thread.
    /// </summary>
    public TaskCompletionSource FirstSpawn { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task StartAsync()
    {
        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        return Task.CompletedTask;
    }

    public async Task<MobileEntity> SpawnAsync(
        string templateId,
        MapType map,
        Point3D location,
        IReadOnlyDictionary<string, object?>? props = null,
        CancellationToken cancellationToken = default
    )
    {
        RefuseTheLoopThread();
        Spawns.Add((templateId, map, location));
        FirstSpawn.TrySetResult();
        Spawned.Map = map;
        Spawned.Location = location;

        foreach (var (key, value) in props ?? new Dictionary<string, object?>())
        {
            Spawned.SetProp(key, value);
        }

        if (Gate is not null)
        {
            await Gate.Task.WaitAsync(cancellationToken);
        }

        return SpawnFailure is null ? Spawned : throw SpawnFailure;
    }

    public Task<bool> RemoveAsync(Serial serial, CancellationToken cancellationToken = default)
    {
        RefuseTheLoopThread();
        Removals.Add(serial);

        return Task.FromResult(Removes);
    }

    private void RefuseTheLoopThread()
    {
        if (OnLoopThread?.Invoke() == true)
        {
            throw new InvalidOperationException("The game loop thread cannot wait for work posted to itself.");
        }
    }
}
