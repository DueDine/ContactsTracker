using Lumina.Excel.Sheets;

namespace ContactsTracker.Data;

public static class ExcelHelper
{
    public static string GetTerritoryName(ushort territoryID)
    {
        var sheet = Plugin.DataManager.GetExcelSheet<TerritoryType>();
        return sheet.GetRow(territoryID).ContentFinderCondition.Value.Name.ExtractText();
    }

    public static string GetPoppedContentType(uint ContentID)
    {
        if (ContentID == 0) return "Normal";
        var sheet = Plugin.DataManager.GetExcelSheet<ContentRoulette>();
        return sheet.GetRow(ContentID).Name.ExtractText();
    }

    public static string GetWorldName(ushort worldID)
    {
        var sheet = Plugin.DataManager.GetExcelSheet<World>();
        return sheet.GetRow(worldID).Name.ExtractText();
    }

    public static string GetRouletteName(uint rouletteID)
    {
        var sheet = Plugin.DataManager.GetExcelSheet<ContentRoulette>();
        return sheet.GetRow(rouletteID).Name.ExtractText();
    }
}
