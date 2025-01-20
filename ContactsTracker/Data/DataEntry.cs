using FFXIVClientStructs.FFXIV.Client.Game.Group;
using System;

namespace ContactsTracker.Data;

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
            Instance.jobName = Plugin.UpperFirst(localPlayer.ClassJob.Value.Name.ExtractText());
        }
        Instance.endAt = DateTime.Now.ToString("T");

        if (configuration.EnableLogParty == false)
        {
            Instance.partyMembers = "Party Logging Disabled For this Entry";
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
                    var partyMember = Plugin.PartyList[i];
                    if (partyMember != null)
                    {
                        var worldID = groupManager->GetPartyMemberByContentId((ulong)partyMember.ContentId)->HomeWorld;
                        var worldName = ExcelHelper.GetWorldName(worldID);
                        names[i] = $"{partyMember.Name} @ {worldName}";
                        if (configuration.LogPartyClass)
                        {
                            var jobName = partyMember.ClassJob.Value.Abbreviation.ExtractText();
                            names[i] += $" ({jobName})";
                        }
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
