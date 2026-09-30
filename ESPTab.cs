using UnityEngine;
using MalumMenu.features;

namespace MalumMenu;

public class ESPTab : ITab
{
    public string name => "ESP";

    public void Draw()
    {
        GUILayout.BeginHorizontal();

        GUILayout.BeginVertical(GUILayout.Width(MenuUI.windowWidth * 0.425f));

        DrawGeneral();

        GUILayout.Space(15);

        DrawCamera();

        GUILayout.EndVertical();

        GUILayout.BeginVertical();

        DrawTracers();

        GUILayout.Space(15);

        DrawMinimap();

        GUILayout.EndVertical();

        GUILayout.EndHorizontal();
    }

    private void DrawGeneral()
    {
        CheatToggles.seePlayerInfo = GUILayout.Toggle(CheatToggles.seePlayerInfo, " Voir les infos des joueurs");

        CheatToggles.seeRoles = GUILayout.Toggle(CheatToggles.seeRoles, " Voir les rôles");

        CheatToggles.seeGhosts = GUILayout.Toggle(CheatToggles.seeGhosts, " Voir les fantômes");

        CheatToggles.noShadows = GUILayout.Toggle(CheatToggles.noShadows, " Pas d'ombres");

        CheatToggles.taskArrows = GUILayout.Toggle(CheatToggles.taskArrows, " Flèches des tâches");

        CheatToggles.revealVotes = GUILayout.Toggle(CheatToggles.revealVotes, " Révéler les votes");

        CheatToggles.seeLobbyInfo = GUILayout.Toggle(CheatToggles.seeLobbyInfo, " Voir les infos du salon");

        Visuals.SkipShhhAnimation.Enabled = GUILayout.Toggle(Visuals.SkipShhhAnimation.Enabled, "Passer l'animation Shhh");

        Visuals.AccurateDisconnectReasons.Enabled = GUILayout.Toggle(Visuals.AccurateDisconnectReasons.Enabled, "Utiliser des raisons de déconnexion plus précises");

        Visuals.ShowProtections.Enabled = GUILayout.Toggle(Visuals.ShowProtections.Enabled, "Afficher les protections de l'Ange Gardien");
    }

    private void DrawCamera()
    {
        GUILayout.Label("Caméra", GUIStylePreset.TabSubtitle);

        CheatToggles.zoomOut = GUILayout.Toggle(CheatToggles.zoomOut, " Dézoom");

        CheatToggles.spectate = GUILayout.Toggle(CheatToggles.spectate, " Mode spectateur");

        CheatToggles.freecam = GUILayout.Toggle(CheatToggles.freecam, " Caméra libre");
    }

    private void DrawTracers()
    {
        GUILayout.Label("Traceurs", GUIStylePreset.TabSubtitle);

        CheatToggles.tracersCrew = GUILayout.Toggle(CheatToggles.tracersCrew, " Équipiers");

        CheatToggles.tracersImps = GUILayout.Toggle(CheatToggles.tracersImps, " Imposteurs");

        CheatToggles.tracersGhosts = GUILayout.Toggle(CheatToggles.tracersGhosts, " Fantômes");

        CheatToggles.tracersBodies = GUILayout.Toggle(CheatToggles.tracersBodies, " Corps sans vie");

        CheatToggles.colorBasedTracers = GUILayout.Toggle(CheatToggles.colorBasedTracers, " Basé sur la couleur");

        CheatToggles.distanceBasedTracers = GUILayout.Toggle(CheatToggles.distanceBasedTracers, " Basé sur la distance");
    }

    private void DrawMinimap()
    {
        GUILayout.Label("Mini-carte", GUIStylePreset.TabSubtitle);

        CheatToggles.mapCrew = GUILayout.Toggle(CheatToggles.mapCrew, " Équipiers");

        CheatToggles.mapImps = GUILayout.Toggle(CheatToggles.mapImps, " Imposteurs");

        CheatToggles.mapGhosts = GUILayout.Toggle(CheatToggles.mapGhosts, " Fantômes");

        CheatToggles.colorBasedMap = GUILayout.Toggle(CheatToggles.colorBasedMap, " Basé sur la couleur");
    }
}