using UnityEngine;
using MalumMenu.features;

namespace MalumMenu;

public class ProtectionsTab : ITab
{
    public string name => "Protections";

    public void Draw()
    {
        GUILayout.BeginVertical(GUILayout.Width(MenuUI.windowWidth * 0.425f));

        // Réseau
        Protections.ForceDTLS.Enabled = GUILayout.Toggle(Protections.ForceDTLS.Enabled, "Forcer l'activation de DTLS pour chiffrer les données réseau");

        Protections.BlockServerTeleports.Enabled = GUILayout.Toggle(Protections.BlockServerTeleports.Enabled, "Bloquer les mises à jour de position venant du serveur");

        // Surcharges
        Protections.HardenedReadPackedUInt.Enabled = GUILayout.Toggle(Protections.HardenedReadPackedUInt.Enabled, "Utiliser un désérialiseur d'entiers compactés renforcé");
        Protections.BlockLargeGameMessages = GUILayout.Toggle(Protections.BlockLargeGameMessages, "Bloquer les gros messages de jeu");
        Protections.BlockInvalidGameDataMessages = GUILayout.Toggle(Protections.BlockInvalidGameDataMessages, "Bloquer les messages de données de jeu invalides");
        Protections.BlockUnauthorizedSystemUpdates = GUILayout.Toggle(Protections.BlockUnauthorizedSystemUpdates, "Bloquer les mises à jour système non autorisées");
        Protections.ProtectAgainstNonHostKickExploit = GUILayout.Toggle(Protections.ProtectAgainstNonHostKickExploit, "Se protéger contre l'exploit de kick non-hôte");

        Protections.Votekicks.Enabled = GUILayout.Toggle(Protections.Votekicks.Enabled, "Empêcher d'être vote-kick en tant qu'hôte");

        GUILayout.EndVertical();
    }
}