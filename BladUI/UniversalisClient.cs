using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace BladUI;

/// <summary>
/// Minimal universalis.app market-price client. Read-only public API;
/// prices are cached for 15 minutes per world.
/// </summary>
public static class UniversalisClient
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };
    private static readonly Dictionary<uint, (long Nq, long Hq)> Cache = [];
    private static DateTime cacheTime = DateTime.MinValue;
    private static string? cacheWorld;

    /// <summary>
    /// Minimum current listing prices (NQ, HQ) per item id on the given world.
    /// Items with no data resolve to (0, 0). Call from any thread.
    /// </summary>
    public static async Task<Dictionary<uint, (long Nq, long Hq)>> GetMinPricesAsync(string world, IReadOnlyCollection<uint> itemIds)
    {
        List<uint> missing;
        lock (Cache)
        {
            if (cacheWorld != world || DateTime.UtcNow - cacheTime > TimeSpan.FromMinutes(15))
            {
                Cache.Clear();
                cacheWorld = world;
                cacheTime = DateTime.UtcNow;
            }

            missing = itemIds.Where(id => !Cache.ContainsKey(id)).Distinct().ToList();
        }

        foreach (var chunk in missing.Chunk(100))
        {
            var csv = string.Join(",", chunk);
            var url = $"https://universalis.app/api/v2/{Uri.EscapeDataString(world)}/{csv}?listings=0&entries=0";
            var json = await Http.GetStringAsync(url).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            lock (Cache)
            {
                if (root.TryGetProperty("items", out var items))
                {
                    // Multi-item response: { "items": { "<id>": {...} }, "unresolvedItems": [...] }
                    foreach (var prop in items.EnumerateObject())
                    {
                        if (uint.TryParse(prop.Name, out var id))
                            Cache[id] = ReadPrices(prop.Value);
                    }
                }
                else if (root.TryGetProperty("itemID", out var itemId))
                {
                    // Single-item response is the item object itself.
                    Cache[itemId.GetUInt32()] = ReadPrices(root);
                }

                // Anything the API didn't resolve: cache as no-data so we don't refetch.
                foreach (var id in chunk)
                    Cache.TryAdd(id, (0, 0));
            }
        }

        lock (Cache)
            return itemIds.Distinct().ToDictionary(id => id, id => Cache.GetValueOrDefault(id, (0L, 0L)));
    }

    private static (long Nq, long Hq) ReadPrices(JsonElement item)
    {
        var nq = item.TryGetProperty("minPriceNQ", out var n) ? n.GetInt64() : 0;
        var hq = item.TryGetProperty("minPriceHQ", out var h) ? h.GetInt64() : 0;
        return (nq, hq);
    }
}
