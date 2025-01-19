using ContactsTracker.Data;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ContactsTracker.Query;

public static class RouletteQueries
{
    public static List<(string? TerritoryName, string? RouletteType, int Count)> ExtractOccurrences(List<DataEntry> Entries)
    {
        return [.. Entries
            .Where(entry => entry.RouletteType != null)
            .Where(entry => entry.IsCompleted)
            .GroupBy(Entries => (Entries.TerritoryName, Entries.RouletteType))
            .Select(group => (group.Key.TerritoryName, group.Key.RouletteType, group.Count()))];
    }

    public static List<(string? RouletteType, TimeSpan TotalDuration, TimeSpan AverageDuration)> CalculateTotalDurations(List<DataEntry> Entries)
    {
        return [.. Entries
            .Where(entry => entry.RouletteType != null)
            .Where(entry => entry.IsCompleted && entry.endAt != null)
            .GroupBy(entry => entry.RouletteType)
            .Select(group =>
            {
                var validDurations = group
                    .Select(entry =>
                    {
                        if (TimeSpan.TryParse(entry.beginAt, out var beginAt) && TimeSpan.TryParse(entry.endAt, out var endAt))
                        {
                            if (endAt < beginAt) // If the roulette ends on the next day
                            {
                                endAt = endAt.Add(TimeSpan.FromDays(1));
                            }
                            return endAt - beginAt;
                        }
                        return TimeSpan.Zero;
                    })
                    .Where(duration => duration > TimeSpan.Zero)
                    .ToList();

                var totalDuration = validDurations.Aggregate(TimeSpan.Zero, (sum, duration) => sum + duration);
                var averageDuration = validDurations.Count > 0
                    ? TimeSpan.FromTicks(validDurations.Sum(d => d.Ticks) / validDurations.Count)
                    : TimeSpan.Zero;

                return (RouletteType: group.Key, TotalDuration: totalDuration, AverageDuration: averageDuration);
            })];
    }
}