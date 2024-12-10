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

    // the below exist just to make saving less cumbersome
    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}
