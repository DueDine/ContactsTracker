using Dalamud.Interface.Windowing;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace ContactsTracker.Windows;

public class AnalyzeWindow : Window, IDisposable
{
    private Plugin Plugin;

    private bool isBusy = false;
    private bool isClicked = false;
    private int topX = 0;
    private bool isAvailable = false;

    // For Tuple (Map, Roulette, Times)
    private static List<(string? TerritoryName, string? RouletteType, int Count)> ExtractOccurrences(List<DataEntry> Entries)
    {
        return Entries
            .GroupBy(Entries => (Entries.TerritoryName, Entries.RouletteType))
            .Select(group => (group.Key.TerritoryName, group.Key.RouletteType, group.Count()))
            .ToList();
    }
    List<(string? TerritoryName, string? RouletteType, int Count)> resultsExtractOccurrences = [];

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
                isAvailable = false;
            }
            ImGui.SameLine();
            if (ImGui.Button("Query"))
            {
                isClicked = true;
                isAvailable = false;
            }

            if (isClicked && topX != 0)
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
                    if (topX > 1)
                    {
                        occurrences = occurrences.Take(topX).ToList();
                    }
                    resultsExtractOccurrences = occurrences;
                    isAvailable = true;
                    isBusy = false;
                    isClicked = false;
                }
            }

            if (isAvailable)
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
    }
}
