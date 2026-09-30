using MalumMenu.anticheat;
using UnityEngine;

namespace MalumMenu
{
    internal class AnticheatTab : ITab
    {
        public string name => "Anticheat";

        public void Draw()
        {
            Anticheat.Enabled = GUILayout.Toggle(Anticheat.Enabled, "Activer l'Anticheat HyperMenu");

            Anticheat.CheckSpoofedPlatforms = GUILayout.Toggle(Anticheat.CheckSpoofedPlatforms, "Signaler les données de plateforme falsifiées");

            GUILayout.Space(5);
            GUILayout.Label("RPCs qui doivent être vérifiés par l'anticheat :");
            foreach (var (rpcCall, handler) in Anticheat.RpcHandlers)
            {
                handler.Enabled = GUILayout.Toggle(handler.Enabled, $"{rpcCall}");
            }

            GUILayout.Space(5);
            GUILayout.Label("Quand un tricheur est détecté :");
            Anticheat.sendNotification = GUILayout.Toggle(Anticheat.sendNotification, "Envoyer une notification");
            Anticheat.discardRpc = GUILayout.Toggle(Anticheat.discardRpc, "Rejeter le RPC");

            GUILayout.BeginHorizontal();
            GUILayout.Label($"Punir le joueur avec : {Anticheat.punishment}");
            Anticheat.punishment = (Anticheat.Punishments)GUILayout.HorizontalSlider((float)Anticheat.punishment, 0, 3);
            GUILayout.EndHorizontal();
        }
    }
}