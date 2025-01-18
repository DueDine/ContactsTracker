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
        var sheet = Plugin.DataManager.GetExcelSheet<ContentRoulette>();
        return sheet.GetRow(ContentID).Name.ExtractText();
    }

    public static string GetWorldName(ushort worldID)
    {
        var sheet = Plugin.DataManager.GetExcelSheet<World>();
        return sheet.GetRow(worldID).Name.ExtractText();
    }

}
