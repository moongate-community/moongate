using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using Moongate.Core.Geometry;
using Moongate.Server.Ultima.Data.Multis;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Ultima.Io;
using Moongate.Ultima.Multi;
using Serilog;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     Copies every multi of the client files into memory at startup, after <c>IUltimaDataService</c> has pointed the
///     client-file readers at the client directory.
/// </summary>
public class MultiService : IMultiService
{
    private readonly ILogger _logger = Log.ForContext<MultiService>();

    private FrozenDictionary<int, MultiDefinition> _multis = FrozenDictionary<int, MultiDefinition>.Empty;

    public int Count => _multis.Count;

    public Task StartAsync()
    {
        var useUop = Multis.HasUopFile;

        if (!useUop && (Files.GetFilePath("multi.idx") is null || Files.GetFilePath("multi.mul") is null))
        {
            throw new FileNotFoundException(
                "The Ultima path has neither MultiCollection.uop nor multi.idx and multi.mul."
            );
        }

        // Bind the reader to the current client directory; it opens its index once and caches every multi.
        Multis.Reload();
        var multis = new Dictionary<int, MultiDefinition>();

        for (var id = 0; id < Multis.MaximumMultiIndex; id++)
        {
            var list = useUop ? Multis.GetUopComponents(id) : Multis.GetComponents(id);

            if (list.SortedTiles is { Length: > 0 })
            {
                multis.Add(id, ToDefinition(id, list));
            }
        }

        // The copies above are all the server needs, so drop the reader's own.
        Multis.Reload();

        var source = useUop ? "MultiCollection.uop" : "multi.mul";

        if (multis.Count == 0)
        {
            throw new InvalidDataException($"No multi could be read from {source}.");
        }

        _multis = multis.ToFrozenDictionary();
        _logger.Information("Loaded {Count} multis from {Source}", multis.Count, source);

        return Task.CompletedTask;
    }

    public MultiDefinition GetMulti(int id)
    {
        return TryGetMulti(id, out var multi) ? multi : throw new KeyNotFoundException($"No multi has id {id}.");
    }

    public bool TryGetMulti(int id, [NotNullWhen(true)] out MultiDefinition? multi)
    {
        return _multis.TryGetValue(id, out multi);
    }

    private static MultiDefinition ToDefinition(int id, MultiComponentList list)
    {
        var components = new MultiComponent[list.SortedTiles.Length];

        for (var i = 0; i < components.Length; i++)
        {
            var tile = list.SortedTiles[i];
            components[i] = new()
            {
                ItemId = tile.ItemId,
                Offset = new(tile.OffsetX, tile.OffsetY, tile.OffsetZ),
                Visible = tile.Flags != 0
            };
        }

        return new()
        {
            Id = id,
            Min = new(components.Min(component => component.Offset.X), components.Min(component => component.Offset.Y)),
            Max = new(components.Max(component => component.Offset.X), components.Max(component => component.Offset.Y)),
            Height = components.Max(component => component.Offset.Z),
            Components = components
        };
    }

    public Task StopAsync()
    {
        _multis = FrozenDictionary<int, MultiDefinition>.Empty;

        return Task.CompletedTask;
    }
}
