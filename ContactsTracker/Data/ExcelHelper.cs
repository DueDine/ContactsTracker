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
        var data = sheet.GetRow(territoryID).ContentFinderCondition.Value.Name.ExtractText();
        TerritoryNameCache[territoryID] = data;
        return data;
    }

    public static string GetPoppedContentType(uint rouletteID)
    {
        if (rouletteID == 0) return "Normal";
        if (RouletteNameCache.TryGetValue(rouletteID, out var cached))
        {
            return cached;
        }

        var sheet = Plugin.DataManager.GetExcelSheet<ContentRoulette>();
        var data = sheet.GetRow(rouletteID).Name.ExtractText();
        RouletteNameCache[rouletteID] = data;
        return data;
    }

    public static string GetWorldName(ushort worldID)
    {
        if (worldID == 0) return "Unknown";
        if (WorldNameCache.TryGetValue(worldID, out var cached))
        {
            return cached;
        }

        var sheet = Plugin.DataManager.GetExcelSheet<World>();
        var data = sheet.GetRow(worldID).Name.ExtractText();
        WorldNameCache[worldID] = data;
        return data;
    }
}