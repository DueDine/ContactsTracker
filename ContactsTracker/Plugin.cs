using ContactsTracker.Windows;
using ContactsTracker.Data;
using ContactsTracker.Logic;
using Dalamud.Game.Command;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

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

    private readonly Handler handler;

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        MainWindow = new MainWindow(this);
        AnalyzeWindow = new AnalyzeWindow(this);

        WindowSystem.AddWindow(MainWindow);
        WindowSystem.AddWindow(AnalyzeWindow);

        handler = new Handler(
            Configuration,
            ClientState,
            DutyState,
            DataManager,
            ChatGui,
            Condition);

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
    }

    public void Dispose()
    {
        WindowSystem.RemoveAllWindows();
        MainWindow.Dispose();
        handler.Dispose();
        CommandManager.RemoveHandler(CommandName);
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
