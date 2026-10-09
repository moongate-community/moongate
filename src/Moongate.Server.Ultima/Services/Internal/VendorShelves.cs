using Moongate.Core.Primitives;
using Moongate.Server.Ultima.Data.Internal.Vendors;
using Moongate.Server.Ultima.Data.Templates.Shops;

namespace Moongate.Server.Ultima.Services.Internal;

/// <summary>
///     What the vendors have on their shelves, in memory, as ModernUO's: a counter for each line of a shop, filled to its
///     maximum at a restock that comes at most once an hour, when a window opens; and the goods players sold, offered
///     again for an hour. A restart fills every shelf again.
/// </summary>
internal sealed class VendorShelves
{
    private const int MaxCeiling = 999;
    private const int HalvedCeiling = 640;
    private const int HalvingFloor = 20;
    private const int PruneAbove = 1024;
    private const int ResaleLinesMaximum = 250;

    public static readonly TimeSpan RestockEvery = TimeSpan.FromHours(1);

    public static readonly TimeSpan ResaleFor = TimeSpan.FromHours(1);

    private readonly Dictionary<(Serial Vendor, string Line), StockLine> _stock = new();
    private readonly Dictionary<Serial, DateTime> _restocked = new();
    private readonly Dictionary<Serial, List<ResaleLine>> _resale = new();

    /// <summary>
    ///     Gets the shelf of a line, filled to the amount of the line the first time it is asked for.
    /// </summary>
    public StockLine Of(Serial vendor, ShopLine line)
    {
        var key = (vendor, $"{line.Item}|{line.Price}|{line.Hue}");

        if (!_stock.TryGetValue(key, out var stock))
        {
            _stock[key] = stock = new() { Current = line.Amount, Max = line.Amount };
        }

        return stock;
    }

    /// <summary>
    ///     Restocks the shelves of a vendor when more than an hour passed since the last time; the first time a vendor
    ///     is seen only starts the clock.
    /// </summary>
    public void Restock(Serial vendor, DateTime now, IEnumerable<ShopLine> lines)
    {
        if (!_restocked.TryGetValue(vendor, out var last))
        {
            _restocked[vendor] = now;

            return;
        }

        if (now - last <= RestockEvery)
        {
            return;
        }

        _restocked[vendor] = now;

        foreach (var line in lines)
        {
            Restock(Of(vendor, line));
        }
    }

    /// <summary>
    ///     Adds goods a player sold to the resale shelf of a vendor, joining the same goods at the same price.
    /// </summary>
    public void AddResale(Serial vendor, ShopLine line, int amount, DateTime now)
    {
        if (!_resale.TryGetValue(vendor, out var lines))
        {
            _resale[vendor] = lines = [];
        }

        Forget(lines, now);

        var same = lines.FirstOrDefault(other => other.Line.Item == line.Item &&
                                                 other.Line.Price == line.Price &&
                                                 other.Line.Hue == line.Hue
        );

        if (same is null)
        {
            // A vendor remembers 250 kinds of goods at most: the oldest are forgotten for the new.
            while (lines.Count >= ResaleLinesMaximum)
            {
                lines[0].Stock.Current = 0;
                lines.RemoveAt(0);
            }

            lines.Add(new() { Line = line, Stock = new() { Current = amount, Max = amount }, ExpiresAt = now + ResaleFor });

            return;
        }

        same.Stock.Current += amount;
        same.Stock.Max = same.Stock.Current;
        same.ExpiresAt = now + ResaleFor;
    }

    /// <summary>
    ///     Gets what is still for sale on the resale shelf of a vendor, forgetting the rest.
    /// </summary>
    public IReadOnlyList<ResaleLine> ResaleOf(Serial vendor, DateTime now)
    {
        if (!_resale.TryGetValue(vendor, out var lines))
        {
            return [];
        }

        Forget(lines, now);

        return lines;
    }

    /// <summary>
    ///     Forgets the shelves of vendors that are gone, such as the ones that respawned with another serial.
    /// </summary>
    public void Prune(Func<Serial, bool> isInWorld)
    {
        if (_stock.Count <= PruneAbove)
        {
            return;
        }

        foreach (var key in _stock.Keys.Where(key => !isInWorld(key.Vendor)).ToArray())
        {
            _stock.Remove(key);
        }

        foreach (var vendor in _restocked.Keys.Concat(_resale.Keys).Where(vendor => !isInWorld(vendor)).ToArray())
        {
            _restocked.Remove(vendor);
            _resale.Remove(vendor);
        }
    }

    // Goods past their hour are gone, and their shelf is emptied so that a window still showing them cannot buy them.
    private static void Forget(List<ResaleLine> lines, DateTime now)
    {
        foreach (var line in lines.Where(line => line.ExpiresAt <= now))
        {
            line.Stock.Current = 0;
        }

        lines.RemoveAll(line => line.Stock.Current <= 0);
    }

    // A shelf that sold out stocks twice as much, up to 999. A shelf of more than 20 that still has half of its maximum
    // or more (it sold half or less) stocks half as much, and 999 stocks 640; one that sold more than half, or one of 20
    // or less, keeps its maximum. The shelf is then full.
    private static void Restock(StockLine stock)
    {
        if (stock.Current <= 0)
        {
            stock.Max = Math.Min(MaxCeiling, stock.Max * 2);
        }
        else
        {
            var half = stock.Max;

            if (half >= MaxCeiling)
            {
                half = HalvedCeiling;
            }
            else if (half > HalvingFloor)
            {
                half /= 2;
            }

            if (stock.Current >= half)
            {
                stock.Max = half;
            }
        }

        stock.Current = stock.Max;
    }
}
