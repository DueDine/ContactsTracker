using Dalamud.Interface.Windowing;
using ImGuiNET;
using System;
using System.Numerics;

namespace ContactsTracker.Windows;

public class MainWindow : Window, IDisposable
{
    private Plugin Plugin;
    private int selectedTab = 0;
    private string commentBuffer = string.Empty;

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
            if (ImGui.BeginTabItem("Main"))
            {
                DrawMainTab();
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("Settings"))
            {
                DrawSettingsTab();
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("Data"))
            {
                DrawDataTab();
                ImGui.EndTabItem();
            }
            ImGui.EndTabBar();
        }
    }

    // TODO: Filter / Search by any field
    private void DrawMainTab()
    {
        if (Plugin.Configuration.EnableLogging == false)
        {
            ImGui.Text("Logging Disabled. You can only view previous entries.");
            ImGui.Spacing();
        }

        var entries = Database.Entries;
        if (entries.Count == 0)
        {
            ImGui.Text("No record yet.");
            return;
        }

        if (ImGui.BeginCombo("Select Entry", $"{entries[selectedTab].TerritoryName} - {entries[selectedTab].Date} - {entries[selectedTab].beginAt}"))
        {
            for (var i = 0; i < entries.Count; i++)
            {
                var isSelected = selectedTab == i;
                if (ImGui.Selectable($"{entries[i].TerritoryName} - {entries[i].beginAt}", selectedTab == i))
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
        ImGui.Text($"Roulette Type: {(string.IsNullOrEmpty(entry.RouletteType) ? "N/A" : entry.RouletteType)}");
        ImGui.Spacing();
        ImGui.Text($"Completed?: {(entry.IsCompleted ? "Yes" : "No")}");
        ImGui.Spacing();
        ImGui.Text($"Date: {entry.Date}");
        ImGui.Spacing();
        ImGui.Text($"Start: {entry.beginAt}");
        ImGui.Spacing();
        ImGui.Text($"End: {(string.IsNullOrEmpty(entry.endAt) ? "N/A" : entry.endAt)}");
        ImGui.Spacing();
        ImGui.Text($"Job: {entry.jobName}");
        ImGui.Spacing();

        ImGui.Text("Party Members:");
        if (entry.partyMembers == null)
        {
            ImGui.Text("Solo");
        }
        else
        {
            foreach (var member in entry.partyMembers)
            {
                ImGui.BulletText(member);
            }
        }
        ImGui.Spacing();

        commentBuffer = entry.comment;
        if (ImGui.InputTextMultiline("Comment", ref commentBuffer, 256, new Vector2(0, 100)))
        {
            entry.comment = commentBuffer;
            Database.Save();
        }
        ImGui.Spacing();

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
        var enableLogging = Plugin.Configuration.EnableLogging;
        if (ImGui.Checkbox("Enable Logging", ref enableLogging))
        {
            Plugin.Configuration.EnableLogging = enableLogging;
            Plugin.Configuration.Save();
        }

        var recordSolo = Plugin.Configuration.RecordSolo;
        if (ImGui.Checkbox("Record Solo", ref recordSolo))
        {
            Plugin.Configuration.RecordSolo = recordSolo;
            Plugin.Configuration.Save();
        }

        var printToChat = Plugin.Configuration.PrintToChat;
        if (ImGui.Checkbox("Print To Chat", ref printToChat))
        {
            Plugin.Configuration.PrintToChat = printToChat;
            Plugin.Configuration.Save();
        }

        var onlyDutyRoulette = Plugin.Configuration.OnlyDutyRoulette;
        if (ImGui.Checkbox("Only Record Duty Roulette", ref onlyDutyRoulette))
        {
            Plugin.Configuration.OnlyDutyRoulette = onlyDutyRoulette;
            Plugin.Configuration.Save();
        }
    }

    private static void DrawDataTab()
    {
        if (ImGui.Button("Export to CSV"))
        {
            Database.Export();
        }
    }
}
