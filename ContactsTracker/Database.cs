using CsvHelper;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;

namespace ContactsTracker;

public class Database
{
    public static string dataPath = Path.Combine(Plugin.PluginInterface.ConfigDirectory.FullName, "data.json");
    public static string tempPath = Path.Combine(Plugin.PluginInterface.ConfigDirectory.FullName, "temp.json"); // As journaling

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
        return JsonConvert.DeserializeObject<DataEntry>(File.ReadAllText(tempPath));
    }

    public static void SaveInProgressEntry(DataEntry entry)
    {
        File.WriteAllText(tempPath, JsonConvert.SerializeObject(entry));
    }

    public static void Export()
    {
        var exportPath = Path.Combine(Plugin.PluginInterface.ConfigDirectory.FullName, "export.csv");
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
            using (var writer = new StreamWriter(exportPath))
            using (var csv = new CsvWriter(writer, System.Globalization.CultureInfo.InvariantCulture))
            {
                csv.WriteRecords(records);
            }
        }

        Plugin.ChatGui.Print($"Exported to {exportPath}");
    }

}
