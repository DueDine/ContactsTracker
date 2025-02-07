using ContactsTracker.Data;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.Sheets;
using System;


namespace ContactsTracker.Logic;

public class Handler
{

#pragma warning disable IDE1006
    private readonly Configuration Configuration;
    private readonly IClientState ClientState;
    private readonly IDutyState DutyState;
    private readonly IDataManager DataManager;
    private readonly IChatGui ChatGui;
    private readonly ICondition Condition;
#pragma warning restore IDE1006

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
        if (DataEntryV2.Instance != null && DataEntryV2.Instance.IsCompleted == false && DataEntryV2.Instance.TerritoryId != 0)
        {
            DatabaseV2.SaveInProgressEntry(DataEntryV2.Instance);
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

        if (DatabaseV2.isDirty)
        {
            if (!string.IsNullOrEmpty(territoryName))
            {
                var entry = DatabaseV2.LoadInProgressEntry();
                if (entry != null && entry.TerritoryId == territoryID)
                {
                    DataEntryV2.Initialize(entry);
                }
                DatabaseV2.isDirty = false; // False whatever the result
            }
            return;
        }

        if (DataEntryV2.Instance == null)
        {
            if (!string.IsNullOrEmpty(territoryName))
            {
                if (Configuration.OnlyDutyRoulette) // If DR, already initialized by CFPop
                {
                    return;
                }
                DataEntryV2.Initialize(territoryID, 0);
            }
            else
            {
                return;
            }
        }

        if (DataEntryV2.Instance!.TerritoryId == 0)
        {
            DataEntryV2.Instance.TerritoryId = territoryID;
            DataEntryV2.Instance.BeginAt = DateTime.Now; // Refresh
            var localPlayer = ClientState.LocalPlayer;
            if (localPlayer != null)
            {
                DataEntryV2.Instance.PlayerJobAbbr = localPlayer.ClassJob.Value.Name.ExtractText();
            }
        }
        else if (DataEntryV2.Instance.TerritoryId == territoryID) // Intended to handle rejoin
        {
            // Intentionally do nothing
        }
        else
        {
            if (Configuration.KeepIncompleteEntry)
            {
                EntryLogic.EarlyEndRecord(Configuration);
            }
            else
            {
                DataEntryV2.Reset();
            }
        }
    }

    private void OnDutyStarted(object? sender, ushort territoryID)
    {
        if (Configuration.EnableLogging == false)
        {
            return;
        }

        if (DataEntryV2.Instance != null)
        {
            if (DataEntryV2.Instance.TerritoryId == territoryID)
            {
                DataEntryV2.Instance.BeginAt = DateTime.Now; // More accurate
            }
        }
    }

    private void OnDutyCompleted(object? sender, ushort territoryID)
    {
        if (Configuration.EnableLogging == false)
        {
            return;
        }

        if (DataEntryV2.Instance == null)
        {
            return;
        }

        DataEntryV2.Instance.IsCompleted = true;
        DataEntryV2.EndRecord(Configuration);

        if (Configuration.PrintToChat)
        {
            ChatGui.Print("Recorded", "ContactsTracker");
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
            DataEntryV2.Reset(); // Reset. Some may choose to abandon the roulette
            DataEntryV2.Initialize(0, queueInfo.PoppedContentId);
        }
        else
        {
            DataEntryV2.Reset(); // Handle case where user DR -> Abandon -> Non DR vice versa
        }
    }
}
