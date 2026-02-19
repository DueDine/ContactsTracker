using ContactsTracker.Data;
using FFXIVClientStructs.FFXIV.Client.Game.Group;
using System;
using System.Linq;

namespace ContactsTracker.Logic;

public static class EntryLogic
{
    public static unsafe void EndRecord(DataEntryV2? entry, Configuration configuration)
    {
        if (entry == null) return;

        if (Plugin.ObjectTable.LocalPlayer is not { } localPlayer) return;

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
                var groupManagerInstance = GroupManager.Instance();
                var group = groupManagerInstance != null ? groupManagerInstance->GetGroup() : null;
                var names = new string[numOfParty];

                for (var i = 0; i < numOfParty; i++)
                {
                    var partyMember = Plugin.PartyList[i];
                    if (partyMember == null) continue;

                    var worldName = string.Empty;
                    if (group != null)
                    {
                        var memberInGroup = group->GetPartyMemberByContentId((ulong)partyMember.ContentId);
                        if (memberInGroup != null && memberInGroup->HomeWorld != 0)
                        {
                            worldName = ExcelHelper.GetWorldName(memberInGroup->HomeWorld);
                        }
                    }

                    names[i] = string.IsNullOrEmpty(worldName)
                        ? $"{partyMember.Name}"
                        : $"{partyMember.Name} @ {worldName}";

                    if (configuration.LogPartyClass)
                    {
                        var jobName = partyMember.ClassJob.Value.Abbreviation.ExtractText();
                        names[i] += $" ({jobName})";
                    }
                }
                var namesToAdd = names
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Except(entry.PartyMembers, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                entry.PartyMembers.AddRange(namesToAdd);
            }
        }
        DatabaseV2.InsertEntry(entry);
        DataEntryV2.Reset();
    }

    public static void EarlyEndRecord(Configuration configuration)
    {
        if (DataEntryV2.Instance == null) return;
        DataEntryV2.Instance.EndAt = DateTime.Now;
        DatabaseV2.InsertEntry(DataEntryV2.Instance);
        DataEntryV2.Reset();
    }

    public static unsafe void StartRecord(DataEntryV2 entry, Configuration configuration)
    {
        if (Plugin.ObjectTable.LocalPlayer is not { } localPlayer) return;

        entry.PlayerJobAbbr = localPlayer.ClassJob.Value.Abbreviation.ExtractText();
        entry.BeginAt = DateTime.Now;

        if (!configuration.EnableLogParty) return;
        if (Plugin.PartyList is not { Length: > 1 } partyList) return;

        var numOfParty = partyList.Length;
        var names = new string[numOfParty];
        var groupManagerInstance = GroupManager.Instance();
        var group = groupManagerInstance != null ? groupManagerInstance->GetGroup() : null;

        for (var i = 0; i < numOfParty; i++)
        {
            var partyMember = Plugin.PartyList[i];
            if (partyMember == null) continue;

            var worldName = string.Empty;
            if (group != null)
            {
                var memberInGroup = group->GetPartyMemberByContentId((ulong)partyMember.ContentId);
                if (memberInGroup != null && memberInGroup->HomeWorld != 0)
                {
                    worldName = ExcelHelper.GetWorldName(memberInGroup->HomeWorld);
                }
            }

            names[i] = string.IsNullOrEmpty(worldName)
                ? $"{partyMember.Name}"
                : $"{partyMember.Name} @ {worldName}";
                
            if (configuration.LogPartyClass)
            {
                var jobName = partyMember.ClassJob.Value.Abbreviation.ExtractText();
                names[i] += $" ({jobName})";
            }
        }
        entry.PartyMembers.AddRange(names.Where(name => !string.IsNullOrWhiteSpace(name)));
    }
}
