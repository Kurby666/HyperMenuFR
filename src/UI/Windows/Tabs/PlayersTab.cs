using UnityEngine;
using AmongUs.Data;
using AmongUs.GameOptions;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using InnerNet;
using System;
using System.Collections;

namespace MalumMenu;

public class PlayersTab : ITab
{
    private const int HandlingId = 60013;
    public string name => "Players";

    private Vector2 _subsectionScrollVector = Vector2.zero;
    private Vector2 _subsectionScrollVector2 = Vector2.zero;
    private static CrewmateColor _selectedColor = CrewmateColor.Red;

    public void Draw()
    {
        try
        {
            if (PlayerControl.AllPlayerControls.Count == 0)
            {
                GUILayout.Label("There are currently no online players.");
                return;
            }

            GUILayout.BeginHorizontal();

            // Left panel: Player list
            GUILayout.BeginVertical(GUILayout.Width(MenuUI.windowWidth * 0.35f));
            _subsectionScrollVector = GUILayout.BeginScrollView(_subsectionScrollVector);
            DrawPlayerList();
            GUILayout.EndScrollView();
            GUILayout.EndVertical();

            // Right panel: Player controls
            if (PlayersSection.selectedPlayer != null)
            {
                GUILayout.BeginVertical();
                _subsectionScrollVector2 = GUILayout.BeginScrollView(_subsectionScrollVector2);
                DrawPlayerControls(PlayersSection.selectedPlayer);
                GUILayout.EndScrollView();
                GUILayout.EndVertical();
            }

            GUILayout.EndHorizontal();
        }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "PlayersTab.Draw: draw player list and controls"); }
    }

    private void DrawPlayerList()
    {
        for (byte i = 0; i < PlayerControl.AllPlayerControls.Count; i++)
        {
            PlayerControl player = PlayerControl.AllPlayerControls[i];
            if (player.Data == null) continue;

            RenderPlayerSelection(i, player);
        }
    }

    private void RenderPlayerSelection(byte position, PlayerControl player)
    {
        string playerName = player.Data.PlayerName;
        playerName += $"\n<color=\"{GetRoleColor(player.Data.RoleType)}\">{player.Data.RoleType}</color>";

        bool isSelected = player == PlayersSection.selectedPlayer;
        GUIStyle style = GUI.skin.button;

        if (player.OwnerId == AmongUsClient.Instance.HostId)
        {
            style.normal.textColor = new Color(1.0f, 0.84f, 0.0f);
        }

        if (GUILayout.Button(playerName, style))
        {
            PlayersSection.selectedPlayer = player;
        }
    }

    private string GetRoleColor(RoleTypes role)
    {
        return RoleManager.IsImpostorRole(role) ? "red" : "#8afcfc";
    }

    private static void DrawPlayerControls(PlayerControl target)
    {
        if (target == null || target.Data == null)
        {
            GUILayout.Label("Specified target is not valid.");
            return;
        }

        ClientData clientData = AmongUsClient.Instance.GetClientFromCharacter(target);
        if (clientData != null)
        {
            PlatformSpecificData platform = clientData.PlatformData;
            bool streamerMode = DataManager.Settings.Gameplay.StreamerMode;

            GUILayout.Label(
                $"Name: {target.Data.PlayerName} {target.Data.ColorName}" +
                $"\nRole: {target.Data.RoleType}" +
                $"\nState: " + (target.Data.IsDead ? "Dead" : "Alive") +
                $"\nFriendcode: " + (streamerMode ? "REDACTED" : target.Data.FriendCode) +
                $"\nPUID: " + (streamerMode ? "REDACTED" : target.Data.Puid) +
                $"\nLevel: {target.Data.PlayerLevel + 1}" +
                $"\nDevice: {platform.Platform}" +
                (target.OwnerId == AmongUsClient.Instance.HostId ? "\nHost: true" : "")
            );
        }
        else
        {
            GUILayout.Label(
                $"Name: {target.Data.PlayerName} {target.Data.ColorName}" +
                $"\nRole: {target.Data.RoleType}" +
                $"\nState: " + (target.Data.IsDead ? "Dead" : "Alive") +
                $"\nIs Dummy: true"
            );
        }

        if (GUILayout.Button("Teleport"))
        {
            Teleporter.TeleportTo(target.transform.position);
        }

        if (GUILayout.Button("Murder"))
        {
            if (AmongUsClient.Instance.AmHost)
            {
                MalumMenu.Log.LogInfo($"Attempting to kill {target.Data.PlayerName}, we are host so we are using the MurderPlayer RPC");
                PlayerControl.LocalPlayer.RpcMurderPlayer(target, true);
            }
            else
            {
                MalumMenu.Log.LogInfo($"Attempting to kill {target.Data.PlayerName}, we are not the host so we have to use the CheckMurder RPC");
                PlayerControl.LocalPlayer.CmdCheckMurder(target);
            }
        }

        if (GUILayout.Button(Cheats.LobbyPranks.LoopLeft > 0 ? $"STOP Murder Loop ({Cheats.LobbyPranks.LoopLeft} left)" : "Murder Loop x20 (HOST)"))
        {
            MalumMenu.notifications.Send("Pranks", Cheats.LobbyPranks.MurderLoop(target, 20));
        }

        if (GUILayout.Button("Copy Avatar"))
        {
            Utilities.CopyPlayer(target);
        }

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Steal Outfit"))
        {
            MalumMenu.notifications.Send("Outfit", Cheats.OutfitTools.StealOutfit(target));
        }
        if (GUILayout.Button("Force My Outfit (HOST)"))
        {
            MalumMenu.notifications.Send("Outfit", Cheats.OutfitTools.SetOutfitOnTarget(target));
        }
        GUILayout.EndHorizontal();

        if (GUILayout.Button("Reserve Color (HOST)"))
        {
            MalumMenu.notifications.Send("Colors", Cheats.ColorTools.ReserveTarget(target));
        }

        GUILayout.BeginHorizontal();
        string morphSelectLabel = Cheats.MorphTools.IsSelected(target.PlayerId) ? "Unselect Morph" : "Select Morph";
        if (GUILayout.Button(morphSelectLabel))
        {
            Cheats.MorphTools.ToggleSelect(target.PlayerId);
        }
        if (GUILayout.Button("MORPH INTO THIS (HOST)"))
        {
            MalumMenu.notifications.Send("Morph", Cheats.MorphTools.IntoSelected(target));
        }
        GUILayout.EndHorizontal();
        if (GUILayout.Button("Revert All Morphs (HOST)"))
        {
            MalumMenu.notifications.Send("Morph", Cheats.MorphTools.RevertAll());
        }
        if (GUILayout.Button("CLONE (HOST)"))
        {
            MalumMenu.notifications.Send("Clones", Cheats.NetworkedClones.CloneOf(target));
        }

        GUILayout.BeginHorizontal();
        if (GUILayout.Button($"Force Role: {Cheats.ForceRoles.Name(Cheats.ForceRoles.IndexOf(target.PlayerId))}"))
        {
            Cheats.ForceRoles.Cycle(target.PlayerId);
        }
        if (GUILayout.Button("SET ROLE (HOST)"))
        {
            MalumMenu.notifications.Send("Force Role", Cheats.ForceRoles.ForceNow(target.PlayerId));
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("WHISPER"))
        {
            Cheats.ChatTools.Whisper.Prefill(Cheats.ChatTools.MuteList.Strip(target.Data.PlayerName));
        }
        string muteLabel = Cheats.ChatTools.MuteList.IsMuted(target) ? "UNMUTE" : "MUTE";
        if (GUILayout.Button(muteLabel))
        {
            MalumMenu.notifications.Send("Mute", Cheats.ChatTools.MuteList.Toggle(target));
        }
        GUILayout.EndHorizontal();

        if (GUILayout.Button("Ender"))
        {
            MalumMenu.notifications.Send("Enderman", EndermanKill.Kill(target));
        }

        GUILayout.BeginHorizontal();
        string blindSelectLabel = BlindTools.IsSelected(target.PlayerId) ? "Unselect Blind" : "Select Blind";
        if (GUILayout.Button(blindSelectLabel))
        {
            BlindTools.ToggleSelect(target.PlayerId);
        }
        if (GUILayout.Button($"BLIND: {BlindTools.StateName(target.PlayerId)}"))
        {
            MalumMenu.notifications.Send("Blind", BlindTools.Cycle(target));
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        string ventKickLabel = VentKick.IsSelected(target.PlayerId) ? "Unselect Vent-Kick" : "Select Vent-Kick";
        if (GUILayout.Button(ventKickLabel))
        {
            VentKick.ToggleSelect(target.PlayerId);
        }
        if (GUILayout.Button("KICK"))
        {
            MalumMenu.notifications.Send("Vent Kick", VentKick.Kick(target));
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        string ventTpLabel = VentTpTools.IsMarked(target.PlayerId) ? "Unselect Vent-TP" : "Select Vent-TP";
        if (GUILayout.Button(ventTpLabel))
        {
            VentTpTools.ToggleMark(target.PlayerId);
        }
        if (GUILayout.Button($"VENT {VentTpTools.Vent}"))
        {
            MalumMenu.notifications.Send("Vent-TP", VentTpTools.Send(target, VentTpTools.Vent));
        }
        GUILayout.EndHorizontal();

 

        GUILayout.BeginHorizontal();
        string vkTargetLabel = VotekickTools.IsTarget(target.PlayerId) ? "AUTO ✓" : "AUTO";
        if (GUILayout.Button(vkTargetLabel))
        {
            VotekickTools.ToggleTarget(target.PlayerId);
        }
        if (GUILayout.Button("VOTE"))
        {
            VotekickTools.VoteOne(target);
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("GO"))
        {
            routines.PlayerFollowerRoutine.GoTo(target);
        }
        string followLabel = MalumMenu.routines.playerFollower.IsFollowing(target) ? "STOP" : "FOLLOW";
        if (GUILayout.Button(followLabel))
        {
            MalumMenu.notifications.Send("Follow", MalumMenu.routines.playerFollower.Toggle(target));
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        string petLabel = Cheats.PetHand.IsTarget(target.PlayerId) ? "PETTING ✓" : "PET";
        if (GUILayout.Button(petLabel))
        {
            MalumMenu.notifications.Send("Pet Hand", Cheats.PetHand.Grab(target));
        }
        if (GUILayout.Button("Stop"))
        {
            Cheats.PetHand.Stop();
        }
        string petFollowLabel = Cheats.PetHand.IsFollow(target.PlayerId) ? "PET-FOLLOW ✓" : "PET-FOLLOW";
        if (GUILayout.Button(petFollowLabel))
        {
            MalumMenu.notifications.Send("Pet Hand", Cheats.PetHand.Chase(target));
        }
        GUILayout.EndHorizontal();

        GUILayout.Label("Zipline Controls");
        GUILayout.BeginHorizontal();
        string rideLabel = Cheats.RideTargets.Has(target.PlayerId) ? "RIDE ✓" : "RIDE";
        if (GUILayout.Button(rideLabel))
        {
            Cheats.RideTargets.Toggle(target.PlayerId);
        }
        if (GUILayout.Button("DOWN"))
        {
            MalumMenu.notifications.Send("Zipline", Cheats.ZiplineRide.Ride(target, true));
        }
        if (GUILayout.Button("UP"))
        {
            MalumMenu.notifications.Send("Zipline", Cheats.ZiplineRide.Ride(target, false));
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(5);

        if (GUILayout.Button($"FRAME ({Cheats.FrameSabotage.SystemName(CheatToggles.frameSystemIdx)}, {CheatToggles.frameValue})"))
        {
            MalumMenu.notifications.Send("Frame Sabotage", Cheats.FrameSabotage.Send(target, Cheats.FrameSabotage.Systems[CheatToggles.frameSystemIdx % Cheats.FrameSabotage.Systems.Length], (byte)CheatToggles.frameValue));
        }

        if (GUILayout.Button("Report Body"))
        {
            AttemptReportBody(target);
        }

        GUILayout.Space(5);
        GUILayout.Label("Host Only Features:" + (AmongUsClient.Instance.AmHost ? "" : "\n(Using these will get you kicked!)"));

        string jailLabel = MalumMenu.routines.jailPlayer.IsSelected(target) ? "UNJAIL" : "JAIL";
        if (GUILayout.Button(jailLabel))
        {
            MalumMenu.routines.jailPlayer.ToggleSelect(target);
        }

        string godLabel = GodMode.IsGranted(target.PlayerId) ? "UNGOD" : "GOD";
        if (GUILayout.Button(godLabel))
        {
            MalumMenu.notifications.Send("God Mode", GodMode.Toggle(target));
        }

        if (GUILayout.Button("Force Meeting As"))
        {
            if (Utils.isHost)
            {
                Utilities.OpenMeeting(target, null);
            } else
            {
                MalumMenu.notifications.Send("Meeting Forcer", "This is a host-only cheat.");
            }
        }

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Force All Votes To"))
        {
            if (MeetingHud.Instance == null)
               {
                   MalumMenu.notifications.Send("Vote Forcer", "This option can only be used when there is an active meeting.");
            }
            else if (!Utils.isHost)
            {
                MalumMenu.notifications.Send("Vote Forcer", "This is a host-only cheat.");
            }
            else
            {
                foreach (PlayerControl player in PlayerControl.AllPlayerControls)
                {
                    PlayerVoteArea votingArea = MeetingHud.Instance.playerStates[player.PlayerId];
                    votingArea.SetVote(target.PlayerId);
                }

                MeetingHud.Instance.SetDirtyBit(1);
                MeetingHud.Instance.CheckForEndVoting();
            }
        }

        if (GUILayout.Button("Eject"))
        {
            if (!Utils.isHost)
            {
                MalumMenu.notifications.Send("Eject", "This is a host-only cheat.");
            } else
            {
                if (MeetingHud.Instance == null)
                {
                    MeetingHud.Instance = UnityEngine.Object.Instantiate<MeetingHud>(HudManager.Instance.MeetingPrefab);
                    AmongUsClient.Instance.Spawn(MeetingHud.Instance, -2, SpawnFlags.None);
                }

                MeetingHud.VoterState[] votes = Array.Empty<MeetingHud.VoterState>();
                MeetingHud.Instance.RpcVotingComplete(votes, target.Data, false, false, ushort.MinValue);
                MeetingHud.Instance.RpcClose();
            }
        }
        GUILayout.EndHorizontal();

        if (GUILayout.Button("Frame Shapeshift"))
        {
            if (!Utils.isHost)
            {
                MalumMenu.notifications.Send("Frame Shapesift", "This is a host-only cheat.");
            } else
            {
                target.StartCoroutine(ErrorReporter.GuardCoroutine(AttemptShapeshiftFrame(target), HandlingId, "AttemptShapeshiftFrame").WrapToIl2Cpp());
            }
        }

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Flood Player with Tasks"))
        {
            if (!Utils.isHost)
            {
                MalumMenu.notifications.Send("Task Flooder", "This is a host-only cheat.");
            }
            else
            {
                byte[] taskIds = new byte[255];
                for (byte i = 0; i < 255; i++)
                {
                    taskIds[i] = i;
                }
                target.Data.RpcSetTasks(taskIds);
            }
        }

        if (GUILayout.Button("Clear Tasks"))
        {
            if (!Utils.isHost)
            {
                MalumMenu.notifications.Send("Clear Tasks", "This is a host-only cheat.");
            } else
            {
                target.Data.RpcSetTasks(Array.Empty<byte>());
            }
        }

        if (GUILayout.Button("Restore Normal Tasks"))
        {
            string error = RestoreNormalTasks(target);
            if (error != null)
                MalumMenu.notifications.Send("Restore Tasks", error);
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(5);
        GUILayout.Label("Game Options Modifier:");

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Blind"))
        {
            IGameOptions gameOptions = GameOptions.CreateCloneOptions(GameManager.Instance.LogicOptions.currentGameOptions);
            gameOptions.SetFloat(FloatOptionNames.CrewLightMod, -1.0f);
            gameOptions.SetFloat(FloatOptionNames.ImpostorLightMod, -1.0f);
            GameOptions.SendGameOptionsToClient(gameOptions, target.OwnerId);
        }

        if (GUILayout.Button("Fullbright"))
        {
            IGameOptions gameOptions = GameOptions.CreateCloneOptions(GameManager.Instance.LogicOptions.currentGameOptions);
            gameOptions.SetFloat(FloatOptionNames.CrewLightMod, 1000f);
            gameOptions.SetFloat(FloatOptionNames.ImpostorLightMod, 1000f);
            GameOptions.SendGameOptionsToClient(gameOptions, target.OwnerId);
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Slow Speed"))
        {
            IGameOptions gameOptions = GameOptions.CreateCloneOptions(GameManager.Instance.LogicOptions.currentGameOptions);
            gameOptions.SetFloat(FloatOptionNames.PlayerSpeedMod, 0.1f);
            GameOptions.SendGameOptionsToClient(gameOptions, target.OwnerId);
        }

        if (GUILayout.Button("Super Speed"))
        {
            float maxSpeed = Utilities.IsAnticheatPresent() ? 3.0f : 5.0f;

            IGameOptions gameOptions = GameOptions.CreateCloneOptions(GameManager.Instance.LogicOptions.currentGameOptions);
            gameOptions.SetFloat(FloatOptionNames.PlayerSpeedMod, maxSpeed);
            GameOptions.SendGameOptionsToClient(gameOptions, target.OwnerId);
        }
        GUILayout.EndHorizontal();

        if (GUILayout.Button("Reset to Defaults"))
        {
            IGameOptions gameOptions = GameOptions.CreateCloneOptions(GameManager.Instance.LogicOptions.currentGameOptions);
            GameOptions.SendGameOptionsToClient(gameOptions, target.OwnerId);
        }

        GUILayout.Space(5);
        GUILayout.Label($"Change color to: {_selectedColor}");
        _selectedColor = (CrewmateColor)GUILayout.HorizontalSlider((float)_selectedColor, 0, 17);

        if (GUILayout.Button("Set Color"))
        {
            target.RpcSetColor((byte)_selectedColor);
        }
    }

    private static string RestoreNormalTasks(PlayerControl target)
    {
        try
        {
            if(!Utils.isHost || ShipStatus.Instance == null || target == null || target.Data == null)
                return "This is a host-only cheat, and only works in a match.";

            int common = 1;
            int shortCount = 3;
            int longCount = 1;

            var ids = new System.Collections.Generic.List<byte>();
            TakeMapTasks(ShipStatus.Instance.CommonTasks, common, ids);
            TakeMapTasks(ShipStatus.Instance.ShortTasks, shortCount, ids);
            TakeMapTasks(ShipStatus.Instance.LongTasks, longCount, ids);

            if(ids.Count == 0)
                return "No tasks on this map.";

            target.Data.RpcSetTasks(ids.ToArray());
            return null;
        }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "PlayersTab.RestoreNormalTasks: restoring normal task set"); return "Failed to restore tasks."; }
    }

    private static void TakeMapTasks(Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<NormalPlayerTask> pool, int count, System.Collections.Generic.List<byte> into)
    {
        if(pool == null)
            return;
        int n = 0;
        for(int i = 0; i < pool.Length && n < count; i++)
        {
            NormalPlayerTask t = pool[i];
            if(t == null)
                continue;
            into.Add((byte)t.Index);
            n++;
        }
    }

    private static void AttemptReportBody(PlayerControl target)
    {
        if (AmongUsClient.Instance.AmHost)
        {
            MalumMenu.Log.LogInfo($"Attempting to report {target.Data.PlayerName}'s body, we are the host so we directly use the StartMeeting RPC");
            Utilities.OpenMeeting(PlayerControl.LocalPlayer, target.Data);
            return;
        }

        MalumMenu.Log.LogInfo($"Attempting to report {target.Data.PlayerName}'s body, we are not the host so we have to use the ReportDeadBody RPC");

        if (Utilities.IsAnticheatPresent())
        {
             if (LobbyBehaviour.Instance != null)
            {
                MalumMenu.notifications.Send("Report Body", "The game must have started for this option to work.");
                return;
            }

             if (!target.Data.IsDead)
            {
                MalumMenu.notifications.Send("Report Body", "You can only report bodies of players who have died in this round.");
                return;
            }

            bool bodyExists = false;
            foreach (Collider2D collider in Physics2D.OverlapCircleAll(new Vector2(0, 0), 99999f, Constants.PlayersOnlyMask))
            {
                if (collider.tag != "DeadBody") continue;

                DeadBody bodyComponent = collider.GetComponent<DeadBody>();
                if (bodyComponent && bodyComponent.ParentId == target.PlayerId)
                {
                    bodyExists = true;
                    break;
                }
            }

             if (!bodyExists)
            {
                MalumMenu.notifications.Send("Report Body", "Unable to find a dead body for this player, you can only report a player's body if they have died this round and their body has not dissolved.");
                return;
            }
        }

        MalumMenu.Log.LogInfo($"All checks passed, we are able to report {target.Data.PlayerName}'s body.");

        PlayerControl.LocalPlayer.CmdReportDeadBody(target.Data);
    }

    private static IEnumerator AttemptShapeshiftFrame(PlayerControl target)
    {
        bool hasAnticheat = Utilities.IsAnticheatPresent();
        if (ShipStatus.Instance == null && hasAnticheat)
        {
            MalumMenu.notifications.Send("Framer", "The game must have started for this option to work.");
            yield break;
        }

        PlayerControl randomPl = Utilities.GetRandomPlayer(false, false, false, false);

        if (target.Data.RoleType != RoleTypes.Shapeshifter && hasAnticheat)
        {
            RoleTypes currentRole = target.Data.RoleType;

            target.RpcSetRole(RoleTypes.Shapeshifter, true);
            yield return Effects.Wait(0.5f);
            target.RpcShapeshift(randomPl, true);
            target.RpcSetRole(currentRole, true);
        }
        else
        {
            target.RpcShapeshift(randomPl, true);
        }
    }
}

public static class PlayersSection
{
    public static PlayerControl selectedPlayer;
}
