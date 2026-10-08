namespace Moongate.UoxItemConverter.Data.Internal.Vendors;

/// <summary>
///     The item templates the converter can choose from, three ways.
/// </summary>
/// <param name="ByGraphic">
///     The template ids of each graphic.
/// </param>
/// <param name="GraphicsOfType">
///     The graphics each C# type is sold under in any shop.
/// </param>
/// <param name="ByName">
///     The template ids by the words after their graphic.
/// </param>
internal sealed record ShopItemIndex(
    Dictionary<int, List<string>> ByGraphic,
    Dictionary<string, HashSet<int>> GraphicsOfType,
    Dictionary<string, List<string>> ByName
);
