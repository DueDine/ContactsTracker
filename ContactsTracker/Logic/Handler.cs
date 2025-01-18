using ContactsTracker.Data;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.Sheets;
using System;


namespace ContactsTracker.Logic;

public class Handler
{
    private readonly Configuration Configuration;
    private readonly IClientState ClientState;
    private readonly IDutyState DutyState;
    private readonly IDataManager DataManager;
    private readonly IChatGui ChatGui;
    private readonly ICondition Condition;

    public Handler(
        Configuration configuration,
        IClientState clientState,
        IDutyState dutyState,
        IDataManager dataManager,
        IChatGui chatGui,
        ICondition condition)
    {
        Configuration = configuration;
        ClientState = clientState;
        DutyState = dutyState;
        DataManager = dataManager;
        ChatGui = chatGui;
        Condition = condition;

        ClientState.TerritoryChanged += OnTerritoryChanged;
        ClientState.CfPop += OnCfPop;
        ClientState.Logout += OnLogout;
        DutyState.DutyStarted += OnDutyStarted;
        DutyState.DutyCompleted += OnDutyCompleted;
    }

    public void Dispose()
    {
        ClientState.TerritoryChanged -= OnTerritoryChanged;
        ClientState.CfPop -= OnCfPop;
        ClientState.Logout -= OnLogout;
        DutyState.DutyStarted -= OnDutyStarted;
        DutyState.DutyCompleted -= OnDutyCompleted;
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

        var territoryName = ExcelHelper.GetTerritoryName(territoryID);

        if (Database.isDirty)
        {
            if (!string.IsNullOrEmpty(territoryName))
            {
                var entry = Database.LoadFromTempPath();
                if (entry != null && entry.TerritoryName == territoryName)
                {
                    DataEntry.Initialize(entry);
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
            if (localPlayer != null)
            {
                DataEntry.Instance.jobName = Plugin.UpperFirst(localPlayer.ClassJob.Value.Name.ExtractText());
            }
        }
        else if (DataEntry.Instance.TerritoryName == territoryName) // Intended to handle rejoin
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
            var territoryName = ExcelHelper.GetTerritoryName(territoryID);
            if (DataEntry.Instance.TerritoryName == territoryName)
            {
                DataEntry.Instance.beginAt = DateTime.Now.ToString("T"); // More accurate
            }
        }
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
            var type = ExcelHelper.GetPoppedContentType(queueInfo.PoppedContentId);
            DataEntry.Reset(); // Reset. Some may choose to abandon the roulette
            DataEntry.Initialize(null, type, false);
        }
        else
        {
            DataEntry.Reset(); // Handle case where user DR -> Abandon -> Non DR vice versa
        }
    }
}
