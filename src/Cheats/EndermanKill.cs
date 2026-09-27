using System;
using System.Collections;
using BepInEx.Unity.IL2CPP.Utils;
using HarmonyLib;
using UnityEngine;

namespace MalumMenu;

public static class EndermanKill
{
    private const int HandlingId = 20003;

    internal static bool Running { get; private set; }

    private static string _backupHat;
    private static string _backupVisor;
    private static string _backupSkin;
    private static string _backupPet;
    private static byte _backupColor;
    private static Vector2 _backupPos;

    private static bool IsKillerRole(PlayerControl pc)
    {
        try
        {
            return pc != null && pc.Data != null && pc.Data.Role != null && pc.Data.Role.IsImpostor;
        }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "EndermanKill.IsKillerRole: check impostor role"); return false; }
    }

    private static bool Ready(out PlayerControl me)
    {
        me = PlayerControl.LocalPlayer;
        return me != null && me.Data != null && !me.Data.IsDead
            && !Utils.isLobby && MeetingHud.Instance == null
            && me.NetTransform != null && IsKillerRole(me);
    }

    private static bool ValidTarget(PlayerControl target, PlayerControl me)
    {
        return target != null && target.Data != null && !target.Data.Disconnected && !target.Data.IsDead
            && target != me && !target.inVent && !IsKillerRole(target);
    }

    internal static string Kill(PlayerControl target)
    {
        try
        {
            if (Running) return "Enderman is already running.";
            if (!Ready(out PlayerControl me)) return "Enderman requires a live impostor in a match.";
            if (!ValidTarget(target, me)) return "Invalid Enderman target.";
            if (!CheatToggles.noKillCd && me.killTimer > 0f) return "Kill cooldown is still active.";

            AmongUsClient.Instance.StartCoroutine(ErrorReporter.GuardCoroutine(Co(target), HandlingId, "EndermanKill.Co"));
            return "Enderman: " + target.Data.PlayerName;
        }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "EndermanKill.Kill: start enderman kill"); return "Enderman failed to start."; }
    }

    private static IEnumerator Co(PlayerControl target)
    {
        PlayerControl me = PlayerControl.LocalPlayer;
        Running = true;
        CaptureOutfit(me);
        _backupPos = me.GetTruePosition();

        Utilities.RandomizePlayer(ingame: true);
        yield return new WaitForSeconds(0.05f);
        if (Interrupted())
        {
            Restore();
            yield break;
        }

        try
        {
            me.NetTransform.RpcSnapTo(target.GetTruePosition());
        }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "EndermanKill.Co: snap to victim"); }
        yield return new WaitForSeconds(0.05f);
        if (Interrupted())
        {
            Restore();
            yield break;
        }

        try
        {
            if (AmongUsClient.Instance.AmHost)
                me.RpcMurderPlayer(target, true);
            else
                me.CmdCheckMurder(target);
        }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "EndermanKill.Co: murder victim"); }

        float elapsed = 0f;
        while (elapsed < 0.35f)
        {
            if (Interrupted())
            {
                Restore();
                yield break;
            }
            elapsed += Time.deltaTime;
            yield return null;
        }

        Restore();
    }

    private static bool Interrupted()
    {
        return MeetingHud.Instance != null || PlayerControl.LocalPlayer == null || PlayerControl.LocalPlayer.Data == null;
    }

    private static void CaptureOutfit(PlayerControl me)
    {
        try
        {
            NetworkedPlayerInfo.PlayerOutfit outfit = me.CurrentOutfit;
            _backupHat = outfit.HatId;
            _backupVisor = outfit.VisorId;
            _backupSkin = outfit.SkinId;
            _backupPet = outfit.PetId;
            _backupColor = (byte)outfit.ColorId;
        }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "EndermanKill.CaptureOutfit: save current outfit"); }
    }

    internal static void Restore()
    {
        if (!Running) return;
        Running = false;

        PlayerControl me = PlayerControl.LocalPlayer;
        if (me == null) return;

        try
        {
            if (me.NetTransform != null)
            {
                me.NetTransform.SnapTo(_backupPos);
                me.NetTransform.RpcSnapTo(_backupPos);
            }
        }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "EndermanKill.Restore: snap back"); }

        try
        {
            NetworkedPlayerInfo.PlayerOutfit outfit = me.CurrentOutfit;
            Network.BatchedMessage batch = new Network.BatchedMessage();
            batch.QueueSetColor(me, _backupColor);
            batch.QueueSetHatStr(me, _backupHat, ++outfit.HatSequenceId);
            batch.QueueSetVisorStr(me, _backupVisor, ++outfit.VisorSequenceId);
            batch.QueueSetSkinStr(me, _backupSkin, ++outfit.SkinSequenceId);
            batch.QueueSetPetStr(me, _backupPet, ++outfit.PetSequenceId);
            batch.FinishBatch();
        }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "EndermanKill.Restore: restore outfit"); }
    }
}

[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Start))]
public static class EndermanKill_MeetingPatch
{
    private const int HandlingId = 20003;
    public static void Prefix()
    {
        try { EndermanKill.Restore(); }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "EndermanKill_MeetingPatch.Prefix: restore on meeting"); }
    }
}
