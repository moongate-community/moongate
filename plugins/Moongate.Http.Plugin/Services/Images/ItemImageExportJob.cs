using Moongate.Http.Plugin.Data;
using Moongate.Http.Plugin.Interfaces.Images;
using Moongate.Http.Plugin.Types;
using Serilog;

namespace Moongate.Http.Plugin.Services.Images;

/// <summary>
/// Warms the item image cache by walking every item id that has art. It goes through
/// <see cref="IItemImageService" /> like any request, so it takes the same gate per item and interleaves
/// with live traffic rather than blocking it for the length of the run.
/// </summary>
public sealed class ItemImageExportJob : IItemImageExportJob
{
    private readonly ILogger _logger = Log.ForContext<ItemImageExportJob>();
    private readonly IItemImageService _images;
    private readonly object _sync = new();

    private ItemImageExportStateType _state = ItemImageExportStateType.Idle;
    private int _done;
    private int _total;
    private int _failed;
    private DateTimeOffset? _startedAt;

    public ItemImageExportJob(IItemImageService images)
    {
        _images = images;
    }

    public ItemImageExportStatus Status
    {
        get
        {
            lock (_sync)
            {
                return new(_state.ToString(), _done, _total, _failed, _startedAt);
            }
        }
    }

    public bool TryStart()
    {
        lock (_sync)
        {
            if (_state == ItemImageExportStateType.Running)
            {
                return false;
            }

            _state = ItemImageExportStateType.Running;
            _done = 0;
            _total = 0;
            _failed = 0;
            _startedAt = DateTimeOffset.UtcNow;
        }

        // Deliberately not awaited: the route answers 202 and the caller polls. The task owns its own
        // failures — RunAsync catches everything and records it in the state, so nothing is left to an
        // unobserved exception.
        _ = Task.Run(RunAsync);

        return true;
    }

    private async Task RunAsync()
    {
        try
        {
            var ids = await _images.GetArtItemIdsAsync();

            lock (_sync)
            {
                _total = ids.Count;
            }

            foreach (var id in ids)
            {
                try
                {
                    await _images.GetOrCreateAsync(id, 0);

                    lock (_sync)
                    {
                        _done++;
                    }
                }
                catch (Exception exception)
                {
                    // One unreadable tile must not end the run: the point is to warm what can be warmed.
                    _logger.Warning(exception, "Could not export art for item 0x{ItemId:x4}", id);

                    lock (_sync)
                    {
                        _failed++;
                    }
                }
            }

            lock (_sync)
            {
                _state = ItemImageExportStateType.Completed;
            }

            _logger.Information(
                "Item image export finished: {Done} exported, {Failed} failed",
                _done,
                _failed
            );
        }
        catch (Exception exception)
        {
            _logger.Error(exception, "Item image export stopped early");

            lock (_sync)
            {
                _state = ItemImageExportStateType.Failed;
            }
        }
    }
}
