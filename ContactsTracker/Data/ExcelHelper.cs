using Lumina.Excel.Sheets;
using System.Collections.Generic;

namespace ContactsTracker.Data;

public static class ExcelHelper
{
    private static readonly Dictionary<ushort, string> TerritoryNameCache = [];
    private static readonly Dictionary<uint, string> RouletteNameCache = [];
    private static readonly Dictionary<ushort, string> WorldNameCache = [];

    public static void ClearCache()
    {
        TerritoryNameCache.Clear();
        RouletteNameCache.Clear();
        WorldNameCache.Clear();
    }

    public static string GetTerritoryName(ushort territoryID)
    {
        if (territoryID == 0) return "Unknown";
        if (TerritoryNameCache.TryGetValue(territoryID, out var cached))
        {
            return cached;
        }

        var sheet = Plugin.DataManager.GetExcelSheet<TerritoryType>();
        if (sheet != null && sheet.TryGetRow(territoryID, out var row))
        {
            var data = row.ContentFinderCondition.Value.Name.ExtractText();
            if (!string.IsNullOrWhiteSpace(data))
            {
                TerritoryNameCache[territoryID] = data;
                return data;
            }
        }

        return string.Empty;
    }

    public static string GetPoppedContentType(uint rouletteID)
    {
        if (rouletteID == 0) return "Normal";
        if (RouletteNameCache.TryGetValue(rouletteID, out var cached))
        {
            return cached;
        }

        var sheet = Plugin.DataManager.GetExcelSheet<ContentRoulette>();
        if (sheet != null && sheet.TryGetRow(rouletteID, out var row))
        {
            var data = row.Name.ExtractText();
            if (!string.IsNullOrWhiteSpace(data))
            {
                RouletteNameCache[rouletteID] = data;
                return data;
            }
        }

        return string.Empty;
    }

    public static string GetWorldName(ushort worldID)
    {
        if (worldID == 0) return "Unknown";
        if (WorldNameCache.TryGetValue(worldID, out var cached))
        {
            return cached;
        }

        var sheet = Plugin.DataManager.GetExcelSheet<World>();
        if (sheet != null && sheet.TryGetRow(worldID, out var row))
        {
            var data = row.Name.ExtractText();
            if (!string.IsNullOrWhiteSpace(data))
            {
                WorldNameCache[worldID] = data;
                return data;
            }
        }

        return string.Empty;
    }
}
