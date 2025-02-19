using ContactsTracker.Logic;
using System;
using System.Collections.Generic;

namespace ContactsTracker.Data;

// Store ID only. Get name when displayed. Support language change.
public class DataEntryV2(ushort territoryId, uint rouletteId)
{
    public int Version { get; set; } = 3; // Later will increase this not configuration
    public ushort TerritoryId { get; set; } = territoryId;
    public uint RouletteId { get; set; } = rouletteId;
    public bool IsCompleted { get; set; } = false;
    public DateTime BeginAt { get; set; } = DateTime.Now;
    public DateTime EndAt { get; set; } = DateTime.MinValue;
    public string PlayerJobAbbr { get; set; } = string.Empty;
    public List<string> PartyMembers { get; set; } = [];
    public DutySettings Settings { get; set; } = 0;

    public static DataEntryV2? Instance { get; private set; }

    public static void Initialize(DataEntryV2 _dataEntry) => Instance = _dataEntry;

    public static void Initialize(ushort territoryId, uint rouletteId) =>
    Instance = new DataEntryV2(territoryId, rouletteId);

    public static void EndRecord(Configuration configuration) =>
    EntryLogic.EndRecord(Instance, configuration);

    public static void SetSettings(bool Unrestricted, bool Minimal, bool Level, bool Silence, bool Explore)
    {
        if (Instance == null) return;
        Instance.Settings = 0;
        Plugin.Logger.Debug($"Settings: {Unrestricted} {Minimal} {Level} {Silence} {Explore}");
        if (Unrestricted) Instance.Settings |= DutySettings.IsUnrestrictedParty;
        if (Minimal) Instance.Settings |= DutySettings.IsMinimalIL;
        if (Level) Instance.Settings |= DutySettings.IsLevelSync;
        if (Silence) Instance.Settings |= DutySettings.IsSilenceEcho;
        if (Explore) Instance.Settings |= DutySettings.IsExplorerMode;
    }

    public static void Reset() => Instance = null;
}

[Flags]
public enum DutySettings
{
    IsUnrestrictedParty = 1,
    IsMinimalIL = 2,
    IsLevelSync = 4,
    IsSilenceEcho = 8,
    IsExplorerMode = 16,
}
