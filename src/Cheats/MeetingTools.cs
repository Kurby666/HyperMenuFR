using System;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

namespace MalumMenu;

public static class MeetingTools
{
    private const int HandlingId = 20004;
    private static float _spamNext;

    public static void SpamMeetingsCheat()
    {
        if (!CheatToggles.spamMeetings) return;

        try
        {
            if (Time.unscaledTime < _spamNext) return;
            _spamNext = Time.unscaledTime + 0.15f;

            if (MeetingHud.Instance != null || Utils.isLobby || ShipStatus.Instance == null) return;

            PlayerControl me = PlayerControl.LocalPlayer;
            if (me == null || me.Data == null || me.Data.IsDead) return;

            try { me.RemainingEmergencies = 999999; }
            catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "MeetingTools.SpamMeetingsCheat: refill emergencies"); }

            if (Utils.isHost)
                Utilities.OpenMeeting(me, null);
            else
                me.CmdReportDeadBody(null);
        }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "MeetingTools.SpamMeetingsCheat: spam meetings"); }
    }

    public static string CloseVoting()
    {
        try
        {
            if (!Utils.isHost) return "Close Voting is host-only.";

            MeetingHud meeting = MeetingHud.Instance;
            if (meeting == null) return "No active meeting.";

            MeetingHud.MeetingStates state = meeting.state;
            if (state != MeetingHud.MeetingStates.NotVoted && state != MeetingHud.MeetingStates.Voted)
                return "Voting is not active.";

            meeting.ForceSkipAll();
            return "Voting closed.";
        }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "MeetingTools.CloseVoting: tally votes"); return "Failed to close voting."; }
    }

    public static string CloseMeetingNoEject()
    {
        try
        {
            if (!Utils.isHost) return "Close Meeting is host-only.";

            MeetingHud meeting = MeetingHud.Instance;
            if (meeting == null) return "No active meeting.";

            meeting.RpcVotingComplete(new Il2CppStructArray<MeetingHud.VoterState>(0), null, true, false, ushort.MinValue);
            meeting.Close();
            meeting.RpcClose();
            return "Meeting closed (no ejection).";
        }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "MeetingTools.CloseMeetingNoEject: close meeting"); return "Failed to close meeting."; }
    }

    public static string Roam()
    {
        try
        {
            if (Utils.isMeeting || ExileController.Instance != null)
            {
                CheatToggles.closeMeeting = true;
                return "Roaming — meeting still runs for others.";
            }
            return "No meeting right now.";
        }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "MeetingTools.Roam: exit and roam"); return "Failed to roam."; }
    }
}
