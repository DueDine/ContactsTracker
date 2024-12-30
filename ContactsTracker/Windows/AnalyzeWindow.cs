using Dalamud.Interface.Windowing;
using Dalamud.Interface.Utility;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Interface.Utility.Raii;

namespace ContactsTracker.Windows;

internal class QueryState
{
    public bool IsClicked { get; set; } = false;
    public bool IsAvailable { get; set; } = false;
}

public class AnalyzeWindow : Window, IDisposable
{
    private Plugin Plugin;

    private readonly QueryState topXState = new();
    private readonly QueryState totalDurationState = new();

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
                            if (endAt < beginAt) // If the roulette ends on the next day
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
        ImGuiHelpers.SafeTextWrapped("If you have a suggestion for a new query, please let me know.");
        ImGuiHelpers.ScaledDummy(5f);

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
            if (ImGui.Button("Query##Q1"))
            {
                topXState.IsClicked = true;
                topXState.IsAvailable = false;
            }
            ImGui.SameLine();
            if (ImGui.Button("Reset##R1"))
            {
                topXState.IsClicked = false;
                topXState.IsAvailable = false;
                resultsExtractOccurrences.Clear();
            }

            if (topXState.IsClicked && topX != 0)
            {
                if (isBusy)
                {
                    ImGuiHelpers.SafeTextWrapped("Processing...");
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
                if (resultsExtractOccurrences.Count == 0)
                {
                    ImGuiHelpers.SafeTextWrapped("No data available.");
                }
                else
                {
                    using var table = ImRaii.Table("##ExtractOccurrences", 3);
                    if (!table) return;

                    ImGui.TableNextColumn();
                    ImGui.TableHeader("Map");
                    ImGui.TableNextColumn();
                    ImGui.TableHeader("Roulette");
                    ImGui.TableNextColumn();
                    ImGui.TableHeader("Times");

                    foreach (var (TerritoryName, RouletteType, Count) in resultsExtractOccurrences)
                    {
                        ImGui.TableNextRow();
                        ImGui.TableNextColumn();
                        ImGuiHelpers.SafeTextWrapped(TerritoryName ?? "Unknown");
                        ImGui.TableNextColumn();
                        ImGuiHelpers.SafeTextWrapped(RouletteType ?? "Unknown");
                        ImGui.TableNextColumn();
                        ImGuiHelpers.SafeTextWrapped(Count.ToString());
                    }
                }
            }
        }

        if (ImGui.CollapsingHeader("How much time for each roulette"))
        {
            if (ImGui.Button("Query##Q2"))
            {
                totalDurationState.IsClicked = true;
                totalDurationState.IsAvailable = false;
            }
            ImGui.SameLine();
            if (ImGui.Button("Reset##R2"))
            {
                totalDurationState.IsClicked = false;
                totalDurationState.IsAvailable = false;
                resultsTotalDurations.Clear();
            }

            if (totalDurationState.IsClicked)
            {
                if (isBusy)
                {
                    ImGuiHelpers.SafeTextWrapped("Processing...");
                }
                else
                {
                    isBusy = true;
                    var durations = CalculateTotalDurations(Database.Entries);
                    durations.Sort((a, b) => b.TotalDuration.CompareTo(a.TotalDuration));
                    resultsTotalDurations = durations;
                    totalDurationState.IsAvailable = true;
                    isBusy = false;
                    totalDurationState.IsClicked = false;
                }
            }

            if (totalDurationState.IsAvailable)
            {
                if (resultsTotalDurations.Count == 0)
                {
                    ImGuiHelpers.SafeTextWrapped("No data available.");
                }
                else
                {
                    using var table = ImRaii.Table("##TotalDurations", 2);
                    if (!table) return;

                    ImGui.TableNextColumn();
                    ImGui.TableHeader("Type");
                    ImGui.TableNextColumn();
                    ImGui.TableHeader("Total");

                    foreach (var (RouletteType, TotalDuration) in resultsTotalDurations)
                    {
                        ImGui.TableNextRow();
                        ImGui.TableNextColumn();
                        ImGuiHelpers.SafeTextWrapped(RouletteType ?? "Unknown");
                        ImGui.TableNextColumn();
                        ImGuiHelpers.SafeTextWrapped(TotalDuration.ToString());
                    }
                }
            }
        }
    }
}
