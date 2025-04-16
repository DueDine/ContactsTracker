using ContactsTracker.Data;
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

namespace ContactsTracker.Windows;

public class MainWindow : Window, IDisposable
{
    private Plugin Plugin;
    private int selectedTab = 0;
    private bool isFileDialogOpen = false;
    private bool doubleCheck = false;

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
        using (var aboutTab = ImRaii.TabItem("About"))
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
            ImGuiHelpers.SafeTextWrapped("Logging Disabled. Read-Only Mode.");
            return;
        }

        var entries = DatabaseV2.Entries;
        if (entries.Count == 0 && DataEntryV2.Instance == null)
        {
            ImGuiHelpers.SafeTextWrapped("No record yet.");
            return;
        }

        var entry = entries.LastOrDefault();
        if (DataEntryV2.Instance != null && DataEntryV2.Instance.TerritoryId != 0)
        {
            entry = DataEntryV2.Instance;
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
        ImGuiHelpers.SafeTextWrapped($"Place: {ExcelHelper.GetTerritoryName(entry.TerritoryId)}");
        ImGui.Spacing();
        ImGuiHelpers.SafeTextWrapped($"Join via: {ExcelHelper.GetPoppedContentType(entry.RouletteId)}");
        ImGui.Spacing();
        ImGuiHelpers.SafeTextWrapped($"{entry.BeginAt} - {(entry.EndAt == DateTime.MinValue ? "N/A" : entry.EndAt)}");

        if (entry.EndAt != DateTime.MinValue)
        {
            ImGui.Spacing();
            ImGuiHelpers.SafeTextWrapped($"Duration: {entry.EndAt.Subtract(entry.BeginAt):hh\\:mm\\:ss}");
        }
        else if (Plugin.DutyState.IsDutyStarted) // Still in progress
        {
            ImGui.Spacing();
            ImGuiHelpers.SafeTextWrapped($"Duration: {DateTime.Now.Subtract(entry.BeginAt):hh\\:mm\\:ss}");
        }
        ImGui.Spacing();

        if (DataEntryV2.Instance != null)
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
                    DataEntryV2.Reset();
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
        var entries = DatabaseV2.Entries;
        if (entries.Count == 0)
        {
            ImGuiHelpers.SafeTextWrapped("No record yet.");
            return;
        }

        ImGui.Columns(2, "HistoryColumns", true);

        using (var child = ImRaii.Child("Sidebar", new Vector2(0, 0), true))
        {
            if (!child) return;

            entries = [.. entries.OrderBy(entry => entry.BeginAt)];
            for (var i = entries.Count - 1; i >= 0; i--)
            {
                var isSelected = selectedTab == i;
                if (ImGui.Selectable($"{ExcelHelper.GetTerritoryName(entries[i].TerritoryId)} - {entries[i].BeginAt:yyyy-MM-dd HH:mm:ss}", selectedTab == i))
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
            ImGuiHelpers.SafeTextWrapped($"Name: {ExcelHelper.GetTerritoryName(entry.TerritoryId)}");
            ImGui.Spacing();
            ImGuiHelpers.SafeTextWrapped($"Type: {ExcelHelper.GetPoppedContentType(entry.RouletteId)}");
            ImGui.Spacing();
            ImGuiHelpers.SafeTextWrapped($"Completed?: {(entry.IsCompleted ? "Yes" : "No")}");
            ImGui.Spacing();
            ImGuiHelpers.SafeTextWrapped($"Time: {entry.BeginAt:yyyy-MM-dd HH:mm:ss} - {(entry.EndAt == DateTime.MinValue ? "N/A" : entry.EndAt)}");
            ImGui.Spacing();
            ImGuiHelpers.SafeTextWrapped($"Job: {entry.PlayerJobAbbr}");
            ImGui.Spacing();

            ImGuiHelpers.SafeTextWrapped("Party Members:");
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

            if (ImGui.Button("Delete Entry"))
            {
                if (Plugin.KeyState[VirtualKey.CONTROL])
                {
                    DatabaseV2.RemoveEntry(entry);

                    if (selectedTab >= DatabaseV2.Count)
                    {
                        selectedTab = Math.Max(-1, DatabaseV2.Count - 1);
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

            if (Plugin.Configuration.EnableLogParty)
            {
                ImGui.SameLine();

                var logPartyClass = Plugin.Configuration.LogPartyClass;
                if (ImGui.Checkbox("Log Party Class", ref logPartyClass))
                {
                    Plugin.Configuration.LogPartyClass = logPartyClass;
                    Plugin.Configuration.Save();
                }

                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip("Also record their class.");
                }
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
                DatabaseV2.Export();
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
                        var ok = DatabaseV2.Import(path);
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
                        DatabaseV2.Reset();
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

            ImGuiHelpers.ScaledDummy(5f);
            ImGuiHelpers.SafeTextWrapped($"Configuration Version: {Plugin.Configuration.Version}");
            ImGuiHelpers.SafeTextWrapped($"Database Version: {DatabaseV2.Version}");
            ImGuiHelpers.SafeTextWrapped($"Plugin Version: {Plugin.PluginInterface.Manifest.AssemblyVersion}");
        }
    }

    private static void DrawAboutTab()
    {
        ImGuiHelpers.ScaledDummy(5f);

        ImGui.TextColored(ImGuiColors.DalamudRed, "This plugin is in early development. Please report any bugs or suggestions to the developer.");

        ImGuiHelpers.ScaledDummy(2f);

        ImGui.TextColored(ImGuiColors.DalamudOrange, "Discord: @lamitt");
        ImGui.TextColored(ImGuiColors.DalamudOrange, "You can ping me at the Dalamud Discord server. Or open an issue at the GitHub repository.");

        ImGuiHelpers.ScaledDummy(5f);

        using (ImRaii.PushColor(ImGuiCol.Button, ImGuiColors.ParsedBlue))
        {
            if (ImGui.Button("GitHub Repository"))
            {
                Util.OpenLink("https://github.com/DueDine/ContactsTracker");
            }
        }
    }

}
