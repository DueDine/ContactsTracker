using System.IO;
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
            HelpMessage = "Open Main Window"
        });

        PluginInterface.UiBuilder.Draw += DrawUI;

        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUI;

        Database.Load();

        ClientState.TerritoryChanged += OnTerritoryChanged;
        ClientState.CfPop += OnCfPop;
        DutyState.DutyCompleted += OnDutyCompleted;
    }

    public void Dispose()
    {
        WindowSystem.RemoveAllWindows();

        MainWindow.Dispose();

        CommandManager.RemoveHandler(CommandName);

        ClientState.TerritoryChanged -= OnTerritoryChanged;
        ClientState.CfPop -= OnCfPop;
        DutyState.DutyCompleted -= OnDutyCompleted;
    }

    private void OnCommand(string command, string args)
    {
        ToggleMainUI();
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
        Logger.Debug("New Territory: " + newTerritory);

        if (DataEntry.Instance == null)
        {
            DataEntry.Initialize();
        }
        
        if (DataEntry.Instance!.TerritoryName == null)
        {
            DataEntry.Instance.TerritoryName = territoryName;
        }
        else
        {
            Logger.Debug("Territory Changed: " + DataEntry.Instance.TerritoryName + " -> " + territoryName);
            Logger.Debug("Force Finalizing Previous Entry");
            DataEntry.finalize();
            DataEntry.Initialize(territoryName);
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
        DataEntry.finalize();

        ChatGui.Print("Record Completed");
    }

    private unsafe void OnCfPop(ContentFinderCondition condition)
    {
        if (Configuration.EnableLogging == false)
        {
            return;
        }

        Logger.Debug("CF Pop");

        var queueInfo = ContentsFinder.Instance()->QueueInfo;
        if (queueInfo.PoppedContentType == ContentsFinderQueueInfo.PoppedContentTypes.Roulette)
        {
            Logger.Debug("Roulette Mode " + queueInfo.PoppedContentType);
            var type = DataManager.GetExcelSheet<ContentRoulette>()?.GetRow(queueInfo.PoppedContentId).Name.ToString();
            DataEntry.Initialize(null, type);
            Logger.Debug("Roulette Type: " + type);
        }
    }

    private void DrawUI() => WindowSystem.Draw();

    public void ToggleMainUI() => MainWindow.Toggle();
}
