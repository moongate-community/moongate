using Moongate.Persistence.Interfaces;
using Moongate.Server.Ultima.Entities.World;
using Moongate.Server.Ultima.Interfaces;
using Serilog;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     Keeps the one row of <c>world.state</c> live: read at startup, changed by scripts on the game loop, written by
///     the world save like the mobiles and the items.
/// </summary>
public sealed class WorldPropsService : IWorldPropsService
{
    private readonly ILogger _logger = Log.ForContext<WorldPropsService>();
    private readonly IDataAccess<WorldStateEntity> _data;

    public WorldStateEntity State { get; private set; } = new();

    public WorldPropsService(IDataAccess<WorldStateEntity> data)
    {
        _data = data;
    }

    public async Task StartAsync()
    {
        // A new world has no row yet: the first save writes it.
        State = await _data.GetByIdAsync(WorldStateEntity.RowId) ?? new WorldStateEntity();
        _logger.Information("Loaded {Count} world props", State.Props?.Count ?? 0);
    }

    public Task StopAsync()
    {
        return Task.CompletedTask;
    }

    public object? Get(string key)
    {
        return State.Props is { } props && props.TryGetValue(key, out var value) ? value : null;
    }

    public void Set(string key, object? value)
    {
        if (value is null)
        {
            State.RemoveProp(key);

            return;
        }

        State.SetProp(key, value);
    }
}
