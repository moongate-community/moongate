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
    // The words of each place, part by part: its categories, then its name.
    private List<(NamedLocation Place, string[] Parts)>? _places;

    // The categories below the maps, in file order, with their words part by part.
    private List<(LocationNode Node, string[] Parts)>? _categories;

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
        // A path written as the gump shows it, "dungeons/covetous", is its words.
        var wanted = Words((text ?? "").Replace(Separator, ' '));

        if (wanted.Length == 0)
        {
            return [];
        }

        // What is named exactly so comes first: a place, else a category, whose first place stands for it.
        var named = _places!.Where(entry => EndsWithParts(entry.Parts, wanted)).Select(entry => entry.Place).ToList();
        var filed = new List<NamedLocation>();

        foreach (var (node, parts) in _categories!)
        {
            if (EndsWithParts(parts, wanted) && FirstPlace(node) is { } place)
            {
                filed.Add(place);
            }
        }

        // The own map wins, even with a category against a place of another map.
        foreach (var found in new[] { Here(named, own), Here(filed, own), named, filed })
        {
            if (found.Count > 0)
            {
                return found;
            }
        }

        // Nothing is named so: the last words of a name, such as "haven" for "Old Haven".
        var ending = _places.Where(entry => EndsWithWords(string.Join(' ', entry.Parts), wanted))
                            .Select(entry => entry.Place)
                            .ToList();
        var here = Here(ending, own);

        return here.Count > 0 ? here : ending;
    }

    private static List<NamedLocation> Here(List<NamedLocation> places, MapType own)
    {
        return places.Where(place => place.Map == own).ToList();
    }

    private void Build()
    {
        if (_nodes is not null)
        {
            return;
        }

        var nodes = new Dictionary<string, (LocationNode Node, List<string> Categories, List<NamedLocation> Locations)>();
        var places = new List<(NamedLocation Place, string[] Parts)>();
        var categories = new List<(LocationNode Node, string[] Parts)>();
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

                    if (parent.Length > 0)
                    {
                        categories.Add((nodes[Key(child)].Node, Parts(child[(child.IndexOf(Separator) + 1)..])));
                    }
                }

                parent = child;
            }

            nodes[Key(parent)].Locations.Add(place);
            places.Add((place, [.. Parts(place.Category), Words(place.Name)]));
        }

        _places = places;
        _categories = categories;
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

    private static string[] Parts(string category)
    {
        return category.Split(Separator, StringSplitOptions.RemoveEmptyEntries).Select(Words).ToArray();
    }

    // Whether the last parts, whole, are the wanted words: "covetous entrance" for Dungeons, Covetous, Entrance.
    private static bool EndsWithParts(string[] parts, string wanted)
    {
        for (var start = parts.Length - 1; start >= 0; start--)
        {
            if (string.Join(' ', parts[start..]) == wanted)
            {
                return true;
            }
        }

        return false;
    }

    private static bool EndsWithWords(string label, string wanted)
    {
        return label.EndsWith(wanted, StringComparison.Ordinal) &&
               (label.Length == wanted.Length || label[label.Length - wanted.Length - 1] == ' ');
    }
}
