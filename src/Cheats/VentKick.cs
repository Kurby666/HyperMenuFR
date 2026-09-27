using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace MalumMenu;

public static class VentKick
{
    private const int HandlingId = 20005;
    private const float StepDelay = 0.2f;

    private static readonly HashSet<byte> _selected = new();
    private static readonly Queue<byte> _pending = new();
    private static float _nextAt;

    internal static int SelectedCount => _selected.Count;
    internal static bool IsSelected(byte playerId) => _selected.Contains(playerId);

    internal static void ToggleSelect(byte playerId)
    {
        try
        {
            if (!_selected.Remove(playerId)) _selected.Add(playerId);
        }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "VentKick.ToggleSelect: toggle player selection"); }
    }

    internal static void ClearSelection()
    {
        try { _selected.Clear(); }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "VentKick.ClearSelection: clear selection"); }
    }

    internal static void SelectAll()
    {
        try
        {
            foreach (PlayerControl pc in PlayerControl.AllPlayerControls)
            {
                if (pc == null || pc.Data == null || pc.Data.Disconnected) continue;
                if (pc == PlayerControl.LocalPlayer) continue;
                if (pc.OwnerId == AmongUsClient.Instance.HostId) continue;
                _selected.Add(pc.PlayerId);
            }
        }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "VentKick.SelectAll: select all players"); }
    }

    internal static string KickSelected()
    {
        try
        {
            if (_selected.Count == 0) return "Nobody selected.";
            int total = _selected.Count;
            foreach (byte playerId in _selected)
                _pending.Enqueue(playerId);
            _selected.Clear();
            return $"Queued: {total}";
        }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "VentKick.KickSelected: queue selected players"); return "Failed to queue."; }
    }

    internal static void Tick()
    {
        try
        {
            if (_pending.Count == 0) return;
            if (Time.unscaledTime < _nextAt) return;
            _nextAt = Time.unscaledTime + StepDelay;

            byte playerId = _pending.Dequeue();
            PlayerControl target = null;
            foreach (PlayerControl pc in PlayerControl.AllPlayerControls)
            {
                if (pc != null && pc.PlayerId == playerId) { target = pc; break; }
            }
            if (target == null || target.Data == null || target.Data.Disconnected) return;

            MalumMenu.notifications.Send("Vent Kick", Kick(target), 5);
        }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "VentKick.Tick: kick queued players"); }
    }

    internal static void Reset()
    {
        try
        {
            _selected.Clear();
            _pending.Clear();
            _nextAt = 0f;
        }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "VentKick.Reset: clear state"); }
    }

    internal static string Kick(PlayerControl target)
    {
        try
        {
            if (target == null || target.Data == null || target.Data.Disconnected) return "No target.";
            if (target == PlayerControl.LocalPlayer) return "That is you.";
            if (AmongUsClient.Instance == null || ShipStatus.Instance == null) return "In-match only.";
            if (target.OwnerId == AmongUsClient.Instance.HostId) return "Can't kick the host this way.";

            Utilities.KickPlayer(target);
            return $"Kicked: {target.Data.PlayerName}";
        }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "VentKick.Kick: kick player"); return "Failed."; }
    }
}

[HarmonyPatch(typeof(LobbyBehaviour), nameof(LobbyBehaviour.Start))]
public static class VentKick_ResetPatch
{
    private const int HandlingId = 20005;
    public static void Postfix()
    {
        try { VentKick.Reset(); }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "VentKick_ResetPatch.Postfix: reset on lobby"); }
    }
}
