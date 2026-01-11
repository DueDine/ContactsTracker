using ContactsTracker.Data;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ContactsTracker.Query;

public static class RouletteQueries
{
    public static List<(ushort TerritoryId, uint RouletteId, int Count)> ExtractOccurrences(List<DataEntryV2> Entries)
    {
        return [.. Entries
            .Where(entry => entry.RouletteId != 0)
            .Where(entry => entry.IsCompleted)
            .GroupBy(Entries => (Entries.TerritoryId, Entries.RouletteId))
            .Select(group => (group.Key.TerritoryId, group.Key.RouletteId, group.Count()))];
    }

    public static List<(uint RouletteId, TimeSpan TotalDuration, TimeSpan AverageDuration, int Count)> CalculateTotalDurations(List<DataEntryV2> Entries)
    {
        return [.. Entries
            .Where(entry => entry.RouletteId != 0)
            .Where(entry => entry.IsCompleted && entry.EndAt != DateTime.MinValue)
            .GroupBy(entry => entry.RouletteId)
            .Select(group =>
            {
                var validDurations = group
                    .Select(entry => entry.EndAt - entry.BeginAt)
                    .Where(duration => duration > TimeSpan.Zero)
                    .ToList();

                var totalDuration = validDurations.Aggregate(TimeSpan.Zero, (sum, duration) => sum + duration);
                var averageDuration = validDurations.Count > 0
                    ? TimeSpan.FromTicks(validDurations.Sum(d => d.Ticks) / validDurations.Count)
                    : TimeSpan.Zero;

                return (RouletteId: group.Key, TotalDuration: totalDuration, AverageDuration: averageDuration, Count: validDurations.Count);
            })];
    }

    public static List<(ushort TerritoryId, int Count)> OccurrencesByRoulette(List<DataEntryV2> Entries, uint rouletteId)
    {
        return [.. Entries
            .Where(entry => entry.RouletteId == rouletteId)
            .Where(entry => entry.IsCompleted)
            .GroupBy(entry => entry.TerritoryId)
            .Select(group => (group.Key, group.Count()))];
    }

}
