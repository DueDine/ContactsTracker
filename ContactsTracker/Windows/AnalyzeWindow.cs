using ContactsTracker.Data;
using ContactsTracker.Query;
using ContactsTracker.Resources;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace ContactsTracker.Windows;

internal class QueryState
{
    public bool IsClicked { get; set; } = false;
    public bool IsAvailable { get; set; } = false;
    public bool IsStale { get; set; } = false;
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
    private string dateFromText = string.Empty;
    private string dateToText = string.Empty;
    private DateTime? dateFrom = null;
    private DateTime? dateTo = null;
    private int lastEntriesCount = -1;
    private List<DataEntryV2>? lastEntriesRef = null;
    private DateTime? lastDateFrom = null;
    private DateTime? lastDateTo = null;

    private List<(ushort TerritoryId, uint RouletteId, int Count)> resultsExtractOccurrences = [];
    private List<(uint RouletteId, TimeSpan TotalDuration, TimeSpan AverageDuration, int Count)> resultsTotalDurations = [];
    private List<(ushort TerritoryId, int Count)> resultsByRoulette = [];
    private List<DataEntryV2> filteredEntries = [];

    public AnalyzeWindow(Plugin plugin)
    : base("Analyze", ImGuiWindowFlags.NoScrollbar)
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

        UpdateFilteredEntries(resetQueries: false);
        DrawDateFilters();
        if (HasStaleQueryResults())
        {
            ImGui.TextColored(ImGuiColors.DalamudOrange, "Some results are not up-to-date. Please click Query again.");
        }
        ImGuiHelpers.ScaledDummy(5f);

        DrawTopXQuery();
        DrawTotalDurationQuery();
        DrawByRouletteQuery();
    }

    private void DrawDateFilters()
    {
        var filterChanged = false;

        ImGui.Text("Date Range:");
        ImGui.SameLine();
        ImGui.Text("From:");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(120f);
        if (ImGui.InputText("##AnalyzeDateFrom", ref dateFromText, 64))
        {
            filterChanged = true;
            if (string.IsNullOrEmpty(dateFromText))
            {
                dateFrom = null;
            }
            else if (DateTime.TryParse(dateFromText, out var parsedFrom))
            {
                dateFrom = parsedFrom;
            }
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Format: YYYY-MM-DD");
        }

        ImGui.SameLine();
        ImGui.Text("To:");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(120f);
        if (ImGui.InputText("##AnalyzeDateTo", ref dateToText, 64))
        {
            filterChanged = true;
            if (string.IsNullOrEmpty(dateToText))
            {
                dateTo = null;
            }
            else if (DateTime.TryParse(dateToText, out var parsedTo))
            {
                dateTo = parsedTo.Date.AddDays(1).AddSeconds(-1);
            }
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Format: YYYY-MM-DD");
        }

        ImGui.SameLine();
        if (ImGui.Button("Today"))
        {
            filterChanged = true;
            var today = DateTime.Today;
            dateFromText = today.ToString("yyyy-MM-dd");
            dateToText = today.ToString("yyyy-MM-dd");
            dateFrom = today;
            dateTo = today.Date.AddDays(1).AddSeconds(-1);
        }

        ImGui.SameLine();
        if (ImGui.Button("This Week"))
        {
            filterChanged = true;
            var today = DateTime.Today;
            var diff = (7 + (int)today.DayOfWeek - (int)DayOfWeek.Monday) % 7;
            var startOfWeek = today.AddDays(-diff);
            var endOfWeek = startOfWeek.AddDays(6);

            dateFromText = startOfWeek.ToString("yyyy-MM-dd");
            dateToText = endOfWeek.ToString("yyyy-MM-dd");
            dateFrom = startOfWeek;
            dateTo = endOfWeek.Date.AddDays(1).AddSeconds(-1);
        }

        ImGui.SameLine();
        if (ImGui.Button("This Month"))
        {
            filterChanged = true;
            var today = DateTime.Today;
            var startOfMonth = new DateTime(today.Year, today.Month, 1);
            var endOfMonth = startOfMonth.AddMonths(1).AddDays(-1);

            dateFromText = startOfMonth.ToString("yyyy-MM-dd");
            dateToText = endOfMonth.ToString("yyyy-MM-dd");
            dateFrom = startOfMonth;
            dateTo = endOfMonth.Date.AddDays(1).AddSeconds(-1);
        }

        ImGui.SameLine();
        if (ImGui.Button("Clear Filters"))
        {
            filterChanged = true;
            dateFromText = string.Empty;
            dateToText = string.Empty;
            dateFrom = null;
            dateTo = null;
        }

        if (filterChanged)
        {
            UpdateFilteredEntries();
        }

        ImGui.Text($"Showing {filteredEntries.Count} of {DatabaseV2.Entries.Count} entries");
        if (dateFrom.HasValue || dateTo.HasValue)
        {
            ImGui.SameLine();
            var dateRangeText = dateFrom.HasValue && dateTo.HasValue
                ? $"({dateFrom.Value:yyyy-MM-dd} to {dateTo.Value:yyyy-MM-dd})"
                : dateFrom.HasValue
                    ? $"(from {dateFrom.Value:yyyy-MM-dd})"
                    : $"(to {dateTo!.Value:yyyy-MM-dd})";
            ImGui.TextColored(ImGuiColors.DalamudGrey, dateRangeText);
        }
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
            topXState.IsStale = false;
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
                var occurrences = RouletteQueries.ExtractOccurrences(filteredEntries);
                occurrences.Sort((a, b) => b.Count.CompareTo(a.Count));
                if (topX >= 1)
                {
                    occurrences = [.. occurrences.Take(topX)];
                }
                resultsExtractOccurrences = occurrences;
                topXState.IsAvailable = true;
                topXState.IsStale = false;
                isBusy = false;
                topXState.IsClicked = false;
            }
        }

        if (topXState.IsAvailable)
        {
            if (topXState.IsStale)
            {
                ImGui.TextColored(ImGuiColors.DalamudOrange, "Data changed. Please click Query to refresh this result.");
            }

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
            totalDurationState.IsStale = false;
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
                var durations = RouletteQueries.CalculateTotalDurations(filteredEntries);
                durations.Sort((a, b) => b.TotalDuration.CompareTo(a.TotalDuration));
                resultsTotalDurations = durations;
                totalDurationState.IsAvailable = true;
                totalDurationState.IsStale = false;
                isBusy = false;
                totalDurationState.IsClicked = false;
            }
        }

        if (totalDurationState.IsAvailable)
        {
            if (totalDurationState.IsStale)
            {
                ImGui.TextColored(ImGuiColors.DalamudOrange, "Data changed. Please click Query to refresh this result.");
            }

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

            foreach (var (RouletteId, TotalDuration, AverageDuration, Count) in resultsTotalDurations)
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.TextWrapped($"{ExcelHelper.GetPoppedContentType(RouletteId)} (x{Count})");
                ImGui.TableNextColumn();
                ImGui.TextWrapped(TotalDuration.ToString("hh\\:mm\\:ss"));
                ImGui.TableNextColumn();
                ImGui.TextWrapped(AverageDuration.ToString("hh\\:mm\\:ss"));
            }
        }
    }

    private void DrawByRouletteQuery()
    {
        if (!ImGui.CollapsingHeader(Language.QueryRouletteFrequency)) return;

        ImGui.SetNextItemWidth(ImGui.CalcTextSize("Duty Roulette: High-level Dungeons").X + 50);
        using (var combo = ImRaii.Combo("##ByRoulette", ExcelHelper.GetPoppedContentType((uint)selectedRoulette)))
        {
            if (combo.Success)
            {
                foreach (var roulette in Enum.GetValues<RouletteType>())
                {
                    if (ImGui.Selectable(ExcelHelper.GetPoppedContentType((uint)roulette), selectedRoulette == roulette))
                    {
                        selectedRoulette = roulette;
                        byRouletteState.IsClicked = false;
                        byRouletteState.IsAvailable = false;
                        byRouletteState.IsStale = false;
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
            byRouletteState.IsStale = false;
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
                var occurrences = RouletteQueries.OccurrencesByRoulette(filteredEntries, (uint)selectedRoulette);
                occurrences.Sort((a, b) => b.Count.CompareTo(a.Count));
                resultsByRoulette = occurrences;
                byRouletteState.IsAvailable = true;
                byRouletteState.IsStale = false;
                isBusy = false;
                byRouletteState.IsClicked = false;
            }
        }

        if (byRouletteState.IsAvailable)
        {
            if (byRouletteState.IsStale)
            {
                ImGui.TextColored(ImGuiColors.DalamudOrange, "Data changed. Please click Query to refresh this result.");
            }

            var matchingCount = filteredEntries.Count(entry => entry.RouletteId == (uint)selectedRoulette && entry.IsCompleted);
            if (matchingCount > 0)
            {
                ImGui.Text($"Total {matchingCount} entries");
            }
            
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

    private void UpdateFilteredEntries(bool resetQueries = true)
    {
        var entriesList = DatabaseV2.Entries;
        var entriesChanged = !ReferenceEquals(entriesList, lastEntriesRef)
            || entriesList.Count != lastEntriesCount;
        var dateChanged = !Nullable.Equals(dateFrom, lastDateFrom)
            || !Nullable.Equals(dateTo, lastDateTo);
        var shouldRefresh = entriesChanged || dateChanged;

        if (!shouldRefresh)
        {
            if (resetQueries)
            {
                ResetQueryResults();
            }
            return;
        }

        var entries = entriesList.AsEnumerable();

        if (dateFrom.HasValue)
        {
            entries = entries.Where(entry => entry.BeginAt.Date >= dateFrom.Value.Date);
        }

        if (dateTo.HasValue)
        {
            entries = entries.Where(entry => entry.BeginAt.Date <= dateTo.Value.Date);
        }

        filteredEntries = [.. entries];
        lastEntriesCount = entriesList.Count;
        lastEntriesRef = entriesList;
        lastDateFrom = dateFrom;
        lastDateTo = dateTo;

        if (entriesChanged && !resetQueries)
        {
            MarkAvailableQueryResultsAsStale();
        }

        if (resetQueries)
        {
            ResetQueryResults();
        }
    }

    private void ResetQueryResults()
    {
        topXState.IsClicked = false;
        topXState.IsAvailable = false;
        topXState.IsStale = false;

        totalDurationState.IsClicked = false;
        totalDurationState.IsAvailable = false;
        totalDurationState.IsStale = false;

        byRouletteState.IsClicked = false;
        byRouletteState.IsAvailable = false;
        byRouletteState.IsStale = false;

        resultsExtractOccurrences.Clear();
        resultsTotalDurations.Clear();
        resultsByRoulette.Clear();

        isBusy = false;
    }

    private bool HasStaleQueryResults()
    {
        return topXState.IsStale || totalDurationState.IsStale || byRouletteState.IsStale;
    }

    private void MarkAvailableQueryResultsAsStale()
    {
        if (topXState.IsAvailable)
        {
            topXState.IsStale = true;
        }

        if (totalDurationState.IsAvailable)
        {
            totalDurationState.IsStale = true;
        }

        if (byRouletteState.IsAvailable)
        {
            byRouletteState.IsStale = true;
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
