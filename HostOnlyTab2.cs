using System.Collections;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using MalumMenu.features;
using InnerNet;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;


namespace MalumMenu;

public class HostOnlyTab2 : ITab
{
    public string name => "Hôte uniquement 2";

    private byte selectedMap = 0;

    public void Draw()
    {
        if (PlayerControl.LocalPlayer == null)
        {
            GUILayout.Label("Tu n'es pas actuellement en partie, ces options ne fonctionneront pas.");
        }
        else if (!AmongUsClient.Instance.AmHost)
        {
            GUILayout.Label("Tu n'es pas l'hôte du salon actuel. Utiliser ces options ne fera rien ou te fera bannir par l'anticheat");
        }
        Host.BanMidGame.Enabled = GUILayout.Toggle(Host.BanMidGame.Enabled, "Pouvoir bannir les joueurs en pleine partie");

        Host.FlippedSkeld = GUILayout.Toggle(Host.FlippedSkeld, "Utiliser la carte Skeld inversée");

        Host.DisableMeetings.Enabled = GUILayout.Toggle(Host.DisableMeetings.Enabled, "Désactiver les réunions");
        Host.DisableSabotages.Enabled = GUILayout.Toggle(Host.DisableSabotages.Enabled, "Désactiver les sabotages");
        Host.DisableCloseDoors.Enabled = GUILayout.Toggle(Host.DisableCloseDoors.Enabled, "Désactiver la fermeture des portes");
        Host.DisableCameras.Enabled = GUILayout.Toggle(Host.DisableCameras.Enabled, "Désactiver les caméras de sécurité");
        Host.DisableGameEnd.Enabled = GUILayout.Toggle(Host.DisableGameEnd.Enabled, "Désactiver la fin de partie");
        Host.NoKillCooldown.Enabled = GUILayout.Toggle(Host.NoKillCooldown.Enabled, "Pas de délai de kill");

        GUILayout.BeginHorizontal();
        Host.BlockLowLevels.Enabled = GUILayout.Toggle(Host.BlockLowLevels.Enabled, $"Expulser les joueurs de moins de {Host.BlockLowLevels.MinLevel} niveaux");
        Host.BlockLowLevels.MinLevel = (uint)GUILayout.HorizontalSlider(Host.BlockLowLevels.MinLevel, 0, 100);
        GUILayout.EndHorizontal();

        MalumMenu.routines.reportBodySpam.Enabled = GUILayout.Toggle(MalumMenu.routines.reportBodySpam.Enabled, "Spam de signalement de corps");

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Forcer la victoire des équipiers"))
        {
            // Au cas où l'utilisateur aurait ceci activé
            Host.DisableGameEnd.Enabled = false;

            GameManager.Instance.RpcEndGame(GameOverReason.CrewmatesByTask, false);
            MalumMenu.notifications.Send("Partie terminée", "Tu as terminé la partie avec une victoire des équipiers.", 5);
        }

        if (GUILayout.Button("Forcer la victoire des imposteurs"))
        {
            // Au cas où l'utilisateur aurait ceci activé
            Host.DisableGameEnd.Enabled = false;

            GameManager.Instance.RpcEndGame(GameOverReason.ImpostorsByKill, false);
            MalumMenu.notifications.Send("Partie terminée", "Tu as terminé la partie avec une victoire des imposteurs.", 5);
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(5);
        GUILayout.Label("Générateur/Suppresseur de carte :");

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Supprimer le salon"))
        {
            if (LobbyBehaviour.Instance != null)
            {
                LobbyBehaviour.Instance.Despawn();
                MalumMenu.notifications.Send("Carte du salon", "La carte du salon a été supprimée.", 5);
            }
            else
            {
                MalumMenu.notifications.Send("Carte du salon", "La carte du salon a déjà été supprimée.", 5);
            }
        }

        if (GUILayout.Button("Générer le salon"))
        {
            // Depuis GameStartManager::Start
            LobbyBehaviour.Instance = Object.Instantiate<LobbyBehaviour>(GameStartManager.Instance.LobbyPrefab);
            AmongUsClient.Instance.Spawn(LobbyBehaviour.Instance, -2, SpawnFlags.None);

            MalumMenu.notifications.Send("Carte du salon", "Une nouvelle instance de la carte du salon a été générée", 5);
        }
        GUILayout.EndHorizontal();

        GUILayout.Label($"Carte sélectionnée : {(MapNames)selectedMap}");
        selectedMap = (byte)GUILayout.HorizontalSlider(selectedMap, 0, 5);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Supprimer la carte"))
        {
            if (ShipStatus.Instance != null)
            {
                ShipStatus.Instance.Despawn();
                MalumMenu.notifications.Send("Carte de jeu", "La carte actuelle a été supprimée.", 5);
            }
            else
            {
                MalumMenu.notifications.Send("Carte de jeu", "La carte de jeu a déjà été supprimée.", 5);
            }
        }

        if (GUILayout.Button("Générer la carte"))
        {
            AmongUsClient.Instance.StartCoroutine(SpawnMap(selectedMap).WrapToIl2Cpp());
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(5);

        GUILayout.Label("Disco Party :");
        MalumMenu.routines.discoHost.Enabled = GUILayout.Toggle(MalumMenu.routines.discoHost.Enabled, "Activé");
        GUILayout.Label($"Délai de randomisation des couleurs : {MalumMenu.routines.discoHost.randomizationDelay:F2}s");
        MalumMenu.routines.discoHost.randomizationDelay = GUILayout.HorizontalSlider(MalumMenu.routines.discoHost.randomizationDelay, 0.1f, 2.0f);
    }
    private static IEnumerator SpawnMap(byte mapId)
    {
        MalumMenu.Log.LogInfo($"Attempting to spawn in map id {mapId}");

        AsyncOperationHandle<GameObject> asyncHandle = AmongUsClient.Instance.ShipPrefabs[mapId].InstantiateAsync(null, false);
        yield return asyncHandle;

        ShipStatus ship = asyncHandle.Result.GetComponent<ShipStatus>();
        AmongUsClient.Instance.Spawn(ship, -2, SpawnFlags.None);

        MalumMenu.notifications.Send("Générateur de carte", $"{(MapNames)mapId} a été généré.", 5);
    }
}