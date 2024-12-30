using FFXIVClientStructs.FFXIV.Client.Game.Group;
using Lumina.Excel.Sheets;
using System;

namespace ContactsTracker;

public class DataEntry(string? territoryName, string? rouletteType, bool isCompleted)
{
    // Basic Logging
    public string? TerritoryName { get; set; } = territoryName;
    public string? RouletteType { get; set; } = rouletteType; // If joined by duty roulette
    public bool IsCompleted { get; set; } = isCompleted;
    public string Date { get; set; } = DateTime.Now.ToString("yyyy-MM-dd");
    public string beginAt { get; set; } = DateTime.Now.ToString("T");
    public string? endAt { get; set; } = null; // Null when not completed like disconnection etc.
    public string? jobName { get; set; } = null; // Shouldn't be null
    // Additional Info: Contacts
    public string? partyMembers { get; set; } = null; // flatten this to string so that it can be saved to CSV
    // Additional Info: Comments
    public string comment { get; set; } = ""; // User's comment

    public static DataEntry? Instance { get; private set; }

    public static void Initialize(string? territoryName = null, string? rouletteType = null, bool isCompleted = false)
    {
        Instance = new DataEntry(territoryName, rouletteType, isCompleted);
    }

    public static void Initialize(DataEntry _dataEntry)
    {
        Instance = _dataEntry;
    }

    public static unsafe void finalize(Configuration configuration)
    {
        if (Instance == null)
        {
            return;
        }

        var localPlayer = Plugin.ClientState.LocalPlayer;
        if (localPlayer != null)
        {
            Instance.jobName = Plugin.UpperFirst(localPlayer.ClassJob.Value.Name.ToString());
        }
        Instance.endAt = DateTime.Now.ToString("T");

        if (configuration.EnableLogParty == false)
        {
            Instance.partyMembers = "Party Logging Disabled";
        }
        else
        {
            var numOfParty = Plugin.PartyList.Length;

            if (numOfParty == 0)
            {
                if (configuration.RecordSolo == false)
                {
                    Reset();
                    return;
                }
                Instance.partyMembers = "Solo";
            }

            if (numOfParty > 1 && numOfParty <= 8) // TODO: Alliance Support
            {
                var groupManager = GroupManager.Instance()->GetGroup();
                var names = new string[numOfParty];
                for (var i = 0; i < numOfParty; i++)
                {
                    var partyMember = Plugin.PartyList.CreatePartyMemberReference(Plugin.PartyList.GetPartyMemberAddress(i));
                    if (partyMember != null)
                    {
                        var worldID = groupManager->GetPartyMemberByContentId((ulong)partyMember.ContentId)->HomeWorld;
                        var worldName = Plugin.DataManager.GetExcelSheet<World>()?.GetRow(worldID).Name.ToString();
                        names[i] = partyMember.Name.ToString() + " @ " + worldName;
                    }
                }
                foreach (var name in names)
                {
                    Instance.partyMembers += name + " | "; // Delimiter other than comma to avoid CSV confusion
                }
            }
        }

        Instance.TerritoryName = Plugin.UpperFirst(Instance.TerritoryName!);

        Database.InsertEntry(Instance);

        Reset();
    }

    public static void Reset()
    {
        Instance = null;
    }

}
