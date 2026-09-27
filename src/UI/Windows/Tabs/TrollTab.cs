using System;
using MalumMenu.features;
using UnityEngine;

namespace MalumMenu;

public class TrollTab : ITab
{
    private const int HandlingId = 60020;
    public string name => "Troll";

    public void Draw()
    {
        try
        {
            if (PlayerControl.LocalPlayer == null)
            {
                GUILayout.Label("You are not currently in a game, these options will not work.");
            }

            GUILayout.BeginVertical(GUILayout.Width(MenuUI.windowWidth * 0.425f));

            Troll.AutoReportBodies.Enabled = GUILayout.Toggle(Troll.AutoReportBodies.Enabled, "Automatically Report Bodies");
            MalumMenu.routines.autoTriggerSpores.Enabled = GUILayout.Toggle(MalumMenu.routines.autoTriggerSpores.Enabled, "Auto Medbay Scan");
            MalumMenu.routines.fungleSporeTrigger.Enabled = GUILayout.Toggle(MalumMenu.routines.fungleSporeTrigger.Enabled, "Auto Trigger Spores (Fungle)");
            Troll.BlockSabotages.Enabled = GUILayout.Toggle(Troll.BlockSabotages.Enabled, "Block Sabotages");
            Troll.BlockVenting.Enabled = GUILayout.Toggle(Troll.BlockVenting.Enabled, "Disable Vents");

            if (GUILayout.Button(" Trigger All Spores"))
            {
                if (Utilities.GetCurrentMap() != MapNames.Fungle)
                {
                    MalumMenu.notifications.Send("Trigger Spores", "This option only works on the Fungle map.");
                }
                else
                {
                    FungleShipStatus shipStatus = ShipStatus.Instance.Cast<FungleShipStatus>();

                    foreach (Mushroom mushroom in shipStatus.sporeMushrooms.Values)
                    {
                        PlayerControl.LocalPlayer.RpcTriggerSpores(mushroom);
                    }

                    MalumMenu.notifications.Send("Trigger Spores", "All spores have been triggered.", 5);
                }
            }

            if (GUILayout.Button(" Copy Random Player"))
            {
                PlayerControl randomPl = Utilities.GetRandomPlayer();
                Utilities.CopyPlayer(randomPl);
            }

            GUILayout.Space(5);

            GUILayout.Label("Door Troller:");
            MalumMenu.routines.doorTroller.Enabled = GUILayout.Toggle(MalumMenu.routines.doorTroller.Enabled, "Enabled");

            GUILayout.Label($"Lock and Unlock Delay: {MalumMenu.routines.doorTroller.lockAndUnlockDelay:F2}s");
            MalumMenu.routines.doorTroller.lockAndUnlockDelay = GUILayout.HorizontalSlider(MalumMenu.routines.doorTroller.lockAndUnlockDelay, 0.1f, 2.0f);

            GUILayout.Space(5);

            GUILayout.Label("Vent Kick:");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("SELECT ALL"))
            {
                VentKick.SelectAll();
            }
            if (GUILayout.Button("CLEAR ALL"))
            {
                VentKick.ClearSelection();
            }
            GUILayout.EndHorizontal();
            if (GUILayout.Button($"KICK SELECTED ({VentKick.SelectedCount})"))
            {
                MalumMenu.notifications.Send("Vent Kick", VentKick.KickSelected());
            }

            GUILayout.Space(5);

            GUILayout.Label("Jail:");
            MalumMenu.routines.jailPlayer.Enabled = GUILayout.Toggle(MalumMenu.routines.jailPlayer.Enabled, "Enabled");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("SELECT ALL"))
            {
                MalumMenu.routines.jailPlayer.SelectAll();
            }
            if (GUILayout.Button("CLEAR ALL"))
            {
                MalumMenu.routines.jailPlayer.ClearSelection();
            }
            GUILayout.EndHorizontal();
            GUILayout.Label($"Jailed: {MalumMenu.routines.jailPlayer.SelectedCount}");

            GUILayout.Space(5);

            GUILayout.Label("Blind:");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("SELECT ALL"))
            {
                BlindTools.SelectAll();
            }
            if (GUILayout.Button("CLEAR ALL"))
            {
                BlindTools.ClearSelection();
            }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button($"BLIND SELECTED ({BlindTools.SelectedCount})"))
            {
                MalumMenu.notifications.Send("Blind", BlindTools.BlindSelected());
            }
            if (GUILayout.Button("RESTORE SELECTED"))
            {
                MalumMenu.notifications.Send("Blind", BlindTools.RestoreSelected());
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(5);

            GUILayout.Label("God Mode (others):");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("SELECT ALL"))
            {
                GodMode.SelectAll();
            }
            if (GUILayout.Button("CLEAR ALL"))
            {
                GodMode.ClearSelection();
            }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button($"GRANT SELECTED ({GodMode.SelectedCount})"))
            {
                MalumMenu.notifications.Send("God Mode", GodMode.GrantSelected());
            }
            if (GUILayout.Button("REMOVE SELECTED"))
            {
                MalumMenu.notifications.Send("God Mode", GodMode.RemoveSelected());
            }
            GUILayout.EndHorizontal();
            GUILayout.Label($"Immortal: {GodMode.GrantedCount}");

            GUILayout.Space(5);

            GUILayout.Label("Votekick:");
            string autoVkLabel = VotekickTools.Armed ? "Stop Auto-Votekick" : "Start Auto-Votekick";
            if (GUILayout.Button(autoVkLabel))
            {
                VotekickTools.ToggleAuto();
            }
            string autoTargetsLabel = VotekickTools.AutoTargeting ? $"Auto-Targets ON ({VotekickTools.TargetCount})" : $"Auto-Targets ({VotekickTools.TargetCount})";
            if (GUILayout.Button(autoTargetsLabel))
            {
                VotekickTools.ToggleTargetAuto();
            }
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("VOTE ALL + STAY"))
            {
                VotekickTools.VoteAllStay();
            }
            if (GUILayout.Button("VOTE EACH IN TURN"))
            {
                VotekickTools.RapidAll();
            }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("MARK HOST"))
            {
                VotekickTools.ToggleHostTarget();
            }
            if (GUILayout.Button("CLEAR TARGETS"))
            {
                VotekickTools.ClearTargets();
            }
            GUILayout.EndHorizontal();
            CheatToggles.votekickAutoRejoin = GUILayout.Toggle(CheatToggles.votekickAutoRejoin, "Auto rejoin by code");
            CheatToggles.votekickCopyCode = GUILayout.Toggle(CheatToggles.votekickCopyCode, "Copy lobby code");
            if (GUILayout.Button("REJOIN LAST"))
            {
                VotekickTools.RejoinLast();
            }
            CheatToggles.voteSpam = GUILayout.Toggle(CheatToggles.voteSpam, "Vote spam (HOST, in meeting)");
            CheatToggles.votekickProtect = GUILayout.Toggle(CheatToggles.votekickProtect, "Block votekicks (guard)");
            if (CheatToggles.votekickProtect)
            {
                if (GUILayout.Button($"Punish voter: {Cheats.VotekickGuard.PunishName(CheatToggles.votekickPunishIdx)}"))
                {
                    CheatToggles.votekickPunishIdx = (CheatToggles.votekickPunishIdx + 1) % 4;
                }
                GUILayout.Label($"Votes on you: {Cheats.VotekickGuard.VotesOnMe}");
            }

            GUILayout.Space(5);

            GUILayout.Label("Camera Jammer (HOST):");
            CheatToggles.cameraJam = GUILayout.Toggle(CheatToggles.cameraJam, "Jam watchers' cameras");

            GUILayout.EndVertical();
        }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "TrollTab.Draw: draw troll controls"); }
    }
}
