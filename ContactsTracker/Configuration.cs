using Dalamud.Configuration;
using System;

namespace ContactsTracker;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    public bool EnableLogging { get; set; } = true;

    public bool EnableLogParty { get; set; } = false;

    public bool LogPartyClass { get; set; } = false;

    public bool RecordSolo { get; set; } = false;

    public bool OnlyDutyRoulette { get; set; } = true;

    public bool RecordDutySettings { get; set; } = false;

    public bool KeepIncompleteEntry { get; set; } = true;

    public bool ArchiveOldEntries { get; set; } = false;

    public int ArchiveWhenEntriesExceed { get; set; } = -1; // -1 means no limit

    public int ArchiveKeepEntries { get; set; } = 100; // Remove oldest ArchiveWhenEntriesExceed - ArchiveKeepEntries entries

    public bool EnableDeleteAll { get; set; } = false; // Dangerous. Disabled by default.

    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}
