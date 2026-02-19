using CsvHelper;
using CsvHelper.Configuration;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;

namespace ContactsTracker.Data;

public class DatabaseV2
{
    public static int Version { get; set; } = 2;
    private static readonly string DataPath = Path.Combine(Plugin.PluginInterface.ConfigDirectory.FullName, $"data_v{Version}.json");
    private static readonly string TempPath = Path.Combine(Plugin.PluginInterface.ConfigDirectory.FullName, $"temp_v{Version}.json");
    private static readonly Lock EntriesLock = new();
    public static bool isDirty = false;

    public static List<DataEntryV2> Entries { get; private set; } = [];

    public static int Count => Entries.Count;

    public static void InsertEntry(DataEntryV2 entry)
    {
        lock (EntriesLock)
        {
            Entries.Add(entry);
            Save();
        }
    }

    public static void Save()
    {
        lock (EntriesLock)
        {
            File.WriteAllText(DataPath, JsonConvert.SerializeObject(Entries));
        }
    }

    public static void Load()
    {
        if (!File.Exists(DataPath))
        {
            Save();
        }

        try
        {
            var content = JsonConvert.DeserializeObject<List<DataEntryV2>>(File.ReadAllText(DataPath));

            if (content != null)
            {
                lock (EntriesLock)
                {
                    Entries = content;
                }
            }
            else
            {
                Plugin.Logger.Error("Failed to load data!");
            }
        }
        catch (Exception e)
        {
            Plugin.Logger.Error($"Failed to load data: {e.Message}");
        }
    }

    public static bool RemoveEntry(DataEntryV2 entry)
    {
        lock (EntriesLock)
        {
            if (!Entries.Remove(entry))
            {
                return false;
            }

            Save();
            return true;
        }
    }

    public static DataEntryV2? LoadInProgressEntry()
    {
        if (!File.Exists(TempPath))
        {
            return null;
        }

        try
        {
            var content = JsonConvert.DeserializeObject<DataEntryV2>(File.ReadAllText(TempPath));

            if (content != null)
            {
                return content;
            }

            Plugin.Logger.Error("Failed to load in-progress entry!");
        }
        catch (Exception e)
        {
            Plugin.Logger.Error($"Failed to load in-progress entry: {e.Message}");
        }

        return null;
    }

    public static void SaveInProgressEntry(DataEntryV2 entry)
    {
        File.WriteAllText(TempPath, JsonConvert.SerializeObject(entry));
        isDirty = true;
    }

    public static void Reset()
    {
        lock (EntriesLock)
        {
            Entries.Clear();
            Save();
        }
    }

    public static (bool Success, string Message) Export()
    {
        try
        {
            var fileName = $"export-{DateTime.Now:yyyy-MM-dd HH-mm-ss}.csv"; // Avoid duplicate
            var exportPath = Path.Combine(Plugin.PluginInterface.ConfigDirectory.FullName, fileName);
            var records = JsonConvert.DeserializeObject<List<DataEntryV2>>(File.ReadAllText(DataPath));
            if (records == null || records.Count == 0)
            {
                return (false, "No data available to export.");
            }

            var flattenedRecords = records
                .OrderBy(entry => entry.BeginAt)
                .Select(entry => new
                {
                    entry.TerritoryId,
                    entry.RouletteId,
                    entry.IsCompleted,
                    BeginAt = entry.BeginAt.ToString("O", CultureInfo.InvariantCulture),
                    EndAt = entry.EndAt == DateTime.MinValue
                        ? string.Empty
                        : entry.EndAt.ToString("O", CultureInfo.InvariantCulture),
                    entry.PlayerJobAbbr,
                    PartyMembers = string.Join("|", entry.PartyMembers),
                    Settings = (int)entry.Settings
                });

            using var writer = new StreamWriter(exportPath);
            using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);

            csv.WriteRecords(flattenedRecords);
            return (true, $"Exported to {exportPath}");
        }
        catch (Exception e)
        {
            Plugin.Logger.Error($"Failed to export data: {e.Message}");
            return (false, "Failed to export data.");
        }
    }

    public static (bool Success, string Message) Import(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return (false, "Import file not found.");
        }

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
                var settings = csv.GetField<int>("Settings");
                if (territoryId == 0) continue;

                if (!TryParseCsvDateTime(beginAt, allowEmpty: false, out var parsedBeginAt))
                {
                    return (false, $"Invalid BeginAt at row {csv.Parser.Row}.");
                }
                if (!TryParseCsvDateTime(endAt, allowEmpty: true, out var parsedEndAt))
                {
                    return (false, $"Invalid EndAt at row {csv.Parser.Row}.");
                }

                var parsedPartyMembers = string.IsNullOrWhiteSpace(partyMembers)
                    ? new List<string>()
                    : [.. partyMembers.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];

                var entry = new DataEntryV2(territoryId, rouletteId)
                {
                    IsCompleted = isCompleted,
                    BeginAt = parsedBeginAt,
                    EndAt = parsedEndAt,
                    PlayerJobAbbr = playerJobAbbr!,
                    PartyMembers = parsedPartyMembers,
                    Settings = (DutySettings)settings
                };
                importedEntries.Add(entry);
            }

            if (importedEntries.Count == 0)
            {
                return (false, "No valid entries found in the file.");
            }

            var removedDuplicates = 0;
            lock (EntriesLock)
            {
                var mergedEntries = new List<DataEntryV2>(Entries.Count + importedEntries.Count);
                mergedEntries.AddRange(Entries);
                mergedEntries.AddRange(importedEntries);
                Entries = mergedEntries;
                removedDuplicates = DeduplicateEntries(saveChanges: false);
                Save();
            }
            if (removedDuplicates > 0)
            {
                Plugin.Logger.Information($"Removed {removedDuplicates} duplicate entries after import.");
            }
            return (true, $"Imported {importedEntries.Count} entries.");
        }
        catch (Exception e)
        {
            Plugin.Logger.Error(e.Message);
            return (false, "Failed to import.");
        }
    }

    private static bool TryParseCsvDateTime(string? input, bool allowEmpty, out DateTime parsed)
    {
        if (string.IsNullOrWhiteSpace(input) || (allowEmpty && string.Equals(input, "N/A", StringComparison.OrdinalIgnoreCase)))
        {
            parsed = DateTime.MinValue;
            return allowEmpty;
        }

        if (DateTime.TryParseExact(input, "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out parsed))
        {
            return true;
        }

        var styles = DateTimeStyles.AllowWhiteSpaces;
        if (DateTime.TryParse(input, CultureInfo.InvariantCulture, styles, out parsed))
        {
            return true;
        }

        if (DateTime.TryParse(input, CultureInfo.CurrentCulture, styles, out parsed))
        {
            return true;
        }

        parsed = DateTime.MinValue;
        return false;
    }

    public static int DeduplicateEntries(bool saveChanges = true)
    {
        lock (EntriesLock)
        {
            var uniqueEntries = Entries.Distinct().ToList();

            var removedCount = Entries.Count - uniqueEntries.Count;
            if (removedCount > 0)
            {
                Entries = uniqueEntries;
                if (saveChanges)
                {
                    Save();
                }
            }

            return removedCount;
        }
    }
}
