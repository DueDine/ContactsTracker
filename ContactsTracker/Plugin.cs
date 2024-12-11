using ContactsTracker.Windows;
using Dalamud.Game.Command;
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
        ClientState.Login += OnLogon;
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
        ClientState.Login -= OnLogon;
        DutyState.DutyCompleted -= OnDutyCompleted;
    }

    private void OnCommand(string command, string args)
    {
        ToggleMainUI();
    }


    private void OnLogon()
    {
        if (ClientState.IsLoggedIn)
        {
            var territoryName = DataManager.GetExcelSheet<TerritoryType>()?.GetRow(ClientState.TerritoryType).Name.ToString();
            if (!string.IsNullOrEmpty(territoryName))
            {
                Logger.Debug("Try to recover previous entry");
                var entry = Database.LoadFromTempPath();
                if (entry != null)
                {
                    Logger.Debug("Recovered");
                    DataEntry.Initialize(entry);
                }
                else
                {
                    Logger.Debug("No previous entry found");
                }
            }
        }
    }

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

        Logger.Debug("Territory Changed");
        var newTerritory = DataManager.GetExcelSheet<TerritoryType>()?.GetRow(territoryID).ContentFinderCondition.Value;
        var territoryName = newTerritory?.Name.ToString();
        Logger.Debug("New Territory: " + (string.IsNullOrEmpty(territoryName) ? "Non Battle Area" : territoryName));

        if (DataEntry.Instance == null)
        {
            if (!string.IsNullOrEmpty(territoryName))
            {
                Logger.Debug("New Entry");
                if (Configuration.OnlyDutyRoulette)
                {
                    Logger.Debug("Duty Roulette Only Mode");
                    return;
                }
                DataEntry.Initialize();
            }
            else
            {
                Logger.Debug("Non Battle Area");
                return;
            }
        }

        if (DataEntry.Instance!.TerritoryName == null)
        {
            DataEntry.Instance.TerritoryName = territoryName;
            var localPlayer = ClientState.LocalPlayer;
            if (localPlayer != null)
            {
                DataEntry.Instance.jobName = localPlayer.ClassJob.Value.Name.ToString() + " Level: " + localPlayer.Level;
            }
        }
        else if (DataEntry.Instance.TerritoryName == territoryName)
        {
            Logger.Debug("Territory Unchanged: " + territoryName);
        }
        else
        {
            Logger.Debug("Territory Changed: " + DataEntry.Instance.TerritoryName + " -> " + territoryName);
            Logger.Debug("Force Finalizing Previous Entry");
            DataEntry.finalize(Configuration);
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

        Database.Archive(Configuration);
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
            DataEntry.Reset(); // Maybe not needed
            DataEntry.Initialize(null, type);
            Logger.Debug("Roulette Type: " + type);
        }
    }

    private void DrawUI() => WindowSystem.Draw();

    public void ToggleMainUI() => MainWindow.Toggle();
}
