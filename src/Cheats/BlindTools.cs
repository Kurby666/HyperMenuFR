using System;
using System.Collections.Generic;
using AmongUs.GameOptions;
using HarmonyLib;

namespace MalumMenu;

public static class BlindTools
{
    private const int HandlingId = 20018;

    private static readonly HashSet<byte> selected = new HashSet<byte>();
    private static readonly Dictionary<byte, int> state = new Dictionary<byte, int>();

    public static int SelectedCount => selected.Count;
    public static bool IsSelected(byte pid) => selected.Contains(pid);

    public static void ToggleSelect(byte pid)
    {
        if (selected.Contains(pid))
            selected.Remove(pid);
        else
            selected.Add(pid);
    }

    public static void SelectAll()
    {
        try
        {
            foreach (PlayerControl pc in PlayerControl.AllPlayerControls)
            {
                if (pc == null || pc.Data == null || pc.Data.Disconnected || pc == PlayerControl.LocalPlayer)
                    continue;
                selected.Add(pc.PlayerId);
            }
        }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "BlindTools.SelectAll: select all"); }
    }

    public static void ClearSelection() => selected.Clear();

    public static string StateName(byte pid)
    {
        state.TryGetValue(pid, out int s);
        return s == 1 ? "dark" : s == 2 ? "bright" : "normal";
    }

    public static string Cycle(PlayerControl target)
    {
        state.TryGetValue(target.PlayerId, out int s);
        if (s == 0)
            return Blind(target);
        if (s == 1)
            return Bright(target);
        return Restore(target);
    }

    public static string Blind(PlayerControl target) => Apply(target, -1f, 1);
    public static string Bright(PlayerControl target) => Apply(target, 1000f, 2);

    public static string BlindSelected() => Mass(Blind);
    public static string RestoreSelected() => Mass(Restore);

    public static string ResetAll()
    {
        try
        {
            int n = 0;
            foreach (PlayerControl pc in PlayerControl.AllPlayerControls)
            {
                if (pc == null || pc.Data == null || pc.Data.Disconnected) continue;
                if (Restore(pc).StartsWith("done")) n++;
            }
            return $"done: {n} restored";
        }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "BlindTools.ResetAll: restore all"); return "failed"; }
    }

    private static string Mass(Func<PlayerControl, string> action)
    {
        if (selected.Count == 0) return "nobody selected";
        int total = selected.Count, n = 0;
        try
        {
            foreach (PlayerControl pc in PlayerControl.AllPlayerControls)
            {
                if (pc == null || pc.Data == null || !selected.Contains(pc.PlayerId)) continue;
                if (action(pc).StartsWith("done")) n++;
            }
        }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "BlindTools.Mass: mass apply"); return "failed"; }
        return $"done: {n} of {total}";
    }

    private static string Apply(PlayerControl target, float vision, int newState)
    {
        string err = Guard(target);
        if (err != null) return err;

        try
        {
            IGameOptions clone = GameOptions.CreateCloneOptions(GameManager.Instance.LogicOptions.currentGameOptions);
            clone.SetFloat(FloatOptionNames.CrewLightMod, vision);
            clone.SetFloat(FloatOptionNames.ImpostorLightMod, vision);
            GameOptions.SendGameOptionsToClient(clone, target.OwnerId);
            state[target.PlayerId] = newState;
            return "done: " + target.Data.PlayerName;
        }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "BlindTools.Apply: apply vision"); return "failed"; }
    }

    public static string Restore(PlayerControl target)
    {
        string err = Guard(target);
        if (err != null) return err;

        try
        {
            IGameOptions clone = GameOptions.CreateCloneOptions(GameManager.Instance.LogicOptions.currentGameOptions);
            GameOptions.SendGameOptionsToClient(clone, target.OwnerId);
            state.Remove(target.PlayerId);
            return "done: " + target.Data.PlayerName;
        }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "BlindTools.Restore: restore vision"); return "failed"; }
    }

    private static string Guard(PlayerControl target)
    {
        if (target == null || target.Data == null) return "no target";
        if (target == PlayerControl.LocalPlayer) return "that is you";
        if (target.Data.Disconnected) return "player left";
        if (ShipStatus.Instance == null || GameManager.Instance == null) return "in-match only";
        return null;
    }

    public static void Reset()
    {
        selected.Clear();
        state.Clear();
    }
}

[HarmonyPatch(typeof(LobbyBehaviour), nameof(LobbyBehaviour.Start))]
public static class LobbyBehaviour_BlindResetPatch
{
    private const int HandlingId = 20018;
    public static void Postfix()
    {
        try { BlindTools.Reset(); }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "LobbyBehaviour_BlindResetPatch.Postfix: reset blind state"); }
    }
}
