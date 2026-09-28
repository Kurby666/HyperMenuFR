using System;
using HarmonyLib;
using UnityEngine;

namespace MalumMenu;

[HarmonyPatch(typeof(PlayerPhysics), nameof(PlayerPhysics.LateUpdate))]
public static class PlayerPhysics_LateUpdate
{
    private const int HandlingId = 30015;
    public static void Postfix(PlayerPhysics __instance)
    {
        try
        {
            // PlayerControl.LateUpdate still fires in a lobby (a stale PlayerPhysics hangs around)
            // but there is no local PlayerControl until the match spawns us. Everything below is an
            // in-match concept, so bail rather than let each one throw a NullReferenceException per
            // frame. This is the driver-level fix for the whole 20002/20008/20015 family.
            // Require a fully-spawned local player, not just a non-null reference: Data and
            // Data.Role are null on a PlayerControl that is being created or destroyed, and Role
            // is null for lobby players. Everything below is in-match, so this is the right gate.
            PlayerControl local = PlayerControl.LocalPlayer;
            if (local == null || local.Data == null || local.Data.Role == null) return;

            MalumESP.PlayerNametags(__instance);
            MalumESP.SeeGhostsCheat(__instance);

            MalumCheats.NoClipCheat();
            MalumCheats.ProtectCheat();
            MalumCheats.KillAllCheat();
            MalumCheats.KillAllCrewCheat();
            MalumCheats.KillAllImpsCheat();
            MalumCheats.ForceStartGameCheat();
            MalumCheats.TeleportCursorCheat();
            MouseTools.Tick();
            GhostTools.Tick();
            NeonOutline.Tick();
            Cheats.MatchReplay.Tick();
            Cheats.LobbyPranks.LateTick();
            MalumCheats.CompleteMyTasksCheat();
            MalumCheats.CompleteAllTasksCheat();
            MalumCheats.PlayAnimationCheat();
            MalumCheats.PlayScannerCheat();

            MalumPPMCheats.EjectPlayerPPM();
            MalumPPMCheats.SpectatePPM();
            MalumPPMCheats.KillPlayerPPM();
            MalumPPMCheats.TelekillPlayerPPM();
            MalumPPMCheats.TeleportPlayerPPM();
            MalumPPMCheats.SetFakeRolePPM();
            MalumPPMCheats.SetFakeAlivePPM();
            // MalumPPMCheats.ForceRolePPM();

            // This check ensures there is only one run per frame
            // so that OverloadHandler._timer progression remains accurate
            // if (__instance.AmOwner)
            // {
            //     OverloadHandler.Run();
            // }

            TracersHandler.DrawPlayerTracer(__instance);

            GameObject[] bodyObjects = GameObject.FindGameObjectsWithTag("DeadBody");
            foreach (GameObject bodyObject in bodyObjects) // Finds and loops through all dead bodies
            {
                DeadBody deadBody = bodyObject.GetComponent<DeadBody>();
                if (!deadBody) continue;

                TracersHandler.DrawBodyTracer(deadBody);

                if (CheatToggles.autoReportBodies)
                {
                    if (deadBody.Reported) continue;

                    // Same story: no local PlayerControl and no GameData outside a match, so
                    // CmdReportDeadBody could not be called and would throw instead.
                    if (PlayerControl.LocalPlayer == null || GameData.Instance == null) continue;

                    deadBody.Reported = true;

                    PlayerControl.LocalPlayer.CmdReportDeadBody(GameData.Instance.GetPlayerById(deadBody.ParentId));
                }
            }

            try
            {
                // No local PlayerControl outside a match (main menu or lobby), so MyPhysics is
                // null there. PlayerPhysics.LateUpdate runs every frame, so this reported a
                // NullReferenceException continuously while the user sat in a lobby.
                if (PlayerControl.LocalPlayer?.MyPhysics == null)
                {
                    return;
                }

                if (CheatToggles.invertControls)
                {
                    PlayerControl.LocalPlayer.MyPhysics.Speed = -Mathf.Abs(PlayerControl.LocalPlayer.MyPhysics.Speed);
                    PlayerControl.LocalPlayer.MyPhysics.GhostSpeed = -Mathf.Abs(PlayerControl.LocalPlayer.MyPhysics.GhostSpeed);
                }
                else
                {
                    PlayerControl.LocalPlayer.MyPhysics.Speed = Mathf.Abs(PlayerControl.LocalPlayer.MyPhysics.Speed);
                    PlayerControl.LocalPlayer.MyPhysics.GhostSpeed = Mathf.Abs(PlayerControl.LocalPlayer.MyPhysics.GhostSpeed);
                }
            } catch (NullReferenceException ex) { ErrorReporter.Report(ex, HandlingId, "PlayerPhysics_LateUpdate.Postfix: apply invert controls"); }
        }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "PlayerPhysics_LateUpdate.Postfix: run frame cheats"); }
    }
}

[HarmonyPatch(typeof(PlayerPhysics), nameof(PlayerPhysics.HandleAnimation))]
public static class PlayerPhysics_HandleAnimation
{
    private const int HandlingId = 30015;
    // Prefix patch of PlayerPhysics.HandleAnimation to disable walking animation
    public static bool Prefix(PlayerPhysics __instance)
    {
        try
        {
            if (CheatToggles.moonWalk && __instance.AmOwner)
            {
                __instance.ResetAnimState();

                return false;
            }

            return true;
        }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "PlayerPhysics_HandleAnimation.Prefix: moon walk"); return true; }
    }
}
