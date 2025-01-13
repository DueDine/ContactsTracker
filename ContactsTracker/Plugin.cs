using ContactsTracker.Windows;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Command;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.Sheets;
using System;

namespace ContactsTracker;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IDutyState DutyState { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IPluginLog Logger { get; private set; } = null!;
    [PluginService] internal static IPartyList PartyList { get; private set; } = null!;
    [PluginService] internal static IKeyState KeyState { get; private set; } = null!;
    [PluginService] internal static ICondition Condition { get; private set; } = null!;

    private const string CommandName = "/ctracker";

    public Configuration Configuration { get; init; }

    public readonly WindowSystem WindowSystem = new("ContactsTracker");
    private MainWindow MainWindow { get; init; }
    private AnalyzeWindow AnalyzeWindow { get; init; }

    public readonly FileDialogManager FileDialogManager = new();

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        MainWindow = new MainWindow(this);
        AnalyzeWindow = new AnalyzeWindow(this);

        WindowSystem.AddWindow(MainWindow);
        WindowSystem.AddWindow(AnalyzeWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open Plugin\n" +
                          "/ctracker analyze -> AnalyzeWindow\n" +
                          "/ctracker reload -> Reload Database"
        });

        PluginInterface.UiBuilder.Draw += DrawUI;

        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUI;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleAnalyzeUI;

        Database.Load();

        ClientState.TerritoryChanged += OnTerritoryChanged;
        ClientState.CfPop += OnCfPop;
        ClientState.Logout += OnLogout;
        DutyState.DutyStarted += OnDutyStarted;
        DutyState.DutyCompleted += OnDutyCompleted;
    }

    public void Dispose()
    {
        WindowSystem.RemoveAllWindows();

        MainWindow.Dispose();

        CommandManager.RemoveHandler(CommandName);

        ClientState.TerritoryChanged -= OnTerritoryChanged;
        ClientState.CfPop -= OnCfPop;
        ClientState.Logout -= OnLogout;
        DutyState.DutyStarted -= OnDutyStarted;
        DutyState.DutyCompleted -= OnDutyCompleted;
    }

    private void OnCommand(string command, string args)
    {
        if (command is CommandName)
        {
            switch (args)
            {
                case "analyze":
                    AnalyzeWindow.Toggle();
                    break;
                case "reload":
                    Database.Load();
                    break;
                default:
                    MainWindow.Toggle();
                    break;
            }
        }
    }

    private void OnLogout(int type, int code)
    {
        if (DataEntry.Instance != null && DataEntry.Instance.IsCompleted == false && !string.IsNullOrEmpty(DataEntry.Instance.TerritoryName))
        {
            Database.SaveInProgressEntry(DataEntry.Instance);
        }
    }

    private void OnTerritoryChanged(ushort territoryID)
    {
        if (Configuration.EnableLogging == false)
        {
            return;
        }

        if (Condition[ConditionFlag.DutyRecorderPlayback])
        {
            return;
        }

        var newTerritory = DataManager.GetExcelSheet<TerritoryType>()?.GetRow(territoryID).ContentFinderCondition.Value;
        var territoryName = newTerritory?.Name.ExtractText();

        if (Database.isDirty)
        {
            if (!string.IsNullOrEmpty(territoryName))
            {
                var entry = Database.LoadFromTempPath();
                if (entry != null && entry.TerritoryName == territoryName)
                {
                    DataEntry.Initialize(entry);
                }
                else
                {
                    // ignore
                }
                Database.isDirty = false; // False whatever the result
            }
            return;
        }

        if (DataEntry.Instance == null)
        {
            if (!string.IsNullOrEmpty(territoryName))
            {
                if (Configuration.OnlyDutyRoulette) // If DR, already initialized by CFPop
                {
                    return;
                }
                DataEntry.Initialize(null, null, false);
            }
            else
            {
                return;
            }
        }

        if (DataEntry.Instance!.TerritoryName == null)
        {
            DataEntry.Instance.TerritoryName = territoryName;
            DataEntry.Instance.beginAt = DateTime.Now.ToString("T"); // Refresh
            var localPlayer = ClientState.LocalPlayer;
            if (localPlayer != null) // Seems like Player available after TerritoryChanged. Have to rely OnDutyCompleted.
            {
                DataEntry.Instance.jobName = UpperFirst(localPlayer.ClassJob.Value.Name.ExtractText());
            }
        }
        else if (DataEntry.Instance.TerritoryName == territoryName) // Intened to handle rejoin. 
        {
        }
        else
        {
            if (Configuration.KeepIncompleteEntry)
            {
                DataEntry.finalize(Configuration);
            }
            else
            {
                DataEntry.Reset();
            }
        }

    }

    private void OnDutyStarted(object? sender, ushort territoryID)
    {
        if (Configuration.EnableLogging == false)
        {
            return;
        }


        if (DataEntry.Instance != null)
        {
            var territoryName = DataManager.GetExcelSheet<TerritoryType>()?.GetRow(territoryID).ContentFinderCondition.Value.Name.ExtractText();
            if (DataEntry.Instance.TerritoryName == territoryName)
            {
                DataEntry.Instance.beginAt = DateTime.Now.ToString("T"); // More accurate
            }
        }
        return;
    }

    private void OnDutyCompleted(object? sender, ushort territoryID)
    {
        if (Configuration.EnableLogging == false)
        {
            return;
        }


        if (DataEntry.Instance == null)
        {
            return;
        }

        DataEntry.Instance.IsCompleted = true;
        DataEntry.finalize(Configuration);

        if (Configuration.PrintToChat)
        {
            ChatGui.Print("ContactsTracker Record Completed");
        }

        if (Database.Entries.Count >= Configuration.ArchiveWhenEntriesExceed)
        {
            Database.Archive(Configuration);
        }
    }

    private unsafe void OnCfPop(ContentFinderCondition condition)
    {
        if (Configuration.EnableLogging == false)
        {
            return;
        }


        var queueInfo = ContentsFinder.Instance()->QueueInfo;
        if (queueInfo.PoppedContentType == ContentsFinderQueueInfo.PoppedContentTypes.Roulette)
        {
            var type = DataManager.GetExcelSheet<ContentRoulette>()?.GetRow(queueInfo.PoppedContentId).Name.ExtractText();
            DataEntry.Reset(); // Reset. Some may choose to abandon the roulette
            DataEntry.Initialize(null, type, false);
        }
        else
        {
            DataEntry.Reset(); // Handle case where user DR -> Abandon -> Non DR vice versa
            // DataEntry.Initialize(null, "Normal", false);
        }
    }

    private void DrawUI() => WindowSystem.Draw();

    public void ToggleMainUI() => MainWindow.Toggle();
    public void ToggleAnalyzeUI() => AnalyzeWindow.Toggle();

    public static string UpperFirst(string s) // why is this not a built-in function in C#?
    {
        if (string.IsNullOrEmpty(s))
        {
            return string.Empty;
        }
        var a = s.ToCharArray();
        a[0] = char.ToUpper(a[0]);
        return new string(a);
    }
}
