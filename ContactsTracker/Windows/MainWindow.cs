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
        if (ImGui.BeginTabBar("MainTabBar"))
        {
            if (ImGui.BeginTabItem("Active"))
            {
                DrawActiveTab();
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("History"))
            {
                DrawHistoryTab();
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("Settings"))
            {
                DrawSettingsTab();
                ImGui.EndTabItem();
            }
            ImGui.EndTabBar();
        }
    }

    private void DrawActiveTab()
    {
        if (Plugin.Configuration.EnableLogging == false)
        {
            ImGui.Text("Logging Disabled. Read-Only Mode.");
            ImGui.Spacing();
        }

        var entries = Database.Entries;
        if (entries.Count == 0 && DataEntry.Instance == null)
        {
            ImGui.Text("No record yet.");
            return;
        }

        var entry = entries.LastOrDefault();
        if (DataEntry.Instance != null && !string.IsNullOrEmpty(DataEntry.Instance.TerritoryName))
        {
            entry = DataEntry.Instance;
            ImGui.Text("Currently Logging");
        }
        else
        {
            ImGui.Text("No Active Log. Please check the History Tab.");
            return;
        }
        ImGui.Spacing();
        ImGui.Text($"Place: {entry.TerritoryName}");
        ImGui.Spacing();
        ImGui.Text($"Join via: {(string.IsNullOrEmpty(entry.RouletteType) ? "Normal" : entry.RouletteType)}");
        ImGui.Spacing();
        ImGui.Text($"When: {entry.Date}");
        ImGui.SameLine();
        ImGui.Text($"{entry.beginAt} - {(string.IsNullOrEmpty(entry.endAt) ? "N/A" : entry.endAt)}");
        if (!string.IsNullOrEmpty(entry.endAt))
        {
            ImGui.Spacing();
            ImGui.Text($"Duration: {DateTime.Parse(entry.endAt).Subtract(DateTime.Parse(entry.beginAt)):hh\\:mm\\:ss}");
        }
        else if (Plugin.DutyState.IsDutyStarted) // Still in progress
        {
            ImGui.Spacing();
            ImGui.Text($"Duration: {DateTime.Now.Subtract(DateTime.Parse(entry.beginAt)):hh\\:mm\\:ss}");
        }
        else
        {
            ImGui.Spacing();
            ImGui.Text("If you just reconnect, duration will not display here.");
        }
        ImGui.Spacing();

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

    private void DrawHistoryTab()
    {
        var entries = Database.Entries;

        if (ImGui.Button("Enable / Disable Search Feature"))
        {
            enableSearch = !enableSearch;
        }

        if (enableSearch)
        {
            ImGui.Spacing();
            if (ImGui.InputText("Search", ref searchBuffer, 50))
            {
                selectedTab = 0; // Avoid out of range
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
            }
        }

        if (entries.Count == 0)
        {
            if (enableSearch)
            {
                ImGui.Text("No record found.");
            }
            else
            {
                ImGui.Text("No record yet.");
            }
            return;
        }

        if (ImGui.BeginCombo("Select Entry", $"{entries[selectedTab].TerritoryName} - {entries[selectedTab].Date} {entries[selectedTab].beginAt}"))
        {
            for (var i = 0; i < entries.Count; i++)
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
            ImGui.EndCombo();
        }

        var entry = entries[selectedTab];
        ImGui.Text($"Name: {entry.TerritoryName}");
        ImGui.Spacing();
        ImGui.Text($"Type: {(string.IsNullOrEmpty(entry.RouletteType) ? "Normal" : entry.RouletteType)}");
        ImGui.Spacing();
        ImGui.Text($"Completed?: {(entry.IsCompleted ? "Yes" : "No")}");
        ImGui.Spacing();
        ImGui.Text($"Date: {entry.Date}");
        ImGui.Spacing();
        ImGui.Text($"Time: {entry.beginAt} - {(string.IsNullOrEmpty(entry.endAt) ? "N/A" : entry.endAt)}");
        ImGui.Spacing();
        ImGui.Text($"Job: {entry.jobName}");
        ImGui.Spacing();

        ImGui.Text("Party Members:");
        if (entry.partyMembers == null)
        {
            ImGui.SameLine();
            ImGui.Text("N/A");
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

        /*
         * Temporarily disabled until I figure out the UI
        commentBuffer = entry.comment;
        if (ImGui.InputTextMultiline("Comment", ref commentBuffer, 512, new Vector2(0, 100)))
        {
            entry.comment = commentBuffer;
            Database.Save();
        }
        ImGui.Spacing();
        */

        if (ImGui.Button("Delete Entry"))
        {
            ImGui.OpenPopup("Confirm Deletion"); // Double check
        }

        if (ImGui.BeginPopupModal("Confirm Deletion"))
        {
            ImGui.Text("Are you sure you want to delete this entry?");
            ImGui.Separator();

            if (ImGui.Button("Yes"))
            {
                entries.RemoveAt(selectedTab);
                Database.Save();

                selectedTab = Math.Max(0, selectedTab - 1); // Avoid out of range
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
                    ImGui.Text("Are you sure you want to delete ALL? This action is irreversible.");
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
