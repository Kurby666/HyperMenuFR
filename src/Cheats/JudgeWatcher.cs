using System;
using System.Collections.Generic;
using HarmonyLib;
using InnerNet;

namespace MalumMenu;

public static class JudgeWatcher
{
    private const int HandlingId = 20017;

    private struct Pick
    {
        internal byte Judge;
        internal byte Target;
        internal ushort Nonce;
    }

    private static readonly List<Pick> queue = new List<Pick>();
    private static readonly List<string> lines = new List<string>();

    public static List<string> Lines => lines;
    public static int Total { get; private set; }

    public static void ForgetMeeting()
    {
        if (queue.Count == 0) return;
        queue.Clear();
        lines.Clear();
    }

    public static void ForgetMatch()
    {
        ForgetMeeting();
        Total = 0;
    }

    public static void Note(byte judge, byte target, ushort nonce)
    {
        try
        {
            int at = IndexOf(judge);
            if (at >= 0 && queue[at].Target == target) return;

            var p = new Pick { Judge = judge, Target = target, Nonce = nonce };
            if (at >= 0)
                queue[at] = p;
            else
                queue.Add(p);
            Rebuild();

            if (!CheatToggles.judgeWatch) return;
            string entry = lines.Count > 0 ? lines[lines.Count - 1] : "";
            ConsoleUI.Log("[HyperMenu] Judge overrule: " + entry);
            MalumMenu.notifications.Send("Judge Overrule", entry);
        }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "JudgeWatcher.Note: track overrule"); }
    }

    public static void Drop(byte judge)
    {
        try
        {
            int at = IndexOf(judge);
            if (at < 0) return;
            queue.RemoveAt(at);
            Rebuild();
        }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "JudgeWatcher.Drop: remove judge"); }
    }

    public static void Landed(ushort nonce, NetworkedPlayerInfo exiled)
    {
        try
        {
            byte judge = 255;
            for (int i = 0; i < queue.Count; i++)
                if (queue[i].Nonce == nonce)
                {
                    judge = queue[i].Judge;
                    break;
                }

            Total++;
            ForgetMeeting();

            if (!CheatToggles.judgeWatch) return;
            string j = judge != 255 ? PlayerName(judge) : "judge";
            string t = exiled != null ? exiled.PlayerName : "no one";
            string entry = "Overrule landed: " + j + " ejected " + t;
            ConsoleUI.Log("[HyperMenu] Judge " + entry);
            MalumMenu.notifications.Send("Judge Overrule", entry);
        }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "JudgeWatcher.Landed: overrule landed"); }
    }

    public static string ClearAll()
    {
        try
        {
            if (!Utils.isHost) return "Host only.";
            MeetingHud m = MeetingHud.Instance;
            if (m == null) return "No meeting.";

            if (m.judgeOverrulesQueue != null)
                m.judgeOverrulesQueue.Clear();
            m.ClearJudgeOverrule();
            ForgetMeeting();
            return "Overrule queue cleared.";
        }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "JudgeWatcher.ClearAll: clear queue"); return "Error clearing queue."; }
    }

    private static int IndexOf(byte judge)
    {
        for (int i = 0; i < queue.Count; i++)
            if (queue[i].Judge == judge) return i;
        return -1;
    }

    private static void Rebuild()
    {
        lines.Clear();
        for (int i = 0; i < queue.Count; i++)
            lines.Add(PlayerName(queue[i].Judge) + "  ->  " + PlayerName(queue[i].Target));
    }

    private static string PlayerName(byte pid)
    {
        if (GameData.Instance == null) return "?";
        NetworkedPlayerInfo info = GameData.Instance.GetPlayerById(pid);
        return info != null && !string.IsNullOrEmpty(info.PlayerName) ? info.PlayerName : "#" + pid;
    }
}

[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.AddOrUpdateJudgeOverrule))]
public static class MeetingHud_JudgeQueuePatch
{
    private const int HandlingId = 20017;
    public static void Postfix(
        [HarmonyArgument(0)] PlayerId judgePlayerId,
        [HarmonyArgument(1)] PlayerId targetPlayerId,
        [HarmonyArgument(2)] ushort overruleNonce)
    {
        try { JudgeWatcher.Note(judgePlayerId, targetPlayerId, overruleNonce); }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "MeetingHud_JudgeQueuePatch.Postfix: note overrule"); }
    }
}

[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.ClearJudgeOverrule))]
public static class MeetingHud_JudgeClearPatch
{
    private const int HandlingId = 20017;
    public static void Postfix()
    {
        try { JudgeWatcher.ForgetMeeting(); }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "MeetingHud_JudgeClearPatch.Postfix: forget meeting"); }
    }
}

[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.UpdateOverruleQueueFromDisconnection))]
public static class MeetingHud_JudgeLeavePatch
{
    private const int HandlingId = 20017;
    public static void Postfix([HarmonyArgument(0)] PlayerId disconnectedPlayerId)
    {
        try { JudgeWatcher.Drop(disconnectedPlayerId); }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "MeetingHud_JudgeLeavePatch.Postfix: drop judge"); }
    }
}

[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.VotingComplete))]
public static class MeetingHud_JudgeDonePatch
{
    private const int HandlingId = 20017;
    public static void Prefix([HarmonyArgument(1)] NetworkedPlayerInfo exiled, [HarmonyArgument(3)] bool wasOverruled, [HarmonyArgument(4)] ushort nonce)
    {
        try
        {
            if (wasOverruled)
                JudgeWatcher.Landed(nonce, exiled);
            else
                JudgeWatcher.ForgetMeeting();
        }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "MeetingHud_JudgeDonePatch.Prefix: voting complete"); }
    }
}

[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Start))]
public static class MeetingHud_JudgeMeetingStartPatch
{
    private const int HandlingId = 20017;
    public static void Postfix()
    {
        try { JudgeWatcher.ForgetMeeting(); }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "MeetingHud_JudgeMeetingStartPatch.Postfix: forget meeting"); }
    }
}

[HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.Start))]
public static class ShipStatus_JudgeMatchStartPatch
{
    private const int HandlingId = 20017;
    public static void Postfix()
    {
        try { JudgeWatcher.ForgetMatch(); }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "ShipStatus_JudgeMatchStartPatch.Postfix: forget match"); }
    }
}
