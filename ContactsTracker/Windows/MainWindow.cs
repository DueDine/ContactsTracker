using Dalamud.Game.ClientState.Keys;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using System;
using System.Linq;
using System.Numerics;

namespace ContactsTracker.Windows;

public class MainWindow : Window, IDisposable
{
    private Plugin Plugin;
    private int selectedTab = 0;
    // private string commentBuffer = string.Empty;
    private bool isFileDialogOpen = false;
    private bool doubleCheck = false;
    private bool enableSearch = false;
    private string searchBuffer = string.Empty;

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

        using (var activeTab = ImRaii.TabItem("Active"))
        {
            if (activeTab)
            {
                DrawActiveTab();
            }
        }
        using (var historyTab = ImRaii.TabItem("History"))
        {
            if (historyTab)
            {
                DrawHistoryTab();
            }
        }
        using (var settingsTab = ImRaii.TabItem("Settings"))
        {
            if (settingsTab)
            {
                DrawSettingsTab();
            }
        }
    }

    private void DrawActiveTab()
    {
        if (Plugin.Configuration.EnableLogging == false)
        {
            ImGuiHelpers.SafeTextWrapped("Logging Disabled. Read-Only Mode.");
            return;
        }

        var entries = Database.Entries;
        if (entries.Count == 0 && DataEntry.Instance == null)
        {
            ImGuiHelpers.SafeTextWrapped("No record yet.");
            return;
        }

        var entry = entries.LastOrDefault();
        if (DataEntry.Instance != null && !string.IsNullOrEmpty(DataEntry.Instance.TerritoryName))
        {
            entry = DataEntry.Instance;
            ImGuiHelpers.SafeTextWrapped("Currently Logging");
        }
        else if (entry != default)
        {
            ImGuiHelpers.SafeTextWrapped("No Active Log. Display Last Entry Instead.");
        }
        else
        {
            ImGuiHelpers.SafeTextWrapped("No Active Log.");
            return;
        }

        ImGui.Spacing();
        ImGuiHelpers.SafeTextWrapped($"Place: {entry.TerritoryName}");
        ImGui.Spacing();
        ImGuiHelpers.SafeTextWrapped($"Join via: {(string.IsNullOrEmpty(entry.RouletteType) ? "Normal" : entry.RouletteType)}");
        ImGui.Spacing();
        ImGuiHelpers.SafeTextWrapped($"When: {entry.Date}");
        ImGui.SameLine();
        ImGuiHelpers.SafeTextWrapped($"{entry.beginAt} - {(string.IsNullOrEmpty(entry.endAt) ? "N/A" : entry.endAt)}");

        if (!string.IsNullOrEmpty(entry.endAt))
        {
            ImGui.Spacing();
            ImGuiHelpers.SafeTextWrapped($"Duration: {DateTime.Parse(entry.endAt).Subtract(DateTime.Parse(entry.beginAt)):hh\\:mm\\:ss}");
        }
        else if (Plugin.DutyState.IsDutyStarted) // Still in progress
        {
            ImGui.Spacing();
            ImGuiHelpers.SafeTextWrapped($"Duration: {DateTime.Now.Subtract(DateTime.Parse(entry.beginAt)):hh\\:mm\\:ss}");
        }
        ImGui.Spacing();

        if (DataEntry.Instance != null)
        {
            if (ImGui.Button("Ignore"))
            {
                doubleCheck = true;
            }

            if (doubleCheck)
            {
                ImGui.SameLine();
                if (ImGui.Button("Confirm Ignore"))
                {
                    DataEntry.Reset();
                    doubleCheck = false;
                }

                ImGui.SameLine();
                if (ImGui.Button("Cancel"))
                {
                    doubleCheck = false;
                }
            }
        }
    }

    private void DrawHistoryTab()
    {
        var entries = Database.Entries;

        if (ImGui.Button("Enable / Disable Search Function"))
        {
            enableSearch = !enableSearch;
            searchBuffer = string.Empty;
        }

        if (entries.Count == 0)
        {
            ImGuiHelpers.SafeTextWrapped("No record yet.");
            return;
        }

        ImGui.Columns(2, "HistoryColumns", true);

        using (var child = ImRaii.Child("Sidebar", new Vector2(0, 0), true))
        {
            if (!child) return;

            if (enableSearch)
            {
                ImGui.Spacing();
                if (ImGui.InputText("Search", ref searchBuffer, 50))
                {
                    selectedTab = Math.Max(-1, entries.Count - 1); // Reset to last entry
                }
                ImGui.Spacing();
                if (!string.IsNullOrEmpty(searchBuffer))
                {
                    entries = entries
                    .Where(entry =>
                        (entry.TerritoryName?.Contains(searchBuffer, StringComparison.OrdinalIgnoreCase) ?? false) ||
                        (entry.RouletteType?.Contains(searchBuffer, StringComparison.OrdinalIgnoreCase) ?? false) ||
                        (entry.Date?.Contains(searchBuffer, StringComparison.OrdinalIgnoreCase) ?? false) ||
                        (entry.beginAt?.Contains(searchBuffer, StringComparison.OrdinalIgnoreCase) ?? false) ||
                        (entry.endAt?.Contains(searchBuffer, StringComparison.OrdinalIgnoreCase) ?? false) ||
                        (entry.jobName?.Contains(searchBuffer, StringComparison.OrdinalIgnoreCase) ?? false) ||
                        (entry.partyMembers?.Contains(searchBuffer, StringComparison.OrdinalIgnoreCase) ?? false) ||
                        (entry.comment?.Contains(searchBuffer, StringComparison.OrdinalIgnoreCase) ?? false)
                ).ToList();
                    if (selectedTab >= entries.Count)
                        selectedTab = Math.Max(-1, entries.Count - 1);
                }

                if (selectedTab == -1)
                {
                    ImGuiHelpers.SafeTextWrapped("No record found.");
                    return;
                }
            }

            for (var i = entries.Count - 1; i >= 0; i--)
            {
                var isSelected = selectedTab == i;
                if (ImGui.Selectable($"{entries[i].TerritoryName} - {entries[i].Date} {entries[i].beginAt}", selectedTab == i))
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

            var entry = entries[selectedTab];
            ImGuiHelpers.SafeTextWrapped($"Name: {entry.TerritoryName}");
            ImGui.Spacing();
            ImGuiHelpers.SafeTextWrapped($"Type: {(string.IsNullOrEmpty(entry.RouletteType) ? "Normal" : entry.RouletteType)}");
            ImGui.Spacing();
            ImGuiHelpers.SafeTextWrapped($"Completed?: {(entry.IsCompleted ? "Yes" : "No")}");
            ImGui.Spacing();
            ImGuiHelpers.SafeTextWrapped($"Date: {entry.Date}");
            ImGui.Spacing();
            ImGuiHelpers.SafeTextWrapped($"Time: {entry.beginAt} - {(string.IsNullOrEmpty(entry.endAt) ? "N/A" : entry.endAt)}");
            ImGui.Spacing();
            ImGuiHelpers.SafeTextWrapped($"Job: {entry.jobName}");
            ImGui.Spacing();

            ImGuiHelpers.SafeTextWrapped("Party Members:");
            if (entry.partyMembers == null)
            {
                ImGui.SameLine();
                ImGuiHelpers.SafeTextWrapped("N/A");
            }
            else
            {
                // Separate by |
                var members = entry.partyMembers.Split('|');
                if (members.Length > 1)
                    members = members.Take(members.Length - 1).ToArray();
                foreach (var member in members)
                {
                    // Remove trailing space if any
                    ImGui.BulletText(member.Trim());
                }
            }
            ImGui.Spacing();

            if (ImGui.Button("Delete Entry"))
            {
                if (Plugin.KeyState[VirtualKey.CONTROL])
                {
                    entries.RemoveAt(selectedTab);
                    Database.Save();

                    if (selectedTab >= entries.Count)
                    {
                        selectedTab = Math.Max(-1, entries.Count - 1);
                    }
                }
            }

            if (ImGui.IsItemHovered())
            {
                if (!Plugin.KeyState[VirtualKey.CONTROL])
                {
                    ImGui.SetTooltip("Hold CTRL to Delete.");
                }
                else
                {
                    ImGui.SetTooltip("This action is irreversible.");
                }
            }
        }

        ImGui.Columns(1);
    }

    private void DrawSettingsTab()
    {
        if (ImGui.CollapsingHeader("General"))
        {
            var enableLogging = Plugin.Configuration.EnableLogging;
            if (ImGui.Checkbox("Enable Logging", ref enableLogging))
            {
                Plugin.Configuration.EnableLogging = enableLogging;
                Plugin.Configuration.Save();
            }

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Enable to start recording data.");
            }

            var enableLogParty = Plugin.Configuration.EnableLogParty;
            if (ImGui.Checkbox("Enable Log Party", ref enableLogParty))
            {
                Plugin.Configuration.EnableLogParty = enableLogParty;
                Plugin.Configuration.Save();
            }

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Enable to log party members on completion.");
            }

            var recordSolo = Plugin.Configuration.RecordSolo;
            if (ImGui.Checkbox("Record Solo", ref recordSolo))
            {
                Plugin.Configuration.RecordSolo = recordSolo;
                Plugin.Configuration.Save();
            }

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Enable to record solo duty.");
            }

            var onlyDutyRoulette = Plugin.Configuration.OnlyDutyRoulette;
            if (ImGui.Checkbox("Only Record Duty Roulette", ref onlyDutyRoulette))
            {
                Plugin.Configuration.OnlyDutyRoulette = onlyDutyRoulette;
                Plugin.Configuration.Save();
            }

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Enable to only record duty joined by roulette.");
            }

            var keepIncompleteEntry = Plugin.Configuration.KeepIncompleteEntry;
            if (ImGui.Checkbox("Keep Incomplete Entry", ref keepIncompleteEntry))
            {
                Plugin.Configuration.KeepIncompleteEntry = keepIncompleteEntry;
                Plugin.Configuration.Save();
            }

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Keep record regardless of completion.");
            }
        }

        if (ImGui.CollapsingHeader("Output"))
        {
            var printToChat = Plugin.Configuration.PrintToChat;
            if (ImGui.Checkbox("Print To Chat", ref printToChat))
            {
                Plugin.Configuration.PrintToChat = printToChat;
                Plugin.Configuration.Save();
            }

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("On Duty Complete, whether output 'ContactsTracker Record Completed'.");
            }

        }

        if (ImGui.CollapsingHeader("Data"))
        {
            if (ImGui.Button("Export to CSV"))
            {
                Database.Export();
            }

            ImGui.SameLine();

            if (ImGui.Button("Import from CSV"))
            {
                isFileDialogOpen = true;
                Plugin.FileDialogManager.OpenFileDialog("Select a CSV File", ".csv", (success, paths) =>
                {
                    if (success && paths.Count > 0)
                    {
                        var path = paths.First();
                        var ok = Database.Import(path);
                        if (ok)
                        {
                            Plugin.ChatGui.Print("Imported successfully.");
                        }
                        else
                        {
                            Plugin.ChatGui.PrintError("Failed to import.");
                        }
                    }
                    isFileDialogOpen = false;
                }, 1, Plugin.PluginInterface.GetPluginConfigDirectory(), false);

            }

            if (isFileDialogOpen)
            {
                Plugin.FileDialogManager.Draw();
            }

            ImGui.Spacing();

            var autoArchive = Plugin.Configuration.ArchiveOldEntries;
            if (ImGui.Checkbox("Enable Auto Archive", ref autoArchive))
            {
                Plugin.Configuration.ArchiveOldEntries = autoArchive;
                Plugin.Configuration.Save();
            }

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Do not enable this unless very limited storage.");
            }

            if (autoArchive)
            {
                ImGui.Spacing();

                var archiveLimit = Plugin.Configuration.ArchiveWhenEntriesExceed;
                if (ImGui.InputInt("Max Number of Active Entries", ref archiveLimit))
                {
                    Plugin.Configuration.ArchiveWhenEntriesExceed = archiveLimit;
                    Plugin.Configuration.Save();
                }

                ImGui.Spacing();

                var archiveKeep = Plugin.Configuration.ArchiveKeepEntries;
                if (ImGui.InputInt("Keep Newest", ref archiveKeep))
                {
                    Plugin.Configuration.ArchiveKeepEntries = archiveKeep;
                    Plugin.Configuration.Save();
                }

            }

            ImGui.Spacing();

            if (Plugin.Configuration.EnableDeleteAll)
            {
                if (ImGui.Button("Delete ALL Active Entries"))
                {
                    ImGui.OpenPopup("Confirm Delete ALL");
                }

                if (ImGui.BeginPopupModal("Confirm Delete ALL"))
                {
                    ImGuiHelpers.SafeTextWrapped("Are you sure you want to delete ALL? This action is irreversible.");
                    ImGui.Separator();

                    if (ImGui.Button("Yes"))
                    {
                        Database.Reset();
                        ImGui.CloseCurrentPopup();
                    }
                    ImGui.SameLine();
                    if (ImGui.Button("No"))
                    {
                        ImGui.CloseCurrentPopup();
                    }
                    ImGui.EndPopup();
                }
            }

        }

        if (ImGui.CollapsingHeader("Advanced"))
        {
            var deleteAll = Plugin.Configuration.EnableDeleteAll;
            if (ImGui.Checkbox("Enable Delete at Data Tab", ref deleteAll))
            {
                Plugin.Configuration.EnableDeleteAll = deleteAll;
                Plugin.Configuration.Save();
            }

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Show the button to delete all active entries at Data Tab.");
            }
        }
    }

}
