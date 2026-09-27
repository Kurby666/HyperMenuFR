using System;
using HarmonyLib;
using UnityEngine;

namespace MalumMenu;

// Port of othermenu GhostStartPatches.cs (ghost after start + impostor suicide).
// Real local death via PlayerControl.Die, same as othermenu Activate(); suicide via
// host RpcMurderPlayer / non-host CmdCheckMurder, same as src PlayersTab murder button.
internal static class GhostTools
{
    private const int HandlingId = 20024;

    private static int _tries = -1;
    private static float _nextAt;

    internal static void Arm()
    {
        try
        {
            _tries = CheatToggles.ghostAfterStart ? 0 : -1;
            _nextAt = Time.realtimeSinceStartup + 1f;
        }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "GhostTools.Arm: arm ghost timer"); }
    }

    // Called every frame from PlayerPhysics_LateUpdate.
    internal static void Tick()
    {
        try
        {
            if (_tries < 0) return;
            if (!CheatToggles.ghostAfterStart)
            {
                _tries = -1;
                return;
            }
            if (Time.realtimeSinceStartup < _nextAt) return;

            PlayerControl me = PlayerControl.LocalPlayer;
            if (IsDead(me))
            {
                _tries = -1;
                return;
            }

            bool ready = ShipStatus.Instance != null && LobbyBehaviour.Instance == null
                && MeetingHud.Instance == null && ExileController.Instance == null
                && me != null && me.Data != null && me.Data.Role != null;
            if (!ready)
            {
                Retry();
                return;
            }

            if (Activate(me))
            {
                MalumMenu.notifications.Send("Ghost", "Ghost mode enabled.", 2.5f);
                _tries = -1;
                return;
            }
            Retry();
        }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "GhostTools.Tick: ghost activation"); }
    }

    private static void Retry()
    {
        if (_tries >= 60)
        {
            _tries = -1;
            return;
        }
        _tries++;
        _nextAt = Time.realtimeSinceStartup + 0.5f;
    }

    // Impostor suicide: real death via murder RPCs. Returns feedback string for UI toast.
    internal static string SuicideNow()
    {
        try
        {
            PlayerControl me = PlayerControl.LocalPlayer;
            if (me == null || me.Data == null) return "No player.";
            if (ShipStatus.Instance == null || LobbyBehaviour.Instance != null) return "In-match only.";
            if (MeetingHud.Instance != null || ExileController.Instance != null) return "Not during a meeting.";
            if (IsDead(me)) return "You are already dead.";
            if (me.Data.Role == null || !RoleManager.IsImpostorRole(me.Data.RoleType)) return "Impostor only.";

            _tries = -1;
            if (AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost)
                me.RpcMurderPlayer(me, true);
            else
                me.CmdCheckMurder(me);
            return "Suicide sent.";
        }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "GhostTools.SuicideNow: suicide"); return "Failed."; }
    }

    private static bool Activate(PlayerControl me)
    {
        if (me == null) return false;
        if (IsDead(me)) return true;

        if (TryDie(me, DeathReason.Exile, true) || TryDie(me, DeathReason.Exile, false)
            || TryDie(me, DeathReason.Kill, true) || TryDie(me, DeathReason.Kill, false))
            return true;

        return false;
    }

    private static bool TryDie(PlayerControl me, DeathReason reason, bool anim)
    {
        try
        {
            me.Die(reason, anim);
        }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "GhostTools.TryDie: die"); }
        return IsDead(me);
    }

    private static bool IsDead(PlayerControl me)
    {
        return me != null && me.Data != null && me.Data.IsDead;
    }

    [HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.CoStartGame))]
    private static class GhostToolsArmPatch
    {
        static void Postfix()
        {
            try { Arm(); }
            catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "GhostToolsArmPatch.Postfix: arm"); }
        }
    }
}
