using ContactsTracker.Logic;
using System;
using System.Collections.Generic;

namespace ContactsTracker.Data;

// Store ID only. Get name when displayed. Support language change.
public class DataEntryV2(ushort territoryId, uint rouletteId)
{
    public int Version { get; set; } = 2; // Later will increase this not configuration
    public ushort TerritoryId { get; set; } = territoryId;
    public uint RouletteId { get; set; } = rouletteId;
    public bool IsCompleted { get; set; } = false;
    public DateTime BeginAt { get; set; } = DateTime.Now;
    public DateTime EndAt { get; set; } = DateTime.MinValue;
    public string PlayerJobAbbr { get; set; } = string.Empty;
    public List<string> PartyMembers { get; set; } = [];

    public static DataEntryV2? Instance { get; private set; }

    public static void Initialize(DataEntryV2 _dataEntry) => Instance = _dataEntry;

    public static void Initialize(ushort territoryId, uint rouletteId) =>
    Instance = new DataEntryV2(territoryId, rouletteId);

    public static void EndRecord(Configuration configuration) =>
    EntryLogic.EndRecord(Instance, configuration);

    public static void Reset() => Instance = null;
}

