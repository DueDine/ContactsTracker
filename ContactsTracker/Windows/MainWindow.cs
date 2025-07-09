using ContactsTracker.Data;
using ContactsTracker.Resources;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Utility;
using ImGuiNET;
using System;
using System.Linq;
using System.Numerics;
using System.Collections.Generic;

namespace ContactsTracker.Windows;

public class MainWindow : Window, IDisposable
{
    private Plugin Plugin;
    private int selectedTab = 0;
    private bool isFileDialogOpen = false;
    private bool doubleCheck = false;
    private string searchText = string.Empty;
    private bool showCompletedOnly = false;
    private List<DataEntryV2> filteredEntries = [];
    private string dateFromText = string.Empty;
    private string dateToText = string.Empty;
    private DateTime? dateFrom = null;
    private DateTime? dateTo = null;

    public class SearchCriteria
    {
        public string TextSearch { get; set; } = string.Empty;
        public bool? CompletedOnly { get; set; } = null;
        public DateTime? DateFrom { get; set; } = null;
        public DateTime? DateTo { get; set; } = null;
        public string JobFilter { get; set; } = string.Empty;
    }

    public MainWindow(Plugin plugin)
        : base("Contacts Tracker", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
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
        using var tabBar = ImRaii.TabBar("MainTabBar");
        if (!tabBar) return;

        using (var activeTab = ImRaii.TabItem(Language.TabNameActive))
        {
            if (activeTab)
            {
                DrawActiveTab();
            }
        }
        using (var historyTab = ImRaii.TabItem(Language.TabNameHistory))
        {
            if (historyTab)
            {
                DrawHistoryTab();
            }
        }
        using (var settingsTab = ImRaii.TabItem(Language.TabNameSetting))
        {
            if (settingsTab)
            {
                DrawSettingsTab();
            }
        }
        using (var aboutTab = ImRaii.TabItem(Language.TabNameAbout))
        {
            if (aboutTab)
            {
                DrawAboutTab();
            }
        }
    }

    private void DrawActiveTab()
    {
        if (Plugin.Configuration.EnableLogging == false)
        {
            ImGuiHelpers.SafeTextWrapped(Language.WarningWhenDisableLogging);
            return;
        }

        var entries = DatabaseV2.Entries;
        if (entries.Count == 0 && DataEntryV2.Instance == null)
        {
            ImGuiHelpers.SafeTextWrapped(Language.NotAnyRecord);
            return;
        }

        var entry = entries.LastOrDefault();
        if (DataEntryV2.Instance != null && DataEntryV2.Instance.TerritoryId != 0)
        {
            entry = DataEntryV2.Instance;
            ImGuiHelpers.SafeTextWrapped(Language.WhenLogging);
        }
        else if (entry != default)
        {
            ImGuiHelpers.SafeTextWrapped(Language.NotActiveButHaveHistory);
        }
        else
        {
            ImGuiHelpers.SafeTextWrapped(Language.NotAnyRecord);
            return;
        }

        ImGui.Spacing();
        ImGuiHelpers.SafeTextWrapped($"{Language.TerritoryName}: {ExcelHelper.GetTerritoryName(entry.TerritoryId)}");
        ImGui.Spacing();
        ImGuiHelpers.SafeTextWrapped($"{Language.RouletteName}: {ExcelHelper.GetPoppedContentType(entry.RouletteId)}");
        ImGui.Spacing();
        ImGuiHelpers.SafeTextWrapped($"{entry.BeginAt} - {(entry.EndAt == DateTime.MinValue ? "N/A" : entry.EndAt)}");

        if (entry.EndAt != DateTime.MinValue)
        {
            ImGui.Spacing();
            ImGuiHelpers.SafeTextWrapped($"{Language.DurationOfEntry}: {entry.EndAt.Subtract(entry.BeginAt):hh\\:mm\\:ss}");
        }
        else if (Plugin.DutyState.IsDutyStarted) // Still in progress
        {
            ImGui.Spacing();
            ImGuiHelpers.SafeTextWrapped($"{Language.DurationOfEntry}: {DateTime.Now.Subtract(entry.BeginAt):hh\\:mm\\:ss}");
        }
        ImGui.Spacing();

        if (DataEntryV2.Instance != null)
        {
            if (ImGui.Button(Language.IgnoreCurrentEntry))
            {
                doubleCheck = true;
            }

            if (doubleCheck)
            {
                ImGui.SameLine();
                if (ImGui.Button(Language.DoubleConfirmIgnore))
                {
                    DataEntryV2.Reset();
                    doubleCheck = false;
                }

                ImGui.SameLine();
                if (ImGui.Button(Language.CancelIgnore))
                {
                    doubleCheck = false;
                }
            }
        }
    }

    private void DrawHistoryTab()
    {
        var entries = DatabaseV2.Entries;
        if (entries.Count == 0)
        {
            ImGuiHelpers.SafeTextWrapped(Language.NotAnyRecord);
            return;
        }

        ImGui.Text("Search:");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(200f);
        if (ImGui.InputText("##SearchText", ref searchText, 256))
        {
            var criteria = new SearchCriteria 
            { 
                TextSearch = searchText,
                CompletedOnly = showCompletedOnly ? true : null,
                DateFrom = dateFrom,
                DateTo = dateTo
            };
            filteredEntries = FilterEntries(entries, criteria);
            
            if (selectedTab >= filteredEntries.Count)
            {
                selectedTab = Math.Max(0, filteredEntries.Count - 1);
            }
        }

        ImGui.SameLine();
        if (ImGui.Checkbox("Completed Only", ref showCompletedOnly))
        {
            var criteria = new SearchCriteria 
            { 
                TextSearch = searchText,
                CompletedOnly = showCompletedOnly ? true : null,
                DateFrom = dateFrom,
                DateTo = dateTo
            };
            filteredEntries = FilterEntries(entries, criteria);
            
            if (selectedTab >= filteredEntries.Count)
            {
                selectedTab = Math.Max(0, filteredEntries.Count - 1);
            }
        }

        ImGui.SameLine();
        if (ImGui.Button("Clear"))
        {
            searchText = string.Empty;
            showCompletedOnly = false;
            dateFromText = string.Empty;
            dateToText = string.Empty;
            dateFrom = null;
            dateTo = null;
            filteredEntries = [.. entries.OrderBy(entry => entry.BeginAt)];
            selectedTab = Math.Max(0, filteredEntries.Count - 1);
        }

        // Date filtering row
        ImGui.Text("Date Range:");
        ImGui.SameLine();
        ImGui.Text("From:");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(120f);
        if (ImGui.InputText("##DateFrom", ref dateFromText, 64))
        {
            if (string.IsNullOrEmpty(dateFromText))
            {
                dateFrom = null;
            }
            else if (DateTime.TryParse(dateFromText, out var parsedFrom))
            {
                dateFrom = parsedFrom;
            }
            else
            {
                // Keep previous valid date if parsing fails
            }
            
            var criteria = new SearchCriteria 
            { 
                TextSearch = searchText,
                CompletedOnly = showCompletedOnly ? true : null,
                DateFrom = dateFrom,
                DateTo = dateTo
            };
            filteredEntries = FilterEntries(entries, criteria);
            
            if (selectedTab >= filteredEntries.Count)
            {
                selectedTab = Math.Max(0, filteredEntries.Count - 1);
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
        if (ImGui.InputText("##DateTo", ref dateToText, 64))
        {
            if (string.IsNullOrEmpty(dateToText))
            {
                dateTo = null;
            }
            else if (DateTime.TryParse(dateToText, out var parsedTo))
            {
                dateTo = parsedTo.Date.AddDays(1).AddSeconds(-1); // 23:59:59
            }
            else
            {
                //
            }
            
            var criteria = new SearchCriteria 
            { 
                TextSearch = searchText,
                CompletedOnly = showCompletedOnly ? true : null,
                DateFrom = dateFrom,
                DateTo = dateTo
            };
            filteredEntries = FilterEntries(entries, criteria);
            
            if (selectedTab >= filteredEntries.Count)
            {
                selectedTab = Math.Max(0, filteredEntries.Count - 1);
            }
        }
        
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Format: YYYY-MM-DD");
        }

        // Quick date buttons
        ImGui.SameLine();
        if (ImGui.Button("Today"))
        {
            var today = DateTime.Today;
            dateFromText = today.ToString("yyyy-MM-dd");
            dateToText = today.ToString("yyyy-MM-dd");
            dateFrom = today;
            dateTo = today.Date.AddDays(1).AddSeconds(-1);
            
            var criteria = new SearchCriteria 
            { 
                TextSearch = searchText,
                CompletedOnly = showCompletedOnly ? true : null,
                DateFrom = dateFrom,
                DateTo = dateTo
            };
            filteredEntries = FilterEntries(entries, criteria);
            
            if (selectedTab >= filteredEntries.Count)
            {
                selectedTab = Math.Max(0, filteredEntries.Count - 1);
            }
        }

        ImGui.SameLine();
        if (ImGui.Button("This Week"))
        {
            var today = DateTime.Today;
            var startOfWeek = today.AddDays(-(int)today.DayOfWeek + 1);
            var endOfWeek = startOfWeek.AddDays(6);
            
            dateFromText = startOfWeek.ToString("yyyy-MM-dd");
            dateToText = endOfWeek.ToString("yyyy-MM-dd");
            dateFrom = startOfWeek;
            dateTo = endOfWeek.Date.AddDays(1).AddSeconds(-1);
            
            var criteria = new SearchCriteria 
            { 
                TextSearch = searchText,
                CompletedOnly = showCompletedOnly ? true : null,
                DateFrom = dateFrom,
                DateTo = dateTo
            };
            filteredEntries = FilterEntries(entries, criteria);
            
            if (selectedTab >= filteredEntries.Count)
            {
                selectedTab = Math.Max(0, filteredEntries.Count - 1);
            }
        }

        ImGui.SameLine();
        if (ImGui.Button("This Month"))
        {
            var today = DateTime.Today;
            var startOfMonth = new DateTime(today.Year, today.Month, 1);
            var endOfMonth = startOfMonth.AddMonths(1).AddDays(-1);
            
            dateFromText = startOfMonth.ToString("yyyy-MM-dd");
            dateToText = endOfMonth.ToString("yyyy-MM-dd");
            dateFrom = startOfMonth;
            dateTo = endOfMonth.Date.AddDays(1).AddSeconds(-1);
            
            var criteria = new SearchCriteria 
            { 
                TextSearch = searchText,
                CompletedOnly = showCompletedOnly ? true : null,
                DateFrom = dateFrom,
                DateTo = dateTo
            };
            filteredEntries = FilterEntries(entries, criteria);
            
            if (selectedTab >= filteredEntries.Count)
            {
                selectedTab = Math.Max(0, filteredEntries.Count - 1);
            }
        }
        
        if (string.IsNullOrEmpty(searchText) && !showCompletedOnly && dateFrom == null && dateTo == null)
        {
            filteredEntries = [.. entries.OrderBy(entry => entry.BeginAt)];
        }
        
        ImGui.Text($"Showing {filteredEntries.Count} of {entries.Count} entries");
        if (dateFrom.HasValue || dateTo.HasValue)
        {
            ImGui.SameLine();
            var dateRangeText = dateFrom.HasValue && dateTo.HasValue 
                ? $"({dateFrom.Value:yyyy-MM-dd} to {dateTo.Value:yyyy-MM-dd})"
                : dateFrom.HasValue 
                    ? $"(from {dateFrom.Value:yyyy-MM-dd})"
                    : $"(to {dateTo!.Value:yyyy-MM-dd})"; // At least one to enter if, so null-forgiving
            ImGui.TextColored(ImGuiColors.DalamudGrey, dateRangeText);
        }
        ImGui.Separator();

        if (selectedTab >= filteredEntries.Count && filteredEntries.Count > 0)
        {
            selectedTab = filteredEntries.Count - 1;
        }
        else if (filteredEntries.Count == 0)
        {
            ImGuiHelpers.SafeTextWrapped("No entries match your search criteria.");
            return;
        }

        ImGui.Columns(2, "HistoryColumns", true);

        using (var child = ImRaii.Child("Sidebar", new Vector2(0, 0), true))
        {
            if (!child) return;

            ImGui.SetNextItemWidth(ImGui.GetWindowWidth() * 0.3f);
            if (ImGui.Button(Language.OpenAnalyzeWindow))
            {
                Plugin.ToggleAnalyzeUI();
            }
            ImGui.Separator();

            // Use filtered entries instead of all entries
            for (var i = filteredEntries.Count - 1; i >= 0; i--)
            {
                var isSelected = selectedTab == i;
                if (ImGui.Selectable($"{ExcelHelper.GetTerritoryName(filteredEntries[i].TerritoryId)} - {filteredEntries[i].BeginAt:yyyy-MM-dd HH:mm:ss}", selectedTab == i))
                {
                    selectedTab = i;
                }
                if (isSelected)
                {
                    ImGui.SetItemDefaultFocus();
                }
            }
        }

        ImGui.NextColumn();

        using (var child = ImRaii.Child("Details", new Vector2(0, 0), true))
        {
            if (!child) return;

            var entry = filteredEntries[selectedTab];
            ImGuiHelpers.SafeTextWrapped($"{Language.TerritoryName}: {ExcelHelper.GetTerritoryName(entry.TerritoryId)}");
            ImGui.Spacing();
            ImGuiHelpers.SafeTextWrapped($"{Language.RouletteName}: {ExcelHelper.GetPoppedContentType(entry.RouletteId)}");
            ImGui.Spacing();
            if (entry.Settings != 0)
            {
                ImGuiHelpers.SafeTextWrapped($"{Language.EntrySettings}: {entry.Settings}");
                ImGui.Spacing();
            }
            ImGuiHelpers.SafeTextWrapped($"{Language.IsEntryCompleted}: {(entry.IsCompleted ? Language.TextYes : Language.TextNo)}");
            ImGui.Spacing();
            ImGuiHelpers.SafeTextWrapped($"{Language.EntryTimeFromTo}: {entry.BeginAt:yyyy-MM-dd HH:mm:ss} - {(entry.EndAt == DateTime.MinValue ? "N/A" : entry.EndAt)}");
            ImGui.Spacing();
            ImGuiHelpers.SafeTextWrapped($"{Language.PlayerJob}: {entry.PlayerJobAbbr}");
            ImGui.Spacing();

            ImGuiHelpers.SafeTextWrapped(Language.EntryParty);
            if (entry.PartyMembers.Count == 0)
            {
                ImGui.SameLine();
                ImGuiHelpers.SafeTextWrapped("N/A");
            }
            else
            {
                foreach (var member in entry.PartyMembers)
                {
                    if (!string.IsNullOrEmpty(member))
                        ImGui.BulletText(member);
                }
            }
            ImGui.Spacing();

            if (ImGui.Button(Language.DeleteHistoryEntry))
            {
                if (Plugin.KeyState[VirtualKey.CONTROL])
                {
                    DatabaseV2.RemoveEntry(entry);

                    var criteria = new SearchCriteria 
                    { 
                        TextSearch = searchText,
                        CompletedOnly = showCompletedOnly ? true : null
                    };
                    filteredEntries = FilterEntries(DatabaseV2.Entries, criteria);

                    if (selectedTab >= filteredEntries.Count)
                    {
                        selectedTab = Math.Max(0, filteredEntries.Count - 1);
                    }
                }
            }

            if (ImGui.IsItemHovered())
            {
                if (!Plugin.KeyState[VirtualKey.CONTROL])
                {
                    ImGui.SetTooltip(Language.DeleteHistoryEntryCtrl);
                }
                else
                {
                    ImGui.SetTooltip(Language.DeleteHistoryEntryWarning);
                }
            }
        }

        ImGui.Columns(1);
    }

    private void DrawSettingsTab()
    {
        if (ImGui.CollapsingHeader(Language.SettingTabGeneral))
        {
            var enableLogging = Plugin.Configuration.EnableLogging;
            if (ImGui.Checkbox(Language.CheckboxEnableLogging, ref enableLogging))
            {
                Plugin.Configuration.EnableLogging = enableLogging;
                Plugin.Configuration.Save();
            }

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(Language.CheckboxEnableLoggingTooltip);
            }

            var enableLogParty = Plugin.Configuration.EnableLogParty;
            if (ImGui.Checkbox(Language.CheckboxEnableLogParty, ref enableLogParty))
            {
                Plugin.Configuration.EnableLogParty = enableLogParty;
                Plugin.Configuration.Save();
            }

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(Language.CheckboxEnableLogPartyTooltip);
            }

            if (Plugin.Configuration.EnableLogParty)
            {
                ImGui.SameLine();

                var logPartyClass = Plugin.Configuration.LogPartyClass;
                if (ImGui.Checkbox(Language.CheckboxEnableLogPartyClass, ref logPartyClass))
                {
                    Plugin.Configuration.LogPartyClass = logPartyClass;
                    Plugin.Configuration.Save();
                }

                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip(Language.CheckboxEnableLogPartyClassTooltip);
                }
            }

            var recordSolo = Plugin.Configuration.RecordSolo;
            if (ImGui.Checkbox(Language.CheckboxEnableRecordSolo, ref recordSolo))
            {
                Plugin.Configuration.RecordSolo = recordSolo;
                Plugin.Configuration.Save();
            }

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(Language.CheckboxEnableRecordSoloTooltip);
            }

            var onlyDutyRoulette = Plugin.Configuration.OnlyDutyRoulette;
            if (ImGui.Checkbox(Language.CheckboxEnableRecordRoulette, ref onlyDutyRoulette))
            {
                Plugin.Configuration.OnlyDutyRoulette = onlyDutyRoulette;
                Plugin.Configuration.Save();
            }

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(Language.CheckboxEnableRecordRouletteTooltip);
            }

            if (!Plugin.Configuration.OnlyDutyRoulette)
            {
                ImGui.SameLine();
                var recordUnrestricted = Plugin.Configuration.RecordDutySettings;
                if (ImGui.Checkbox(Language.CheckboxEnableRecordSettings, ref recordUnrestricted))
                {
                    Plugin.Configuration.RecordDutySettings = recordUnrestricted;
                    Plugin.Configuration.Save();
                }
            }

            var keepIncompleteEntry = Plugin.Configuration.KeepIncompleteEntry;
            if (ImGui.Checkbox(Language.CheckboxEnableKeepEntry, ref keepIncompleteEntry))
            {
                Plugin.Configuration.KeepIncompleteEntry = keepIncompleteEntry;
                Plugin.Configuration.Save();
            }

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(Language.CheckboxEnableKeepEntryTooltip);
            }
        }

        if (ImGui.CollapsingHeader(Language.SettingTabData))
        {
            if (ImGui.Button(Language.DataExport))
            {
                DatabaseV2.Export();
            }

            ImGui.SameLine();

            if (ImGui.Button(Language.DataImport))
            {
                isFileDialogOpen = true;
                Plugin.FileDialogManager.OpenFileDialog("Select a CSV File", ".csv", (success, paths) =>
                {
                    if (success && paths.Count > 0)
                    {
                        var path = paths.First();
                        var ok = DatabaseV2.Import(path);
                        if (ok)
                        {
                            Plugin.ChatGui.Print(Language.DataImportSuccess);
                        }
                        else
                        {
                            Plugin.ChatGui.PrintError(Language.DataImportFail);
                        }
                    }
                    isFileDialogOpen = false;
                }, 1, Plugin.PluginInterface.GetPluginConfigDirectory(), false);

            }

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(Language.ButtonImportWarning);
            }

            if (isFileDialogOpen)
            {
                Plugin.FileDialogManager.Draw();
            }

            ImGui.Spacing();

            if (Plugin.Configuration.EnableDeleteAll)
            {
                if (ImGui.Button(Language.ButtonDeleteAllAction))
                {
                    ImGui.OpenPopup("Confirm Delete ALL");
                }

                if (ImGui.BeginPopupModal("Confirm Delete ALL"))
                {
                    ImGuiHelpers.SafeTextWrapped(Language.ButtonDeleteAllWarning);
                    ImGui.Separator();

                    if (ImGui.Button(Language.ButtonSelectYes))
                    {
                        DatabaseV2.Reset();
                        ImGui.CloseCurrentPopup();
                    }
                    ImGui.SameLine();
                    if (ImGui.Button(Language.ButtonSelectNo))
                    {
                        ImGui.CloseCurrentPopup();
                    }
                    ImGui.EndPopup();
                }
            }

        }

        if (ImGui.CollapsingHeader(Language.SettingTabAdvance))
        {
            var deleteAll = Plugin.Configuration.EnableDeleteAll;
            if (ImGui.Checkbox(Language.ButtonDeleteAll, ref deleteAll))
            {
                Plugin.Configuration.EnableDeleteAll = deleteAll;
                Plugin.Configuration.Save();
            }

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(Language.ButtonDeleteAllTooltip);
            }

            ImGuiHelpers.ScaledDummy(5f);
            ImGuiHelpers.SafeTextWrapped($"Configuration Version: {Plugin.Configuration.Version}");
            ImGuiHelpers.SafeTextWrapped($"Database Version: {DatabaseV2.Version}");
            ImGuiHelpers.SafeTextWrapped($"Plugin Version: {Plugin.PluginInterface.Manifest.AssemblyVersion}");
        }
    }

    private static void DrawAboutTab()
    {
        ImGuiHelpers.ScaledDummy(5f);

        ImGui.TextColored(ImGuiColors.DalamudRed, Language.PluginAboutInfo);

        ImGuiHelpers.ScaledDummy(2f);

        ImGui.TextColored(ImGuiColors.DalamudOrange, Language.AuthorDiscord);
        ImGui.TextColored(ImGuiColors.DalamudOrange, Language.PluginHowToFeedback);

        ImGuiHelpers.ScaledDummy(5f);

        using (ImRaii.PushColor(ImGuiCol.Button, ImGuiColors.ParsedBlue))
        {
            if (ImGui.Button("GitHub"))
            {
                Util.OpenLink("https://github.com/DueDine/ContactsTracker");
            }
        }
    }

    private static List<DataEntryV2> FilterEntries(List<DataEntryV2> entries, SearchCriteria criteria)
    {
        return [.. entries.Where(entry => 
        {
            // Text search across multiple fields
            if (!string.IsNullOrEmpty(criteria.TextSearch))
            {
                var searchText = criteria.TextSearch.ToLower();
                var territoryName = ExcelHelper.GetTerritoryName(entry.TerritoryId).ToLower();
                var rouletteName = ExcelHelper.GetPoppedContentType(entry.RouletteId).ToLower();
                var partyText = string.Join(" ", entry.PartyMembers).ToLower();
                
                if (!territoryName.Contains(searchText) && 
                    !rouletteName.Contains(searchText) && 
                    !partyText.Contains(searchText) &&
                    !entry.PlayerJobAbbr.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                    return false;
            }
            
            if (criteria.CompletedOnly.HasValue && entry.IsCompleted != criteria.CompletedOnly.Value)
                return false;

            if (!string.IsNullOrEmpty(criteria.JobFilter) && 
                !entry.PlayerJobAbbr.Contains(criteria.JobFilter, StringComparison.OrdinalIgnoreCase))
                return false;

            if (criteria.DateFrom.HasValue && entry.BeginAt.Date < criteria.DateFrom.Value.Date)
                return false;
            
            if (criteria.DateTo.HasValue && entry.BeginAt.Date > criteria.DateTo.Value.Date)
                return false;
            
            return true;
        }).OrderBy(entry => entry.BeginAt)];
    }
}
