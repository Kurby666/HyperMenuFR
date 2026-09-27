using System;
using System.Collections.Generic;
using HarmonyLib;
using Hazel;
using InnerNet;
using UnityEngine;

namespace MalumMenu;

public static class GodMode
{
    private const int HandlingId = 20020;

    private const byte GRANT_VENT_ID = 60;
    private const float KEEP_GAP = 5f;

    private static readonly HashSet<byte> granted = new HashSet<byte>();
    private static readonly HashSet<byte> selected = new HashSet<byte>();

    private static float lastKeep;

    public static int GrantedCount => granted.Count;
    public static int SelectedCount => selected.Count;
    public static bool IsGranted(byte pid) => granted.Contains(pid);
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
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "GodMode.SelectAll: select all"); }
    }

    public static void ClearSelection() => selected.Clear();

    public static string Toggle(PlayerControl target)
    {
        string err = Guard(target);
        if (err != null) return err;

        if (granted.Contains(target.PlayerId))
            return Remove(target);
        return Grant(target);
    }

    public static string GrantSelected()
    {
        if (selected.Count == 0) return "nobody selected";
        int total = selected.Count, n = 0;
        try
        {
            foreach (PlayerControl pc in PlayerControl.AllPlayerControls)
            {
                if (pc == null || pc.Data == null || !selected.Contains(pc.PlayerId)) continue;
                if (Grant(pc).StartsWith("done")) n++;
            }
        }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "GodMode.GrantSelected: mass grant"); return "failed"; }
        return $"done: {n} of {total}";
    }

    public static string RemoveSelected()
    {
        if (selected.Count == 0) return "nobody selected";
        int total = selected.Count, n = 0;
        try
        {
            foreach (PlayerControl pc in PlayerControl.AllPlayerControls)
            {
                if (pc == null || pc.Data == null || !selected.Contains(pc.PlayerId)) continue;
                if (Remove(pc).StartsWith("done")) n++;
            }
        }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "GodMode.RemoveSelected: mass remove"); return "failed"; }
        return $"done: {n} of {total}";
    }

    public static string Grant(PlayerControl target)
    {
        string err = Guard(target);
        if (err != null) return err;

        try
        {
            if (!Push(target, true)) return "failed";
            granted.Add(target.PlayerId);
            return "done: immortality granted to " + target.Data.PlayerName;
        }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "GodMode.Grant: grant immortality"); return "failed"; }
    }

    public static string Remove(PlayerControl target)
    {
        string err = Guard(target);
        if (err != null) return err;

        try
        {
            if (!Push(target, false)) return "failed";
            granted.Remove(target.PlayerId);
            return "done: immortality removed from " + target.Data.PlayerName;
        }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "GodMode.Remove: remove immortality"); return "failed"; }
    }

    private static bool Push(PlayerControl target, bool enter)
    {
        try
        {
            // Forges a ventilation update as if it came from the target player, putting
            // them in a fake vent so CheckMurder fails for them (same trick as self Immortality).
            // Host applies broadcast updates itself; non-host sends GameDataTo the host.
            Network.BatchedMessage batch = AmongUsClient.Instance.AmHost
                ? new Network.BatchedMessage()
                : new Network.BatchedMessage(AmongUsClient.Instance.HostId);

            MessageWriter body = MessageWriter.Get(SendOption.Reliable);
            body.Write((ushort)(enter ? 0 : 1));
            body.Write((byte)(enter ? VentilationSystem.Operation.Enter : VentilationSystem.Operation.Exit));
            body.Write(GRANT_VENT_ID);

            batch.QueueUpdateSystem(target, SystemTypes.Ventilation, body);
            body.Recycle();
            batch.FinishBatch();
            return true;
        }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "GodMode.Push: send forged vent update"); return false; }
    }

    public static void Tick()
    {
        if (granted.Count == 0) return;
        if (ShipStatus.Instance == null) return;

        float now = Time.unscaledTime;
        if (now - lastKeep < KEEP_GAP) return;
        lastKeep = now;

        try
        {
            byte[] ids = new byte[granted.Count];
            granted.CopyTo(ids);
            foreach (byte pid in ids)
            {
                PlayerControl pc = null;
                foreach (PlayerControl c in PlayerControl.AllPlayerControls)
                {
                    if (c != null && c.PlayerId == pid) { pc = c; break; }
                }
                if (pc == null || pc.Data == null || pc.Data.Disconnected || pc.Data.IsDead)
                {
                    granted.Remove(pid);
                    continue;
                }
                Push(pc, true);
            }
        }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "GodMode.Tick: keep-alive re-grant"); }
    }

    public static void ResendAll()
    {
        if (granted.Count == 0) return;
        if (ShipStatus.Instance == null) return;

        try
        {
            byte[] ids = new byte[granted.Count];
            granted.CopyTo(ids);
            foreach (byte pid in ids)
            {
                foreach (PlayerControl c in PlayerControl.AllPlayerControls)
                {
                    if (c != null && c.PlayerId == pid && c.Data != null && !c.Data.Disconnected && !c.Data.IsDead)
                        Push(c, true);
                }
            }
            lastKeep = Time.unscaledTime;
        }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "GodMode.ResendAll: re-send grants"); }
    }

    private static string Guard(PlayerControl target)
    {
        if (target == null || target.Data == null) return "no target";
        if (target == PlayerControl.LocalPlayer) return "that is you (use Immortality)";
        if (target.Data.Disconnected) return "player left";
        if (target.Data.IsDead) return "player is dead";
        if (ShipStatus.Instance == null) return "in-match only";
        return null;
    }

    public static void Reset()
    {
        granted.Clear();
        selected.Clear();
    }
}

[HarmonyPatch(typeof(LobbyBehaviour), nameof(LobbyBehaviour.Start))]
public static class LobbyBehaviour_GodResetPatch
{
    private const int HandlingId = 20020;
    public static void Postfix()
    {
        try { GodMode.Reset(); }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "LobbyBehaviour_GodResetPatch.Postfix: reset god state"); }
    }
}

[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Close))]
public static class MeetingHud_GodResendPatch
{
    private const int HandlingId = 20020;
    public static void Postfix()
    {
        try { GodMode.ResendAll(); }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "MeetingHud_GodResendPatch.Postfix: re-send grants after meeting"); }
    }
}
