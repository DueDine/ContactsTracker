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
    public string[]? partyMembers { get; set; } = null; // Null when solo
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

    public static void finalize(Configuration configuration)
    {
        if (Instance == null)
        {
            return;
        }

        var localPlayer = Plugin.ClientState.LocalPlayer;
        if (localPlayer != null)
        {
            // At least Frontline allows changing job inside. Maybe use list?
            // DataEntry.Instance.jobName = localPlayer.ClassJob.Value.Name.ToString() + " Level: " + localPlayer.Level;
        }
        Instance.endAt = DateTime.Now.ToString("T");

        var numOfParty = Plugin.PartyList.Length;

        if (numOfParty == 0)
        {
            if (configuration.RecordSolo == false)
            {
                Plugin.Logger.Debug("Solo record is disabled. Ignoring the record.");
                Reset();
                return;
            }
            Instance.partyMembers = ["Solo"];
        }

        if (numOfParty > 1 && numOfParty <= 8) // TODO: Alliance Support
        {
            var names = new string[numOfParty];
            for (var i = 0; i < numOfParty; i++)
            {
                var partyMember = Plugin.PartyList.CreatePartyMemberReference(Plugin.PartyList.GetPartyMemberAddress(i));
                names[i] = (partyMember?.Name.ToString() + " @ " + partyMember?.World.Value.Name.ToString()) ?? "Unknown";
            }
            Instance.partyMembers = names;
        }

        Database.InsertEntry(Instance);

        Reset();
    }

    public static void Reset()
    {
        Instance = null;
    }

}
