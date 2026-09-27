using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace MalumMenu;

// Port of othermenu's HyperVentTp + HyperImpTrap: mark players and send
// them to a selected vent, auto-scatter them on a timer, restrict who may
// vent (host), and rally everyone onto the impostor's vent on kill/shift/vanish.
public static class VentTpTools
{
    private const int HandlingId = 20019;

    private static readonly HashSet<byte> marked = new HashSet<byte>();

    public static int Vent;
    public static int MarkedCount => marked.Count;
    public static bool IsMarked(byte pid) => marked.Contains(pid);

    public static string ModeName(int mode) => mode switch
    {
        1 => "crew only",
        2 => "imps only",
        3 => "nobody",
        _ => "everyone",
    };

    public static void ToggleMark(byte pid)
    {
        if (!marked.Remove(pid)) marked.Add(pid);
    }

    public static void MarkAll()
    {
        try
        {
            foreach (PlayerControl pc in PlayerControl.AllPlayerControls)
            {
                if (pc == null || pc.Data == null || pc.Data.Disconnected || pc == PlayerControl.LocalPlayer)
                    continue;
                marked.Add(pc.PlayerId);
            }
        }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "VentTpTools.MarkAll: mark all"); }
    }

    public static void ClearMarks() => marked.Clear();

    public static int VentCount()
    {
        try { return ShipStatus.Instance != null ? ShipStatus.Instance.AllVents.Count : 0; }
        catch { return 0; }
    }

    public static string CycleVent(int dir)
    {
        int c = VentCount();
        if (c <= 0) return "no vents";
        Vent = ((Vent + dir) % c + c) % c;
        return "vent " + Vent;
    }

    public static int NearestVentIndex(Vector2 pos)
    {
        try
        {
            var vents = ShipStatus.Instance?.AllVents;
            if (vents == null || vents.Count == 0) return -1;
            int best = -1;
            float bestD = float.MaxValue;
            for (int i = 0; i < vents.Count; i++)
            {
                Vent v = vents[i];
                if (v == null) continue;
                float d = Vector2.Distance(pos, v.transform.position);
                if (d < bestD) { bestD = d; best = i; }
            }
            return best;
        }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "VentTpTools.NearestVentIndex: find nearest vent"); return -1; }
    }

    private static int VentIdAt(int index)
    {
        var vents = ShipStatus.Instance.AllVents;
        if (vents == null || vents.Count == 0) return -1;
        int i = Mathf.Clamp(index, 0, vents.Count - 1);
        Vent v = vents[i];
        return v != null ? v.Id : i;
    }

    public static string Send(PlayerControl target, int ventIndex)
    {
        if (target == null || target.Data == null) return "no target";
        if (target.Data.Disconnected) return "player left";
        if (target.Data.IsDead) return "target is dead";
        if (AmongUsClient.Instance == null || ShipStatus.Instance == null) return "in-match only";

        int id = VentIdAt(ventIndex);
        if (id < 0) return "no vents";

        try
        {
            Teleporter.TeleportToVent(target, id);
            return $"vent {id}: " + target.Data.PlayerName;
        }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "VentTpTools.Send: send to vent"); return "failed"; }
    }

    private static float lastSend = -99f;

    public static string SendMarked()
    {
        if (AmongUsClient.Instance == null || ShipStatus.Instance == null) return "in-match only";
        if (marked.Count == 0) return "nobody selected";

        float now = Time.unscaledTime;
        if (now - lastSend < 0.2f) return string.Empty;
        lastSend = now;

        if (VentCount() <= 0) return "no vents";
        return $"scattered: {Scatter()}";
    }

    private static float lastAuto = -99f;

    public static void AutoTick()
    {
        try
        {
            if (!CheatToggles.ventTpAuto) return;
            if (AmongUsClient.Instance == null || ShipStatus.Instance == null || marked.Count == 0) return;
            int c = VentCount();
            if (c <= 0) return;

            float now = Time.unscaledTime;
            float delay = Mathf.Clamp(CheatToggles.ventTpAutoDelay, 0.3f, 10f);
            if (now - lastAuto < delay) return;
            lastAuto = now;

            Scatter();
            Vent = (Vent + 1) % c;
        }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "VentTpTools.AutoTick: auto scatter"); }
    }

    private static int Scatter()
    {
        int count = VentCount();
        if (count <= 0) return 0;

        int n = 0, idx = 0;
        try
        {
            foreach (PlayerControl pc in PlayerControl.AllPlayerControls)
            {
                if (pc == null || pc.Data == null || pc.Data.Disconnected || pc.Data.IsDead) continue;
                if (!marked.Contains(pc.PlayerId)) continue;
                int vent = ((Vent + idx) % count + count) % count;
                if (Send(pc, vent).StartsWith("vent")) n++;
                idx++;
            }
        }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "VentTpTools.Scatter: scatter marked"); }
        return n;
    }

    public static bool VentAllowed(int mode, bool imp) => mode switch
    {
        1 => !imp,
        2 => imp,
        3 => false,
        _ => true,
    };

    private static float lastVentCheck = -99f;

    public static void EnforceVentMode()
    {
        try
        {
            int mode = CheatToggles.ventTpMode;
            if (mode <= 0) return;
            if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost) return;
            if (ShipStatus.Instance == null || MeetingHud.Instance != null) return;

            float now = Time.unscaledTime;
            if (now - lastVentCheck < 0.15f) return;
            lastVentCheck = now;

            foreach (PlayerControl pc in PlayerControl.AllPlayerControls)
            {
                if (pc == null || pc.Data == null || pc.Data.Disconnected || pc.Data.IsDead || !pc.inVent) continue;
                if (pc.Data.Role == null) continue;

                if (VentAllowed(mode, pc.Data.Role.IsImpostor)) continue;

                int idx = NearestVentIndex(pc.GetTruePosition());
                if (idx >= 0) Teleporter.TeleportToVent(pc, VentIdAt(idx));
            }
        }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "VentTpTools.EnforceVentMode: boot violators"); }
    }

    public static void Tick()
    {
        AutoTick();
        EnforceVentMode();
    }

    public static void Reset()
    {
        marked.Clear();
        Vent = 0;
        lastSend = -99f;
        lastAuto = -99f;
    }
}

public static class ImpTrap
{
    private const int HandlingId = 20019;
    private const float Gap = 1.5f;
    private static float last = -99f;

    public static void Fire(PlayerControl imp)
    {
        try
        {
            if (!CheatToggles.impTrap) return;
            if (imp == null || imp.Data == null) return;
            if (ShipStatus.Instance == null || LobbyBehaviour.Instance != null) return;
            if (MeetingHud.Instance != null || ExileController.Instance != null) return;

            float now = Time.unscaledTime;
            if (now - last < Gap) return;

            int vent = VentTpTools.NearestVentIndex(imp.GetTruePosition());
            if (vent < 0) return;
            last = now;

            int n = 0;
            foreach (PlayerControl pc in PlayerControl.AllPlayerControls)
            {
                if (pc == null || pc.Data == null || pc.Data.Disconnected || pc.Data.IsDead) continue;
                if (pc.PlayerId == imp.PlayerId || pc == PlayerControl.LocalPlayer) continue;
                if (VentTpTools.Send(pc, vent).StartsWith("vent")) n++;
            }

            if (n > 0)
                MalumMenu.notifications.Send("Rally", $"{imp.Data.PlayerName} - pulled: {n}");
        }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "ImpTrap.Fire: rally on impostor action"); }
    }

    public static void Reset() => last = -99f;
}

[HarmonyPatch(typeof(LobbyBehaviour), nameof(LobbyBehaviour.Start))]
public static class LobbyBehaviour_VentTpResetPatch
{
    private const int HandlingId = 20019;
    public static void Postfix()
    {
        try { VentTpTools.Reset(); ImpTrap.Reset(); }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "LobbyBehaviour_VentTpResetPatch.Postfix: reset vent-tp state"); }
    }
}

[HarmonyPatch(typeof(PlayerPhysics), nameof(PlayerPhysics.CoEnterVent))]
public static class PlayerPhysics_VentModeEnterPatch
{
    private const int HandlingId = 20019;
    public static bool Prefix(PlayerPhysics __instance)
    {
        try
        {
            int mode = CheatToggles.ventTpMode;
            if (mode <= 0 || AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost)
                return true;

            PlayerControl me = __instance.myPlayer;
            if (me == null || !me.AmOwner || me.Data == null || me.Data.Role == null)
                return true;

            return VentTpTools.VentAllowed(mode, me.Data.Role.IsImpostor);
        }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "PlayerPhysics_VentModeEnterPatch.Prefix: gate vent entry"); return true; }
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.MurderPlayer))]
public static class PlayerControl_ImpTrapKillPatch
{
    private const int HandlingId = 20019;
    public static void Postfix(PlayerControl __instance)
    {
        try { ImpTrap.Fire(__instance); }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "PlayerControl_ImpTrapKillPatch.Postfix: rally on kill"); }
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.Shapeshift))]
public static class PlayerControl_ImpTrapShiftPatch
{
    private const int HandlingId = 20019;
    public static void Postfix(PlayerControl __instance)
    {
        try { ImpTrap.Fire(__instance); }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "PlayerControl_ImpTrapShiftPatch.Postfix: rally on shapeshift"); }
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.RpcShapeshift))]
public static class PlayerControl_ImpTrapRpcShiftPatch
{
    private const int HandlingId = 20019;
    public static void Postfix(PlayerControl __instance)
    {
        try { ImpTrap.Fire(__instance); }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "PlayerControl_ImpTrapRpcShiftPatch.Postfix: rally on rpc shapeshift"); }
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.HandleServerVanish))]
public static class PlayerControl_ImpTrapVanishPatch
{
    private const int HandlingId = 20019;
    public static void Postfix(PlayerControl __instance)
    {
        try { ImpTrap.Fire(__instance); }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "PlayerControl_ImpTrapVanishPatch.Postfix: rally on vanish"); }
    }
}
