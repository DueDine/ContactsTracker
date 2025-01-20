using ContactsTracker.Data;
using Lumina.Excel.Sheets;
using System;
using System.Linq;

namespace ContactsTracker.Migration;

public class EntryV1ToV2
{
    public static bool Migrate()
    {
        var RouletteExcel = Plugin.DataManager.GetExcelSheet<ContentRoulette>();
        var ContentConditionExcel = Plugin.DataManager.GetExcelSheet<ContentFinderCondition>();
        var TerritoryExcel = Plugin.DataManager.GetExcelSheet<TerritoryType>();

        var oldEntries = Database.Entries;

        try
        {
            DatabaseV2.Load();

            foreach (var entry in oldEntries)
            {
                if (entry.TerritoryName == null) continue;

                var rouletteId = RouletteExcel.Where(roulette => roulette.Name.ExtractText() == entry.RouletteType)
                                            .FirstOrDefault().RowId;

                var territoryName = entry.TerritoryName.StartsWith("The") ? "the" + entry.TerritoryName[3..] : entry.TerritoryName;
                var contentRow = ContentConditionExcel.Where(content => content.Name.ExtractText() == territoryName)
                                                    .FirstOrDefault();
                var territoryId = TerritoryExcel.Where(territory => territory.ContentFinderCondition.RowId == contentRow.RowId)
                                                .FirstOrDefault().RowId;
                if (territoryId == 0) continue;

                DataEntryV2.Initialize((ushort)territoryId, rouletteId);
                DataEntryV2.Instance!.IsCompleted = entry.IsCompleted;
                var startTime = DateTime.Parse(entry.Date) + TimeSpan.Parse(entry.beginAt);
                var endTime = entry.endAt != null ? DateTime.Parse(entry.Date) + TimeSpan.Parse(entry.endAt) : DateTime.MinValue;
                if (endTime != DateTime.MinValue && endTime < startTime)
                    endTime += TimeSpan.FromDays(1);
                DataEntryV2.Instance!.BeginAt = startTime;
                DataEntryV2.Instance!.EndAt = endTime;
                DataEntryV2.Instance!.PlayerJobAbbr = entry.jobName ?? string.Empty;

                if (entry.partyMembers != null)
                {
                    var members = entry.partyMembers.Split('|');
                    if (members.Length > 1)
                        members = [.. members.Take(members.Length - 1).Select(m => m.Trim())];
                    DataEntryV2.Instance!.PartyMembers = members;
                }
                DatabaseV2.InsertEntry(DataEntryV2.Instance);
            }
            Plugin.Logger.Information($"Migrated {oldEntries.Count} entries");
            return true;
        }
        catch (Exception e)
        {
            Plugin.Logger.Error($"Error migrating entry: {e.Message}");
            return false;
        }
    }
}