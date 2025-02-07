using ContactsTracker.Data;
using FFXIVClientStructs.FFXIV.Client.Game.Group;
using System;

namespace ContactsTracker.Logic;

public static class EntryLogic
{
    public static unsafe void EndRecord(DataEntryV2? entry, Configuration configuration)
    {
        if (entry == null) return;

        if (Plugin.ClientState.LocalPlayer is not { } localPlayer) return;

        entry.PlayerJobAbbr = localPlayer.ClassJob.Value.Abbreviation.ExtractText();
        entry.EndAt = DateTime.Now;

        if (!configuration.EnableLogParty)
        {
            entry.PartyMembers.Add("Party Logging Disabled For this Entry");
        }
        else
        {
            var numOfParty = Plugin.PartyList.Length;
            if (numOfParty == 0)
            {
                if (!configuration.RecordSolo)
                {
                    DataEntryV2.Reset();
                    return;
                }
                entry.PartyMembers.Add("Solo");
            }

            if (numOfParty > 1 && numOfParty <= 8)
            {
                var groupManager = GroupManager.Instance()->GetGroup();
                var names = new string[numOfParty];

                for (var i = 0; i < numOfParty; i++)
                {
                    var partyMember = Plugin.PartyList[i];
                    if (partyMember == null) continue;

                    var worldID = groupManager->GetPartyMemberByContentId((ulong)partyMember.ContentId)->HomeWorld;
                    var worldName = ExcelHelper.GetWorldName(worldID);
                    names[i] = $"{partyMember.Name} @ {worldName}";
                    if (configuration.LogPartyClass)
                    {
                        var jobName = partyMember.ClassJob.Value.Abbreviation.ExtractText();
                        names[i] += $" ({jobName})";
                    }

                    entry.PartyMembers.Add(names[i]);
                }
            }
        }
        DatabaseV2.InsertEntry(entry);
        DataEntryV2.Reset();
    }

    public static void EarlyEndRecord(Configuration configuration)
    {
        if (DataEntryV2.Instance == null) return;
        DatabaseV2.InsertEntry(DataEntryV2.Instance);
        DataEntryV2.Reset();
    }
}
