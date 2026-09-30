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
    public string name => "Joueurs";

    private Vector2 _subsectionScrollVector = Vector2.zero;
    private Vector2 _subsectionScrollVector2 = Vector2.zero;
    private static CrewmateColor _selectedColor = CrewmateColor.Red;

    public void Draw()
    {
        if (PlayerControl.AllPlayerControls.Count == 0)
        {
            GUILayout.Label("Il n'y a actuellement aucun joueur en ligne.");
            return;
        }

        GUILayout.BeginHorizontal();

        // Panneau gauche : Liste des joueurs
        GUILayout.BeginVertical(GUILayout.Width(MenuUI.windowWidth * 0.35f));
        _subsectionScrollVector = GUILayout.BeginScrollView(_subsectionScrollVector);
        DrawPlayerList();
        GUILayout.EndScrollView();
        GUILayout.EndVertical();

        // Panneau droit : Contrôles du joueur
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
            GUILayout.Label("La cible spécifiée n'est pas valide.");
            return;
        }

        ClientData clientData = AmongUsClient.Instance.GetClientFromCharacter(target);
        if (clientData != null)
        {
            PlatformSpecificData platform = clientData.PlatformData;
            bool streamerMode = DataManager.Settings.Gameplay.StreamerMode;

            GUILayout.Label(
                $"Nom : {target.Data.PlayerName} {target.Data.ColorName}" +
                $"\nRôle : {target.Data.RoleType}" +
                $"\nÉtat : " + (target.Data.IsDead ? "Mort" : "Vivant") +
                $"\nCode ami : " + (streamerMode ? "CACHÉ" : target.Data.FriendCode) +
                $"\nPUID : " + (streamerMode ? "CACHÉ" : target.Data.Puid) +
                $"\nNiveau : {target.Data.PlayerLevel + 1}" +
                $"\nAppareil : {platform.Platform}" +
                (target.OwnerId == AmongUsClient.Instance.HostId ? "\nHôte : vrai" : "")
            );
        }
        else
        {
            GUILayout.Label(
                $"Nom : {target.Data.PlayerName} {target.Data.ColorName}" +
                $"\nRôle : {target.Data.RoleType}" +
                $"\nÉtat : " + (target.Data.IsDead ? "Mort" : "Vivant") +
                $"\nEst un bot : vrai"
            );
        }

        if (GUILayout.Button("Téléporter"))
        {
            Teleporter.TeleportTo(target.transform.position);
        }

        if (GUILayout.Button("Tuer"))
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

        if (GUILayout.Button("Copier l'avatar"))
        {
            Utilities.CopyPlayer(target);
        }

        if (GUILayout.Button("Signaler le corps"))
        {
            AttemptReportBody(target);
        }

        GUILayout.Space(5);
        GUILayout.Label("Fonctionnalités réservées à l'hôte :" + (AmongUsClient.Instance.AmHost ? "" : "\n(Les utiliser te fera expulser !)"));

        if (GUILayout.Button("Forcer une réunion en tant que"))
        {
            if (Utils.isHost)
            {
                Utilities.OpenMeeting(target, null);
            } else
            {
                MalumMenu.notifications.Send("Forceur de réunion", "C'est une triche réservée à l'hôte.");
            }
        }

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Forcer tous les votes sur"))
        {
            if (MeetingHud.Instance == null)
               {
                   MalumMenu.notifications.Send("Forceur de vote", "Cette option ne peut être utilisée que pendant une réunion active.");
            }
            else if (!Utils.isHost)
            {
                MalumMenu.notifications.Send("Forceur de vote", "C'est une triche réservée à l'hôte.");
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

        if (GUILayout.Button("Éjecter"))
        {
            if (!Utils.isHost)
            {
                MalumMenu.notifications.Send("Éjecter", "C'est une triche réservée à l'hôte.");
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

        if (GUILayout.Button("Frame métamorphose"))
        {
            if (!Utils.isHost)
            {
                MalumMenu.notifications.Send("Frame Shapesift", "C'est une triche réservée à l'hôte.");
            } else
            {
                target.StartCoroutine(AttemptShapeshiftFrame(target).WrapToIl2Cpp());
            }
        }

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Inonder le joueur de tâches"))
        {
            if (!Utils.isHost)
            {
                MalumMenu.notifications.Send("Inondeur de tâches", "C'est une triche réservée à l'hôte.");
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

        if (GUILayout.Button("Effacer les tâches"))
        {
            if (!Utils.isHost)
            {
                MalumMenu.notifications.Send("Effacer les tâches", "C'est une triche réservée à l'hôte.");
            } else
            {
                target.Data.RpcSetTasks(Array.Empty<byte>());
            }
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(5);
        GUILayout.Label("Modificateur d'options de jeu :");

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Aveugler"))
        {
            IGameOptions gameOptions = GameOptions.CreateCloneOptions(GameManager.Instance.LogicOptions.currentGameOptions);
            gameOptions.SetFloat(FloatOptionNames.CrewLightMod, -1.0f);
            gameOptions.SetFloat(FloatOptionNames.ImpostorLightMod, -1.0f);
            GameOptions.SendGameOptionsToClient(gameOptions, target.OwnerId);
        }

        if (GUILayout.Button("Pleine luminosité"))
        {
            IGameOptions gameOptions = GameOptions.CreateCloneOptions(GameManager.Instance.LogicOptions.currentGameOptions);
            gameOptions.SetFloat(FloatOptionNames.CrewLightMod, 1000f);
            gameOptions.SetFloat(FloatOptionNames.ImpostorLightMod, 1000f);
            GameOptions.SendGameOptionsToClient(gameOptions, target.OwnerId);
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Vitesse lente"))
        {
            IGameOptions gameOptions = GameOptions.CreateCloneOptions(GameManager.Instance.LogicOptions.currentGameOptions);
            gameOptions.SetFloat(FloatOptionNames.PlayerSpeedMod, 0.1f);
            GameOptions.SendGameOptionsToClient(gameOptions, target.OwnerId);
        }

        if (GUILayout.Button("Super vitesse"))
        {
            float maxSpeed = Utilities.IsAnticheatPresent() ? 3.0f : 5.0f;

            IGameOptions gameOptions = GameOptions.CreateCloneOptions(GameManager.Instance.LogicOptions.currentGameOptions);
            gameOptions.SetFloat(FloatOptionNames.PlayerSpeedMod, maxSpeed);
            GameOptions.SendGameOptionsToClient(gameOptions, target.OwnerId);
        }
        GUILayout.EndHorizontal();

        if (GUILayout.Button("Réinitialiser aux valeurs par défaut"))
        {
            IGameOptions gameOptions = GameOptions.CreateCloneOptions(GameManager.Instance.LogicOptions.currentGameOptions);
            GameOptions.SendGameOptionsToClient(gameOptions, target.OwnerId);
        }

        GUILayout.Space(5);
        GUILayout.Label($"Changer la couleur en : {_selectedColor}");
        _selectedColor = (CrewmateColor)GUILayout.HorizontalSlider((float)_selectedColor, 0, 17);

        if (GUILayout.Button("Définir la couleur"))
        {
            target.RpcSetColor((byte)_selectedColor);
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
                MalumMenu.notifications.Send("Signaler le corps", "La partie doit avoir commencé pour que cette option fonctionne.");
                return;
            }

             if (!target.Data.IsDead)
            {
                MalumMenu.notifications.Send("Signaler le corps", "Tu ne peux signaler que les corps des joueurs morts dans cette manche.");
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
                MalumMenu.notifications.Send("Signaler le corps", "Impossible de trouver un corps pour ce joueur, tu ne peux signaler un corps que s'il est mort dans cette manche et que son corps n'a pas disparu.");
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
            MalumMenu.notifications.Send("Framer", "La partie doit avoir commencé pour que cette option fonctionne.");
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