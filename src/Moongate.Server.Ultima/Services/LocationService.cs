using Moongate.Server.Ultima.Data.Locations;
using Moongate.Server.Ultima.Interfaces;
using Moongate.Server.Ultima.Interfaces.Loaders;
using Moongate.Ultima.Types;
using Serilog;

namespace Moongate.Server.Ultima.Services;

/// <summary>
///     Keeps the named places of the maps the server loads as a tree of maps and categories, and finds them by name.
/// </summary>
public sealed class LocationService : ILocationService
{
    private const char Separator = '/';

    private readonly IDataLoaderService _data;
    private readonly ISectorService _sectors;
    private readonly IMapService? _maps;
    private readonly ILogger _logger = Log.ForContext<LocationService>();

    // By path in lower case; the lists are those the nodes show.
    private Dictionary<string, (LocationNode Node, List<string> Categories, List<NamedLocation> Locations)>? _nodes;
    private List<(NamedLocation Place, string Label)>? _places;

    public LocationService(IDataLoaderService data, ISectorService sectors, IMapService? maps = null)
    {
        _maps = maps;
        _data = data;
        _sectors = sectors;
    }

    public LocationNode? GetNode(string path)
    {
        Build();

        return _nodes!.TryGetValue(Key(path ?? ""), out var entry) ? entry.Node : null;
    }

    public IReadOnlyList<NamedLocation> Find(string text, MapType own)
    {
        Build();
        var wanted = Words(text ?? "");

        if (wanted.Length == 0)
        {
            return [];
        }

        var found = _places!.Where(entry => EndsWithWords(entry.Label, wanted)).Select(entry => entry.Place).ToList();

        if (found.Count == 0)
        {
            // No place is named so: a category is, and its first place stands for it.
            foreach (var (key, entry) in _nodes!)
            {
                var slash = key.IndexOf(Separator);

                if (slash >= 0 &&
                    EndsWithWords(Words(key[(slash + 1)..].Replace(Separator, ' ')), wanted) &&
                    FirstPlace(entry.Node) is { } place)
                {
                    found.Add(place);
                }
            }
        }

        var here = found.Where(place => place.Map == own).ToList();

        return here.Count > 0 ? here : found;
    }

    private void Build()
    {
        if (_nodes is not null)
        {
            return;
        }

        var nodes = new Dictionary<string, (LocationNode Node, List<string> Categories, List<NamedLocation> Locations)>();
        var places = new List<(NamedLocation Place, string Label)>();
        Add(nodes, "", "");

        foreach (var place in _data.GetEntities<NamedLocation>())
        {
            // A map whose files are not open cannot be travelled to.
            if (_maps is not null && !_maps.Maps.Contains(place.Map))
            {
                continue;
            }

            if (!_sectors.IsInside(place.Map, place.Location.X, place.Location.Y))
            {
                // One line per map would hide the place that is really wrong on a loaded one.
                _logger.Debug(
                    "Place {Name} of {Map} at {Location} is outside the loaded maps and is left out",
                    place.Name,
                    place.Map,
                    place.Location
                );

                continue;
            }

            var path = place.Map.ToString();
            var parent = "";

            foreach (var part in (path + Separator + place.Category).Split(Separator, StringSplitOptions.RemoveEmptyEntries))
            {
                var name = part.Trim();
                var child = parent.Length == 0 ? name : parent + Separator + name;

                if (!nodes.ContainsKey(Key(child)))
                {
                    Add(nodes, nodes[Key(parent)].Node.Path is { Length: > 0 } above ? above + Separator + name : name, name);
                    nodes[Key(parent)].Categories.Add(name);
                }

                parent = child;
            }

            nodes[Key(parent)].Locations.Add(place);
            places.Add((place, Words(place.Category.Replace(Separator, ' ') + " " + place.Name)));
        }

        _places = places;
        _nodes = nodes;
    }

    private static void Add(
        Dictionary<string, (LocationNode Node, List<string> Categories, List<NamedLocation> Locations)> nodes,
        string path,
        string name
    )
    {
        var categories = new List<string>();
        var locations = new List<NamedLocation>();
        nodes[Key(path)] = (new() { Path = path, Name = name, Categories = categories, Locations = locations }, categories, locations);
    }

    // The first place of a category: one of its own, else the first of the categories in it.
    private NamedLocation? FirstPlace(LocationNode node)
    {
        if (node.Locations.Count > 0)
        {
            return node.Locations[0];
        }

        foreach (var category in node.Categories)
        {
            if (FirstPlace(_nodes![Key(node.Path + Separator + category)].Node) is { } place)
            {
                return place;
            }
        }

        return null;
    }

    private static string Key(string path)
    {
        return string.Join(Separator, path.Split(Separator, StringSplitOptions.RemoveEmptyEntries).Select(part => part.Trim()))
                     .ToLowerInvariant();
    }

    // Lower case, one space between words.
    private static string Words(string text)
    {
        return string.Join(' ', text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                     .ToLowerInvariant();
    }

    private static bool EndsWithWords(string label, string wanted)
    {
        return label.EndsWith(wanted, StringComparison.Ordinal) &&
               (label.Length == wanted.Length || label[label.Length - wanted.Length - 1] == ' ');
    }
}
