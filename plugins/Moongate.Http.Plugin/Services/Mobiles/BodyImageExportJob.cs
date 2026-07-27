using Moongate.Http.Plugin.Data;
using Moongate.Http.Plugin.Interfaces.Mobiles;
using Moongate.Http.Plugin.Types;
using Moongate.Ultima.Types;
using Serilog;

namespace Moongate.Http.Plugin.Services.Mobiles;

/// <summary>
/// Warms the body image cache by walking every classified, non-equipment body. It goes through
/// <see cref="IBodyImageService" /> like any request, so it takes the same gate per body and
/// interleaves with live traffic rather than blocking it for the length of the run.
/// </summary>
public sealed class BodyImageExportJob : IBodyImageExportJob
{
    private readonly ILogger _logger = Log.ForContext<BodyImageExportJob>();
    private readonly IBodyImageService _images;
    private readonly IAnimationCatalog _catalog;
    private readonly object _sync = new();

    private BodyImageExportStateType _state = BodyImageExportStateType.Idle;
    private int _done;
    private int _total;
    private int _failed;
    private DateTimeOffset? _startedAt;

    public BodyImageExportJob(IBodyImageService images, IAnimationCatalog catalog)
    {
        _images = images;
        _catalog = catalog;
    }

    public BodyImageExportStatus Status
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
            if (_state == BodyImageExportStateType.Running)
            {
                return false;
            }

            _state = BodyImageExportStateType.Running;
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
            var bodies = _catalog.ClassifiedBodies
                .Where(entry => entry.Type != MobType.Equipment)
                .Select(entry => entry.Body)
                .ToArray();

            lock (_sync)
            {
                _total = bodies.Length;
            }

            foreach (var body in bodies)
            {
                try
                {
                    // Null means "no usable animation": counted as failed so the status adds up, but it
                    // must not end the run — the point is to warm what can be warmed.
                    if (await _images.GetOrCreateAsync(body, 0) is null)
                    {
                        lock (_sync)
                        {
                            _failed++;
                        }
                    }
                }
                catch (Exception exception)
                {
                    _logger.Warning(exception, "Could not export the image for body {Body}", body);

                    lock (_sync)
                    {
                        _failed++;
                    }
                }

                lock (_sync)
                {
                    _done++;
                }
            }

            lock (_sync)
            {
                _state = BodyImageExportStateType.Completed;
            }

            _logger.Information("Body image export finished: {Done} processed, {Failed} failed", _done, _failed);
        }
        catch (Exception exception)
        {
            _logger.Error(exception, "Body image export stopped early");

            lock (_sync)
            {
                _state = BodyImageExportStateType.Failed;
            }
        }
    }
}
