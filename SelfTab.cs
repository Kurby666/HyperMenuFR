using System.Collections;
using UnityEngine;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using MalumMenu.features;

namespace MalumMenu
{
    internal class SelfTab : ITab
    {
        public string name => "Soi-même";

        private uint level = 0;

        public void Draw()
        {
            GUILayout.BeginVertical(GUILayout.Width(MenuUI.windowWidth * 0.425f));
            if (PlayerControl.LocalPlayer == null || PlayerControl.LocalPlayer.Data == null)
            {
                GUILayout.Label("Tu n'es pas actuellement en partie, ces options ne fonctionneront pas.");
            }
            else
            {
                GUILayout.Label($"Rôle : {PlayerControl.LocalPlayer.Data.RoleType}");
            }

            // Self.BypassIntentionalDisconnectionBlocks.Enabled = GUILayout.Toggle(Self.BypassIntentionalDisconnectionBlocks.Enabled, "Contourner les bans temporaires de déconnexion intentionnelle");
            Self.UpdateStatsFreeplay.Enabled = GUILayout.Toggle(Self.UpdateStatsFreeplay.Enabled, "Mettre à jour les stats en Freeplay");
            Immortality.Enabled = GUILayout.Toggle(Immortality.Enabled, "Devenir immortel");
            Self.AlwaysShowTaskAnimations = GUILayout.Toggle(Self.AlwaysShowTaskAnimations, "Toujours afficher les animations de tâches");
            Self.NoLadderCooldown.Enabled = GUILayout.Toggle(Self.NoLadderCooldown.Enabled, "Pas de délai d'échelle");
            Self.UnlimitedMeetings.enabled = GUILayout.Toggle(Self.UnlimitedMeetings.enabled, "Réunions illimitées");

            if (GUILayout.Button("Appeler une réunion"))
            {
                if (AmongUsClient.Instance.AmHost)
                {
                    MalumMenu.Log.LogInfo("We are the host, we can force a meeting");
                    Utilities.OpenMeeting(PlayerControl.LocalPlayer, null);
                }
                else
                {
                    PlayerControl.LocalPlayer.CmdReportDeadBody(null);
                }
            }

            if (GUILayout.Button("Terminer toutes les tâches"))
            {
                PlayerControl.LocalPlayer.StartCoroutine(CompleteAllTasks().WrapToIl2Cpp());
            }

            if (GUILayout.Button("Avatar aléatoire"))
            {
                if (AmongUsClient.Instance.AmConnected)
                {
                    Utilities.RandomizePlayer(true);

                    MalumMenu.notifications.Send("Randomiseur de joueur", "Ton avatar a été randomisé pour cette partie.", 5);
                }
                else
                {
                    AccountManager.Instance.RandomizeName();
                    Utilities.RandomizePlayer();

                    MalumMenu.notifications.Send("Randomiseur de joueur", "Ton nom et ton avatar ont été randomisés.", 5);
                }
            }

            GUILayout.Label("Animations de tâches :");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Démarrer le scan Medbay"))
            {
                if (!Utils.isLobby)
                {
                    Network.RPCEmitter.SendSetScanner(true);
                }else
                {
                    MalumMenu.notifications.Send("Avertissement anticheat", "Cette triche est désactivée dans le salon à cause de la détection anticheat. Tu peux l'utiliser une fois la partie lancée.");
                }
            }

            if (GUILayout.Button("Terminer le scan Medbay"))
            {
                if (!Utils.isLobby)
                {
                    Network.RPCEmitter.SendSetScanner(false);
                }else
                {
                    MalumMenu.notifications.Send("Avertissement anticheat", "Cette triche est désactivée dans le salon à cause de la détection anticheat. Tu peux l'utiliser une fois la partie lancée.");
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Détruire les astéroïdes"))
            {
                if (!Utils.isLobby)
                {
                    Network.RPCEmitter.SendPlayAnimation((byte)TaskTypes.ClearAsteroids);
                }else
                {
                    MalumMenu.notifications.Send("Avertissement anticheat", "Cette triche est désactivée dans le salon à cause de la détection anticheat. Tu peux l'utiliser une fois la partie lancée.");
                }
            }

            if (GUILayout.Button("Vider les poubelles"))
            {
                if (!Utils.isLobby)
                {
                    Network.RPCEmitter.SendPlayAnimation((byte)TaskTypes.EmptyGarbage);
                }else
                {
                    MalumMenu.notifications.Send("Avertissement anticheat", "Cette triche est désactivée dans le salon à cause de la détection anticheat. Tu peux l'utiliser une fois la partie lancée.");
                }
            }
            GUILayout.EndHorizontal();

            if (GUILayout.Button("Activer les boucliers"))
            {
                if (!Utils.isLobby)
                {
                    Network.RPCEmitter.SendPlayAnimation((byte)TaskTypes.PrimeShields);
                } else
                {
                    MalumMenu.notifications.Send("Avertissement anticheat", "Cette triche est désactivée dans le salon à cause de la détection anticheat. Tu peux l'utiliser une fois la partie lancée.");
                }
            }

            GUILayout.Space(5);
            GUILayout.Label($"Mettre le niveau à : {level + 1}");
            level = (uint)GUILayout.HorizontalSlider(level, 0, 199);

            if (GUILayout.Button("Envoyer la mise à jour du niveau"))
            {
                PlayerControl.LocalPlayer.RpcSetLevel(level);
                MalumMenu.notifications.Send("Mise à jour du niveau", $"Ton niveau a été changé à {level + 1}", 5);
            }
            GUILayout.EndVertical();
        }
        private IEnumerator CompleteAllTasks()
        {
            Il2CppSystem.Collections.Generic.List<PlayerTask> allTasks = PlayerControl.LocalPlayer.myTasks;

            MalumMenu.Log.LogInfo("Completing all tasks...");
            foreach (PlayerTask task in allTasks)
            {
                if (task.IsComplete)
                {
                    MalumMenu.Log.LogInfo($"Task {task.Id} has already been completed, skipping");
                    continue;
                }

                MalumMenu.Log.LogInfo($"Sent CompleteTask RPC for task {task.Id}");
                PlayerControl.LocalPlayer.RpcCompleteTask(task.Id);

                // Si on veut compléter plus de six tâches, un délai est nécessaire
                // sinon l'anticheat vanilla nous kick pour violation du ratelimit
                yield return Effects.Wait(0.05f);
            }

            MalumMenu.notifications.Send("Termineur de tâches", "Toutes tes tâches ont été terminées.", 5);
        }

    }
}