using Dalamud.Game.ClientState.Party;
using System;

namespace ContactsTracker;

public class DataEntry(string? territoryName, string? rouletteType, bool isCompleted)
{
    public string? TerritoryName { get; set; } = territoryName;
    public string? RouletteType { get; set; } = rouletteType; // If joined by duty roulette
    public bool IsCompleted { get; set; } = isCompleted;
    public string Date { get; set; } = DateTime.Now.ToString("yyyy-MM-dd");
    public string beginAt { get; set; } = DateTime.Now.ToString("T");
    public string? endAt { get; set; } = null; // Null when not completed like disconnection etc.
    public string? jobName { get; set; } = null; // User's job
    public string[]? partyMembers { get; set; } = null; // Null when solo
    public string? comment { get; set; } = null; // User's comment

    public static DataEntry? Instance { get; private set; }

    public static void Initialize(string? territoryName = null, string? rouletteType = null, bool isCompleted = false)
    {
        Instance = new DataEntry(territoryName, rouletteType, isCompleted);
    }

    public static void Initialize(DataEntry _dataEntry)
    {
        Instance = _dataEntry;
    }

    public static void finalize()
    {
        if (Instance == null)
        {
            return;
        }

        Instance.jobName = Plugin.ClientState.LocalPlayer?.ClassJob.Value.Name.ToString();
        Instance.endAt = DateTime.Now.ToString("T");

        var numOfParty = Plugin.PartyList.Length;
        if (numOfParty > 1 && numOfParty <= 8) // Not Alliance
        {
            string[] names = new string[numOfParty];
            for (int i = 0; i < numOfParty; i++)
            {
                IPartyMember? partyMember = Plugin.PartyList.CreatePartyMemberReference(Plugin.PartyList.GetPartyMemberAddress(i));
                names[i] = (partyMember?.Name.ToString()+ " @ " + partyMember?.World.Value.Name.ToString()) ?? "Unknown";
            }
            Instance.partyMembers = names;
        }

        Database.InsertEntry(Instance);

        Instance = null; // Reset
    }

    public static void Reset()
    {
        Instance = null;
    }

}
