using UnityEngine;

namespace MalumMenu;

public class HostOnlyTab : ITab
{
    public string name => "Hôte uniquement";

    public void Draw()
    {
        GUILayout.BeginHorizontal();

        GUILayout.BeginVertical(GUILayout.Width(MenuUI.windowWidth * 0.425f));

        if (PlayerControl.LocalPlayer == null)
        {
            GUILayout.Label("Tu n'es pas actuellement en partie, ces options ne fonctionneront pas.");
        }
        else if (!AmongUsClient.Instance.AmHost)
        {
            GUILayout.Label("Tu n'es pas l'hôte du salon actuel. Utiliser ces options ne fera rien ou te fera bannir par l'anticheat");
        }

        DrawGeneral();

        GUILayout.Space(15);

        DrawMurder();

        GUILayout.Space(15);

        DrawGameState();

        GUILayout.EndVertical();

        GUILayout.BeginVertical();

        DrawMeetings();

        GUILayout.EndVertical();

        GUILayout.EndHorizontal();
    }

    private void DrawGeneral()
    {
        CheatToggles.bypassHostOnly = GUILayout.Toggle(CheatToggles.bypassHostOnly, " Contourner Hôte uniquement");

        GUILayout.Space(5);

        CheatToggles.killVanished = GUILayout.Toggle(CheatToggles.killVanished, " Tuer en étant invisible");

        CheatToggles.killAnyone = GUILayout.Toggle(CheatToggles.killAnyone, " Tuer n'importe qui");

        CheatToggles.noKillCd = GUILayout.Toggle(CheatToggles.noKillCd, " Pas de délai de kill");

        CheatToggles.showProtectMenu = GUILayout.Toggle(CheatToggles.showProtectMenu, " Afficher le menu de protection");

        // CheatToggles.forceRole = GUILayout.Toggle(CheatToggles.forceRole, " Forcer le rôle");

        // CheatToggles.noOptionsLimits = GUILayout.Toggle(CheatToggles.noOptionsLimits, " Aucune limite d'options");
    }

    private void DrawMurder()
    {
        GUILayout.Label("Meurtre", GUIStylePreset.TabSubtitle);

        CheatToggles.killPlayer = GUILayout.Toggle(CheatToggles.killPlayer, " Tuer un joueur");

        CheatToggles.telekillPlayer = GUILayout.Toggle(CheatToggles.telekillPlayer, " Tuer par téléportation");

        CheatToggles.killAllCrew = GUILayout.Toggle(CheatToggles.killAllCrew, " Tuer tous les équipiers");

        CheatToggles.killAllImps = GUILayout.Toggle(CheatToggles.killAllImps, " Tuer tous les imposteurs");

        CheatToggles.killAll = GUILayout.Toggle(CheatToggles.killAll, " Tuer tout le monde");
    }

    private void DrawGameState()
    {
        GUILayout.Label("État de la partie", GUIStylePreset.TabSubtitle);

        CheatToggles.forceStartGame = GUILayout.Toggle(CheatToggles.forceStartGame, " Forcer le démarrage");

        CheatToggles.noGameEnd = GUILayout.Toggle(CheatToggles.noGameEnd, " Empêcher la fin de partie");
    }

    private void DrawMeetings()
    {
        GUILayout.Label("Réunions", GUIStylePreset.TabSubtitle);

        CheatToggles.skipMeeting = GUILayout.Toggle(CheatToggles.skipMeeting, " Passer la réunion");

        CheatToggles.voteImmune = GUILayout.Toggle(CheatToggles.voteImmune, " Immunisé au vote");

        CheatToggles.ejectPlayer = GUILayout.Toggle(CheatToggles.ejectPlayer, " Éjecter un joueur");
    }
}