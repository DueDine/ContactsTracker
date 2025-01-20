using CsvHelper;
using CsvHelper.Configuration;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace ContactsTracker.Data;

public class DatabaseV2
{
    public static int Version { get; set; } = 2;
    private static readonly string DataPath = Path.Combine(Plugin.PluginInterface.ConfigDirectory.FullName, $"data_v{Version}.json");
    private static readonly string TempPath = Path.Combine(Plugin.PluginInterface.ConfigDirectory.FullName, $"temp_v{Version}.json");
    public static bool isDirty = false;

    public static List<DataEntryV2> Entries { get; private set; } = [];

    public static void InsertEntry(DataEntryV2 entry)
    {
        Entries.Add(entry);
        Save();
    }

    public static void Save() => File.WriteAllText(DataPath, JsonConvert.SerializeObject(Entries));

    public static void Load()
    {
        if (!File.Exists(DataPath))
        {
            Save();
        }

        var content = JsonConvert.DeserializeObject<List<DataEntryV2>>(File.ReadAllText(DataPath));

        if (content != null)
        {
            Entries = content;
        }
        else
        {
            Plugin.Logger.Error("Failed to load data!");
        }
    }

    public static DataEntryV2? LoadInProgressEntry()
    {
        if (!File.Exists(TempPath))
        {
            return null;
        }

        var content = JsonConvert.DeserializeObject<DataEntryV2>(File.ReadAllText(TempPath));

        if (content != null)
        {
            return content;
        }
        else
        {
            Plugin.Logger.Error("Failed to load in-progress entry!");
            return null;
        }
    }

    public static void SaveInProgressEntry(DataEntryV2 entry)
    {
        File.WriteAllText(TempPath, JsonConvert.SerializeObject(entry));
        isDirty = true;
    }

    public static void Reset()
    {
        Entries.Clear();
        Save();
    }

    public static void Export()
    {
        try
        {
            var fileName = $"export-{DateTime.Now:yyyy-MM-dd HH-mm-ss}.csv"; // Avoid duplicate
            var exportPath = Path.Combine(Plugin.PluginInterface.ConfigDirectory.FullName, fileName);
            var records = JsonConvert.DeserializeObject<List<DataEntryV2>>(File.ReadAllText(DataPath));
            if (records == null || records.Count == 0) return;

            var flattenedRecords = records.Select(entry => new
            {
                entry.TerritoryId,
                entry.RouletteId,
                entry.IsCompleted,
                entry.BeginAt,
                entry.EndAt,
                entry.PlayerJobAbbr,
                PartyMembers = string.Join("|", entry.PartyMembers)
            });

            using var writer = new StreamWriter(exportPath);
            using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);

            csv.WriteRecords(flattenedRecords);
            Plugin.ChatGui.Print($"Exported to {exportPath}");
        }
        catch (Exception e)
        {
            Plugin.Logger.Error($"Failed to export data: {e.Message}");
        }
    }

    public static bool Import(string filePath)
    {
        if (!File.Exists(filePath)) return false;

        try
        {
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                MissingFieldFound = null,
            };
            using var reader = new StreamReader(filePath);
            using var csv = new CsvReader(reader, config);
            var importedEntries = new List<DataEntryV2>();

            csv.Read();
            csv.ReadHeader();
            while (csv.Read())
            {
                var territoryId = csv.GetField<ushort>("TerritoryId");
                var rouletteId = csv.GetField<uint>("RouletteId");
                var isCompleted = csv.GetField<bool>("IsCompleted");
                var beginAt = csv.GetField<string>("BeginAt");
                var endAt = csv.GetField<string>("EndAt");
                var playerJobAbbr = csv.GetField<string>("PlayerJobAbbr");
                var partyMembers = csv.GetField<string>("PartyMembers");
                if (territoryId == 0) continue;

                var entry = new DataEntryV2(territoryId, rouletteId)
                {
                    IsCompleted = isCompleted,
                    BeginAt = DateTime.Parse(beginAt!),
                    EndAt = DateTime.Parse(endAt!),
                    PlayerJobAbbr = playerJobAbbr!,
                    PartyMembers = partyMembers!.Split('|')
                };
                importedEntries.Add(entry);
            }

            foreach (var entry in importedEntries)
            {
                InsertEntry(entry);
            }
            Save();
            return true;
        }
        catch (Exception e)
        {
            Plugin.Logger.Error(e.Message);
            return false;
        }
    }
}
