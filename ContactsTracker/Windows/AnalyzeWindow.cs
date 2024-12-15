using Dalamud.Interface.Windowing;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace ContactsTracker.Windows;

internal class QueryState
{
    public bool IsClicked { get; set; } = false;
    public bool IsAvailable { get; set; } = false;
}

public class AnalyzeWindow : Window, IDisposable
{
    private Plugin Plugin;

    private QueryState topXState = new();
    private QueryState totalDurationState = new();

    private bool isBusy = false;
    private int topX = 0;

    private static List<(string? TerritoryName, string? RouletteType, int Count)> ExtractOccurrences(List<DataEntry> Entries)
    {
        return Entries
            .GroupBy(Entries => (Entries.TerritoryName, Entries.RouletteType))
            .Select(group => (group.Key.TerritoryName, group.Key.RouletteType, group.Count()))
            .ToList();
    }

    private List<(string? TerritoryName, string? RouletteType, int Count)> resultsExtractOccurrences = [];

    private static List<(string? RouletteType, TimeSpan TotalDuration)> CalculateTotalDurations(List<DataEntry> Entries)
    {
        return Entries
            .Where(entry => entry.IsCompleted && entry.endAt != null)
            .GroupBy(entry => entry.RouletteType)
            .Select(group =>
            {
                var totalDuration = group
                    .Select(entry =>
                    {
                        if (TimeSpan.TryParse(entry.beginAt, out var beginAt) && TimeSpan.TryParse(entry.endAt, out var endAt))
                        {
                            if (endAt < beginAt)
                            {
                                endAt = endAt.Add(TimeSpan.FromDays(1));
                            }

                            return endAt - beginAt;
                        }

                        return TimeSpan.Zero;
                    })
                    .Aggregate(TimeSpan.Zero, (sum, duration) => sum + duration);

                return (RouletteType: group.Key, TotalDuration: totalDuration);
            })
            .ToList();
    }

    private List<(string? RouletteType, TimeSpan TotalDuration)> resultsTotalDurations = [];

    public AnalyzeWindow(Plugin plugin)
    : base("Analyze - Still developing", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(375, 330),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        Plugin = plugin;
    }

    public void Dispose() { }

    public override void Draw()
    {
        // ImGui.Text("Some query will take a while to complete, please be patient.");
        ImGui.Text("If you have a suggestion for a new query, please let me know.");

        if (ImGui.CollapsingHeader("Top X pair of Roulette and Map"))
        {
            ImGui.SetNextItemWidth(100);
            if (ImGui.InputInt(Namespace + "TopX", ref topX))
            {
                if (topX < 1)
                    topX = 0;
                topXState.IsClicked = false;
            }
            ImGui.SameLine();
            if (ImGui.Button("Query"))
            {
                topXState.IsClicked = true;
                topXState.IsAvailable = false;
            }
            ImGui.SameLine();
            if (ImGui.Button("Reset"))
            {
                topX = 0;
                topXState.IsClicked = false;
                topXState.IsAvailable = false;
                resultsExtractOccurrences.Clear();
            }

            if (topXState.IsClicked && topX != 0)
            {
                if (isBusy)
                {
                    ImGui.Text("Processing...");
                }
                else
                {
                    isBusy = true;
                    var occurrences = ExtractOccurrences(Database.Entries.Where(entry => entry.IsCompleted).ToList());
                    occurrences.Sort((a, b) => b.Count.CompareTo(a.Count));
                    if (topX >= 1)
                    {
                        occurrences = occurrences.Take(topX).ToList();
                    }
                    resultsExtractOccurrences = occurrences;
                    topXState.IsAvailable = true;
                    isBusy = false;
                    topXState.IsClicked = false;
                }
            }

            if (topXState.IsAvailable)
            {
                ImGui.Columns(3, "Top X pair of Roulette and Map", true);
                ImGui.Text("Map");
                ImGui.NextColumn();
                ImGui.Text("Roulette");
                ImGui.NextColumn();
                ImGui.Text("Times");
                ImGui.NextColumn();
                foreach (var (TerritoryName, RouletteType, Count) in resultsExtractOccurrences)
                {
                    ImGui.Text(TerritoryName ?? "Unknown");
                    ImGui.NextColumn();
                    ImGui.Text(RouletteType ?? "Unknown");
                    ImGui.NextColumn();
                    ImGui.Text(Count.ToString());
                    ImGui.NextColumn();
                }
                ImGui.Columns(1);
            }
        }

        if (ImGui.CollapsingHeader("How much time for each roulette"))
        {
            if (ImGui.Button("Query"))
            {
                totalDurationState.IsClicked = true;
                totalDurationState.IsAvailable = false;
            }
            ImGui.SameLine();
            if (ImGui.Button("Reset"))
            {
                totalDurationState.IsClicked = false;
                totalDurationState.IsAvailable = false;
                resultsTotalDurations.Clear();
            }

            if (totalDurationState.IsClicked)
            {
                if (isBusy)
                {
                    ImGui.Text("Processing...");
                }
                else
                {
                    isBusy = true;
                    var durations = CalculateTotalDurations(Database.Entries);
                    resultsTotalDurations = durations;
                    totalDurationState.IsAvailable = true;
                    isBusy = false;
                    totalDurationState.IsClicked = false;
                }
            }

            if (totalDurationState.IsAvailable)
            {
                ImGui.Columns(2, "How much time for each roulette", true);
                ImGui.Text("Type");
                ImGui.NextColumn();
                ImGui.Text("Total");
                ImGui.NextColumn();
                foreach (var (RouletteType, TotalDuration) in resultsTotalDurations)
                {
                    ImGui.Text(RouletteType ?? "Unknown");
                    ImGui.NextColumn();
                    ImGui.Text(TotalDuration.ToString());
                    ImGui.NextColumn();
                }
                ImGui.Columns(1);
            }
        }
    }
}
