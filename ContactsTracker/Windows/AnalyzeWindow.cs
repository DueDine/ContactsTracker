using ContactsTracker.Data;
using ContactsTracker.Query;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
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

    private readonly QueryState topXState = new();
    private readonly QueryState totalDurationState = new();

    private bool isBusy = false;
    private int topX = 0;

    private List<(string? TerritoryName, string? RouletteType, int Count)> resultsExtractOccurrences = [];
    private List<(string? RouletteType, TimeSpan TotalDuration, TimeSpan AverageDuration)> resultsTotalDurations = [];

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

        DrawTopXQuery();
        DrawTotalDurationQuery();
    }

    private void DrawTopXQuery()
    {
        if (!ImGui.CollapsingHeader("Top X pair of Roulette and Map")) return;

        ImGui.SetNextItemWidth(100);
        if (ImGui.InputInt("##TopX", ref topX))
        {
            if (topX < 1) topX = 0;
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
                var occurrences = RouletteQueries.ExtractOccurrences(Database.Entries);
                occurrences.Sort((a, b) => b.Count.CompareTo(a.Count));
                if (topX >= 1)
                {
                    occurrences = [.. occurrences.Take(topX)];
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
                return;
            }

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

    private void DrawTotalDurationQuery()
    {
        if (!ImGui.CollapsingHeader("How much time for each roulette")) return;

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
                var durations = RouletteQueries.CalculateTotalDurations(Database.Entries);
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
                return;
            }

            using var table = ImRaii.Table("##TotalDurations", 3);
            if (!table) return;

            ImGui.TableNextColumn();
            ImGui.TableHeader("Type");
            ImGui.TableNextColumn();
            ImGui.TableHeader("Total");
            ImGui.TableNextColumn();
            ImGui.TableHeader("Average");

            foreach (var (RouletteType, TotalDuration, AverageDuration) in resultsTotalDurations)
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGuiHelpers.SafeTextWrapped(RouletteType ?? "Unknown");
                ImGui.TableNextColumn();
                ImGuiHelpers.SafeTextWrapped(TotalDuration.ToString());
                ImGui.TableNextColumn();
                ImGuiHelpers.SafeTextWrapped(AverageDuration.ToString("hh\\:mm\\:ss"));
            }
        }
    }
}
