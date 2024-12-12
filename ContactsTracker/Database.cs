using CsvHelper;
using CsvHelper.Configuration;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace ContactsTracker;

public class Database
{
    public static string dataPath = Path.Combine(Plugin.PluginInterface.ConfigDirectory.FullName, "data.json");
    public static string tempPath = Path.Combine(Plugin.PluginInterface.ConfigDirectory.FullName, "temp.json"); // As journaling
    public static string contentIdPath = Path.Combine(Plugin.PluginInterface.ConfigDirectory.FullName, "contentId.json"); // TODO
    public static bool isDirty = false; // If temp

    public static List<DataEntry> Entries { get; private set; } = [];

    public static void InsertEntry(DataEntry entry)
    {
        Entries.Add(entry);
        Save();
    }

    public static void Save()
    {
        File.WriteAllText(dataPath, JsonConvert.SerializeObject(Entries));
    }

    public static void Load()
    {
        if (!File.Exists(dataPath))
        {
            Save(); // Initialize the file
        }

        var content = JsonConvert.DeserializeObject<List<DataEntry>>(File.ReadAllText(dataPath));

        if (content != null)
        {
            Entries = content;
        }
        else
        {
            Plugin.Logger.Debug("Failed to load data.json!");
        }
    }

    // TODO: User reconnection logic
    public static DataEntry? LoadFromTempPath()
    {
        if (!File.Exists(tempPath))
        {
            return null;
        }

        var content = File.ReadAllText(tempPath);
        if (content != null)
        {
            // File.Delete(tempPath); // Remove temp file
            Plugin.Logger.Debug("Recover previous entry");
            return JsonConvert.DeserializeObject<DataEntry>(content);
        }
        else
        {
            Plugin.Logger.Debug("Failed to load temp.json!");
            return null;
        }
    }

    public static void SaveInProgressEntry(DataEntry entry)
    {
        File.WriteAllText(tempPath, JsonConvert.SerializeObject(entry));
        isDirty = true;
    }

    public static void Export()
    {
        var fileName = $"export-{DateTime.Now:yyyy-MM-dd HH-mm-ss}.csv"; // Avoid duplicate
        var exportPath = Path.Combine(Plugin.PluginInterface.ConfigDirectory.FullName, fileName);
        var records = JsonConvert.DeserializeObject<List<DataEntry>>(File.ReadAllText(dataPath));
        if (records == null)
        {
            Plugin.Logger.Debug("Failed to export data.json!");
            return;
        }
        else if (records.Count == 0)
        {
            Plugin.Logger.Debug("No records to export.");
            return;
        }
        else
        {
            using var writer = new StreamWriter(exportPath);
            using var csv = new CsvWriter(writer, System.Globalization.CultureInfo.InvariantCulture);
            csv.WriteRecords(records);
        }

        Plugin.ChatGui.Print($"Exported to {exportPath}"); // Maybe we can open explorer directly?
    }

    public static bool Import(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Plugin.Logger.Debug("File not found.");
            return false;
        }

        try
        {
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                MissingFieldFound = null, // Ignore missing fields
            };
            using var reader = new StreamReader(filePath);
            using var csv = new CsvReader(reader, config);
            var importedEntries = new List<DataEntry>();

            csv.Read();
            csv.ReadHeader();
            while (csv.Read())
            {
                var territoryName = csv.GetField<string>("TerritoryName");
                var rouletteType = csv.GetField<string>("RouletteType");
                var isCompleted = csv.GetField<bool>("IsCompleted");
                var date = csv.GetField<string>("Date");
                var beginAt = csv.GetField<string>("beginAt");
                var endAt = csv.GetField<string>("endAt");
                var jobName = csv.GetField<string>("jobName");
                var partyMembers = csv.GetField<string[]>("partyMembers");
                var comment = csv.GetField<string>("comment");
                var entry = new DataEntry(territoryName, rouletteType, isCompleted)
                {
                    Date = date!,
                    beginAt = beginAt!,
                    endAt = endAt,
                    jobName = jobName,
                    partyMembers = partyMembers,
                    comment = comment! 
                };
                importedEntries.Add(entry);
            }

            foreach (var entry in importedEntries)
            {
                InsertEntry(entry);
            }
            Save();
            Plugin.Logger.Debug($"Imported {importedEntries.Count} entries successfully.");
            return true;
        }
        catch (Exception e)
        {
            Plugin.Logger.Debug("Failed to import.");
            Plugin.Logger.Debug(e.Message);
            return false;
        }
    }

    public static void Archive(Configuration configuration)
    {
        if (configuration.ArchiveWhenEntriesExceed == -1)
        {
            Plugin.Logger.Debug("ArchiveWhenEntriesExceed is -1, skipping archive.");
            return;
        }

        if (Entries.Count < configuration.ArchiveWhenEntriesExceed)
        {
            Plugin.Logger.Debug("Entries count is less than ArchiveWhenEntriesExceed, skipping archive.");
            return;
        }

        var fileName = $"archive-{DateTime.Now:yyyy-MM-dd HH-mm-ss}.csv";
        var archivePath = Path.Combine(Plugin.PluginInterface.ConfigDirectory.FullName, fileName);
        var records = JsonConvert.DeserializeObject<List<DataEntry>>(File.ReadAllText(dataPath));

        if (records == null)
        {
            Plugin.Logger.Debug("Failed to archive data.json!");
            return;
        }
        else if (records.Count == 0)
        {
            Plugin.Logger.Debug("No records to archive.");
            return;
        }
        else
        {
            using var writer = new StreamWriter(archivePath);
            using var csv = new CsvWriter(writer, System.Globalization.CultureInfo.InvariantCulture);
            csv.WriteRecords(records);
        }

        Plugin.ChatGui.Print($"Archived to {archivePath}");

        if (File.Exists(archivePath)) // Then keep newest ArchiveKeepEntries entries
        {
            var recordsToKeep = records.Skip(records.Count - configuration.ArchiveKeepEntries).ToList();
            File.WriteAllText(dataPath, JsonConvert.SerializeObject(recordsToKeep));
            Load(); // Update Entries
        }
        else
        {
            Plugin.Logger.Debug("Failed to archive data.json!");
        }

    }

    public static void Reset() // Very dangerous
    {
        Entries = [];
        Save();
    }

}
