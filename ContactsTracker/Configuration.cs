using Dalamud.Configuration;
using System;

namespace ContactsTracker;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;

    public bool EnableLogging { get; set; } = false;

    public bool RecordSolo { get; set; } = false;

    public bool PrintToChat { get; set; } = true;

    public bool OnlyDutyRoulette { get; set; } = false;

    public bool ArchiveOldEntries { get; set; } = false;

    public int ArchiveWhenEntriesExceed { get; set; } = -1; // -1 means no limit

    public int ArchiveKeepEntries { get; set; } = 5; // Remove oldest ArchiveWhenEntriesExceed - ArchiveKeepEntries entries

    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}
