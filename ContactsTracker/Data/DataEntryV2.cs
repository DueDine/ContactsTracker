using ContactsTracker.Logic;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ContactsTracker.Data;

// Store ID only. Get name when displayed. Support language change.
public class DataEntryV2(ushort territoryId, uint rouletteId) : IEquatable<DataEntryV2>
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
        if (Unrestricted) Instance.Settings |= DutySettings.UnrestrictedParty;
        if (Minimal) Instance.Settings |= DutySettings.MinimalIL;
        if (Level) Instance.Settings |= DutySettings.LevelSync;
        if (Silence) Instance.Settings |= DutySettings.SilenceEcho;
        if (Explore) Instance.Settings |= DutySettings.ExplorerMode;
    }

    public static void Reset() => Instance = null;

    public bool Equals(DataEntryV2? other)
    {
        if (ReferenceEquals(this, other)) return true;
        if (other is null) return false;

        if (TerritoryId != other.TerritoryId
            || RouletteId != other.RouletteId
            || IsCompleted != other.IsCompleted
            || BeginAt != other.BeginAt
            || EndAt != other.EndAt
            || Settings != other.Settings
            || !string.Equals(PlayerJobAbbr, other.PlayerJobAbbr, StringComparison.Ordinal))
        {
            return false;
        }

        return PartyMembers.SequenceEqual(other.PartyMembers, StringComparer.OrdinalIgnoreCase);
    }

    public override bool Equals(object? obj) => obj is DataEntryV2 other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(TerritoryId);
        hash.Add(RouletteId);
        hash.Add(IsCompleted);
        hash.Add(BeginAt);
        hash.Add(EndAt);
        hash.Add(PlayerJobAbbr, StringComparer.Ordinal);
        hash.Add((int)Settings);
        hash.Add(PartyMembers.Count);
        foreach (var member in PartyMembers)
        {
            hash.Add(member, StringComparer.OrdinalIgnoreCase);
        }

        return hash.ToHashCode();
    }
}

[Flags]
public enum DutySettings
{
    UnrestrictedParty = 1,
    MinimalIL = 2,
    LevelSync = 4,
    SilenceEcho = 8,
    ExplorerMode = 16,
}
