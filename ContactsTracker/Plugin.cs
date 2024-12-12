using ContactsTracker.Windows;
using Dalamud.Game.Command;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.Sheets;

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

    private const string CommandName = "/ctracker";

    public Configuration Configuration { get; init; }

    public readonly WindowSystem WindowSystem = new("ContactsTracker");
    private MainWindow MainWindow { get; init; }
    
    public readonly FileDialogManager FileDialogManager = new();

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        MainWindow = new MainWindow(this);

        WindowSystem.AddWindow(MainWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open Plugin"
        });

        PluginInterface.UiBuilder.Draw += DrawUI;

        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUI;

        Database.Load();

        //
        // Logger.Debug($"Info: {ClientState.LocalContentId}"); TODO: Character -> CID Mapping
        //

        ClientState.TerritoryChanged += OnTerritoryChanged;
        ClientState.CfPop += OnCfPop;
        ClientState.Logout += OnLogout;
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
        DutyState.DutyCompleted -= OnDutyCompleted;
    }

    private void OnCommand(string command, string args)
    {
        ToggleMainUI();
    }

    /*
    private void OnLogon()
    {
        if (DutyState.IsDutyStarted && ClientState.IsLoggedIn)
        {
            Logger.Debug("User reconnected. Prepare to recover.");
            var territoryName = DataManager.GetExcelSheet<TerritoryType>()?.GetRow(ClientState.TerritoryType).ContentFinderCondition.Value.Name.ToString();
            if (!string.IsNullOrEmpty(territoryName))
            {
                Logger.Debug("Try to recover previous entry");
                var entry = Database.LoadFromTempPath();
                if (entry != null && entry.TerritoryName == territoryName)
                {
                    Logger.Debug("Recovered");
                    DataEntry.Initialize(entry);
                }
                else
                {
                    Logger.Debug("TerritoryName mismatch. Ignore it.");
                }
            }
        }
    }
    */

    private void OnLogout(int type, int code)
    {
        if (DataEntry.Instance != null && DataEntry.Instance.IsCompleted == false && !string.IsNullOrEmpty(DataEntry.Instance.TerritoryName))
        {
            Logger.Debug("User disconnected. Saving the record.");
            Database.SaveInProgressEntry(DataEntry.Instance);
        }
    }

    private void OnTerritoryChanged(ushort territoryID)
    {
        if (Configuration.EnableLogging == false)
        {
            return;
        }

        var newTerritory = DataManager.GetExcelSheet<TerritoryType>()?.GetRow(territoryID).ContentFinderCondition.Value;
        var territoryName = newTerritory?.Name.ToString();

        if (Database.isDirty)
        {
            Logger.Debug("User reconnected. Prepare to recover.");
            if (!string.IsNullOrEmpty(territoryName))
            {
                Logger.Debug("Try to recover previous entry");
                var entry = Database.LoadFromTempPath();
                if (entry != null && entry.TerritoryName == territoryName)
                {
                    Logger.Debug("Recovered");
                    DataEntry.Initialize(entry);
                }
                else
                {
                    Logger.Debug("TerritoryName mismatch. Ignore it.");
                }
                Database.isDirty = false; // False whatever the result
            }
            return;
        }

        Logger.Debug("Territory Changed");
        Logger.Debug("New Territory: " + (string.IsNullOrEmpty(territoryName) ? "Non Battle Area" : territoryName));

        if (DataEntry.Instance == null)
        {
            if (!string.IsNullOrEmpty(territoryName))
            {
                if (Configuration.OnlyDutyRoulette) // If DR, already initialized by CFPop
                {
                    Logger.Debug("Duty Roulette Only Mode. No New Entry.");
                    return;
                }
                Logger.Debug("New Entry");
                DataEntry.Initialize();
            }
            else
            {
                Logger.Debug("Non Battle Area -> No New Entry.");
                return;
            }
        }

        if (DataEntry.Instance!.TerritoryName == null)
        {
            DataEntry.Instance.TerritoryName = territoryName;
            var localPlayer = ClientState.LocalPlayer;
            if (localPlayer != null) // This should be always true but just in case
            {
                DataEntry.Instance.jobName = UpperFirst(localPlayer.ClassJob.Value.Name.ToString());
            }
        }
        else if (DataEntry.Instance.TerritoryName == territoryName) // Intened to handle rejoin. 
        {
            Logger.Debug("Territory Unchanged: " + territoryName);
        }
        else
        {
            Logger.Debug("Territory Changed: " + DataEntry.Instance.TerritoryName + " -> " + (string.IsNullOrEmpty(territoryName) ? "Non Battle Area" : territoryName));
            Logger.Debug("Force Finalizing Previous Entry");
            if (Configuration.KeepIncompleteEntry)
            {
                DataEntry.finalize(Configuration);
            }
            else
            {
                Logger.Debug("Do not keep Incomplete. Ignore this.");
                DataEntry.Reset();
            }
        }

    }


    private void OnDutyCompleted(object? sender, ushort territoryID)
    {
        if (Configuration.EnableLogging == false)
        {
            return;
        }

        Logger.Debug("Duty Completed: " + territoryID);

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

        Logger.Debug("Finder Pop");

        var queueInfo = ContentsFinder.Instance()->QueueInfo;
        if (queueInfo.PoppedContentType == ContentsFinderQueueInfo.PoppedContentTypes.Roulette)
        {
            Logger.Debug("Roulette Mode");
            var type = DataManager.GetExcelSheet<ContentRoulette>()?.GetRow(queueInfo.PoppedContentId).Name.ToString();
            DataEntry.Reset(); // Reset. Some may choose to abandon the roulette
            DataEntry.Initialize(null, type);
            Logger.Debug("Roulette Type: " + type);
        }
        else
        {
            Logger.Debug("Non Roulette Mode");
            DataEntry.Reset(); // Handle case where user DR -> Abandon -> Non DR vice versa
        }
    }

    private void DrawUI() => WindowSystem.Draw();

    public void ToggleMainUI() => MainWindow.Toggle();

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
