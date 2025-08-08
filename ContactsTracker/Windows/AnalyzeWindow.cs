using ContactsTracker.Data;
using ContactsTracker.Query;
using ContactsTracker.Resources;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
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
    private readonly QueryState byRouletteState = new();

    private bool isBusy = false;
    private int topX = 0;
    private RouletteType selectedRoulette = RouletteType.Leveling;

    private List<(ushort TerritoryId, uint RouletteId, int Count)> resultsExtractOccurrences = [];
    private List<(uint RouletteId, TimeSpan TotalDuration, TimeSpan AverageDuration)> resultsTotalDurations = [];
    private List<(ushort TerritoryId, int Count)> resultsByRoulette = [];

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
        ImGui.TextWrapped(Language.AnalyzeFeedback);
        ImGuiHelpers.ScaledDummy(5f);

        DrawTopXQuery();
        DrawTotalDurationQuery();
        DrawByRouletteQuery();
    }

    private void DrawTopXQuery()
    {
        if (!ImGui.CollapsingHeader(Language.QueryTopX)) return;

        ImGui.SetNextItemWidth(100);
        if (ImGui.InputInt("##TopX", ref topX))
        {
            if (topX < 1) topX = 0;
            topXState.IsClicked = false;
        }
        ImGui.SameLine();
        if (ImGui.Button($"{Language.ButtonQuery}###Q1"))
        {
            topXState.IsClicked = true;
            topXState.IsAvailable = false;
        }
        ImGui.SameLine();
        if (ImGui.Button($"{Language.ButtonReset}###R1"))
        {
            topXState.IsClicked = false;
            topXState.IsAvailable = false;
            resultsExtractOccurrences.Clear();
        }

        if (topXState.IsClicked && topX != 0)
        {
            if (isBusy)
            {
                ImGui.TextWrapped(Language.QueryInProcess);
            }
            else
            {
                isBusy = true;
                var occurrences = RouletteQueries.ExtractOccurrences(DatabaseV2.Entries);
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
                ImGui.TextWrapped(Language.QueryNoResult);
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

            foreach (var (TerritoryId, RouletteId, Count) in resultsExtractOccurrences)
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.TextWrapped(ExcelHelper.GetTerritoryName(TerritoryId));
                ImGui.TableNextColumn();
                ImGui.TextWrapped(ExcelHelper.GetPoppedContentType(RouletteId));
                ImGui.TableNextColumn();
                ImGui.TextWrapped(Count.ToString());
            }
        }
    }

    private void DrawTotalDurationQuery()
    {
        if (!ImGui.CollapsingHeader(Language.QuerySpentTime)) return;

        if (ImGui.Button($"{Language.ButtonQuery}###Q2"))
        {
            totalDurationState.IsClicked = true;
            totalDurationState.IsAvailable = false;
        }
        ImGui.SameLine();
        if (ImGui.Button($"{Language.ButtonReset}###R2"))
        {
            totalDurationState.IsClicked = false;
            totalDurationState.IsAvailable = false;
            resultsTotalDurations.Clear();
        }

        if (totalDurationState.IsClicked)
        {
            if (isBusy)
            {
                ImGui.TextWrapped(Language.QueryInProcess);
            }
            else
            {
                isBusy = true;
                var durations = RouletteQueries.CalculateTotalDurations(DatabaseV2.Entries);
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
                ImGui.TextWrapped(Language.QueryNoResult);
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

            foreach (var (RouletteId, TotalDuration, AverageDuration) in resultsTotalDurations)
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.TextWrapped(ExcelHelper.GetPoppedContentType(RouletteId));
                ImGui.TableNextColumn();
                ImGui.TextWrapped(TotalDuration.ToString());
                ImGui.TableNextColumn();
                ImGui.TextWrapped(AverageDuration.ToString("hh\\:mm\\:ss"));
            }
        }
    }

    private void DrawByRouletteQuery()
    {
        if (!ImGui.CollapsingHeader(Language.QueryRouletteFrequency)) return;

        ImGui.SetNextItemWidth(ImGui.CalcTextSize("Duty Roulette: High-level Dungeons").X + 50);
        using (var combo = ImRaii.Combo("##ByRoulette", ExcelHelper.GetRouletteName((uint)selectedRoulette)))
        {
            if (combo.Success)
            {
                foreach (var roulette in Enum.GetValues<RouletteType>())
                {
                    if (ImGui.Selectable(ExcelHelper.GetRouletteName((uint)roulette), selectedRoulette == roulette))
                    {
                        selectedRoulette = roulette;
                        byRouletteState.IsClicked = false;
                        byRouletteState.IsAvailable = false;
                    }
                }
            }
        }
        ImGui.SameLine();
        if (ImGui.Button($"{Language.ButtonQuery}###Q3"))
        {
            byRouletteState.IsClicked = true;
            byRouletteState.IsAvailable = false;
        }
        ImGui.SameLine();
        if (ImGui.Button($"{Language.ButtonReset}###R3"))
        {
            byRouletteState.IsClicked = false;
            byRouletteState.IsAvailable = false;
            resultsByRoulette.Clear();
        }

        if (byRouletteState.IsClicked)
        {
            if (isBusy)
            {
                ImGui.TextWrapped(Language.QueryInProcess);
            }
            else
            {
                isBusy = true;
                var occurrences = RouletteQueries.OccurrencesByRoulette(DatabaseV2.Entries, (uint)selectedRoulette);
                occurrences.Sort((a, b) => b.Count.CompareTo(a.Count));
                resultsByRoulette = occurrences;
                byRouletteState.IsAvailable = true;
                isBusy = false;
                byRouletteState.IsClicked = false;
            }
        }

        if (byRouletteState.IsAvailable)
        {
            if (resultsByRoulette.Count == 0)
            {
                ImGui.TextWrapped(Language.QueryNoResult);
                return;
            }
            using var table = ImRaii.Table("##ByRoulette", 2);
            if (!table) return;
            ImGui.TableNextColumn();
            ImGui.TableHeader("Map");
            ImGui.TableNextColumn();
            ImGui.TableHeader("Times");
            foreach (var (TerritoryId, Count) in resultsByRoulette)
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.TextWrapped(ExcelHelper.GetTerritoryName(TerritoryId));
                ImGui.TableNextColumn();
                ImGui.TextWrapped(Count.ToString());
            }
        }
    }
}

internal enum RouletteType
{
    Leveling = 1,
    HighLevel = 2,
    Scenario = 3,
    Guildhests = 4,
    Expert = 5,
    Trials = 6,
    Frontline = 7,
    LevelCap = 8,
    Mentor = 9,
    AllianceRaid = 15,
    NormalRaid = 17,
}
