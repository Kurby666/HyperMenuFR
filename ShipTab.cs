using UnityEngine;

namespace MalumMenu;

public class ShipTab : ITab
{
    public string name => "Vaisseau";

    public void Draw()
    {
        GUILayout.BeginHorizontal();

        GUILayout.BeginVertical(GUILayout.Width(MenuUI.windowWidth * 0.425f));

        DrawGeneral();

        GUILayout.Space(15);

        DrawSabotage();

        GUILayout.EndVertical();

        GUILayout.BeginVertical();

        DrawVents();

        GUILayout.EndVertical();

        GUILayout.EndHorizontal();
    }

    private void DrawGeneral()
    {
        // Sera implémenté plus tard, actuellement l'anticheat kick l'utilisateur. -ADHyperActive
        // CheatToggles.completeAllTasks = GUILayout.Toggle(CheatToggles.completeAllTasks, " Autoriser toutes les tâches");
        
        CheatToggles.fakeTasks = GUILayout.Toggle(CheatToggles.fakeTasks, " Fausses tâches");

        CheatToggles.doAnyTask = GUILayout.Toggle(CheatToggles.doAnyTask, " Faire n'importe quelle tâche");

        CheatToggles.unfixableLights = GUILayout.Toggle(CheatToggles.unfixableLights, " Lumières irréparables");

        CheatToggles.callMeeting = GUILayout.Toggle(CheatToggles.callMeeting, " Appeler une réunion");

        CheatToggles.reportBody = GUILayout.Toggle(CheatToggles.reportBody, " Signaler un corps");

        CheatToggles.closeMeeting = GUILayout.Toggle(CheatToggles.closeMeeting, " Fermer la réunion");

        CheatToggles.autoReportBodies = GUILayout.Toggle(CheatToggles.autoReportBodies, " Signalement auto des corps");

        CheatToggles.autoOpenDoorsOnUse = GUILayout.Toggle(CheatToggles.autoOpenDoorsOnUse, " Ouverture auto des portes");

        CheatToggles.kickOffensiveNames = GUILayout.Toggle(CheatToggles.kickOffensiveNames, " Expulser les pseudos offensants");
    }

    private void DrawSabotage()
    {
        GUILayout.Label("Sabotage", GUIStylePreset.TabSubtitle);

        CheatToggles.reactorSab = GUILayout.Toggle(CheatToggles.reactorSab, " Réacteur");

        CheatToggles.oxygenSab = GUILayout.Toggle(CheatToggles.oxygenSab, " Oxygène");

        CheatToggles.elecSab = GUILayout.Toggle(CheatToggles.elecSab, " Lumières");

        CheatToggles.commsSab = GUILayout.Toggle(CheatToggles.commsSab, " Communications");

        CheatToggles.showDoorsMenu = GUILayout.Toggle(CheatToggles.showDoorsMenu, " Afficher le menu des portes");

        CheatToggles.mushSab = GUILayout.Toggle(CheatToggles.mushSab, " Mélange de champignons");

        CheatToggles.mushSpore = GUILayout.Toggle(CheatToggles.mushSpore, " Déclencher les spores");

        CheatToggles.sabotageMap = GUILayout.Toggle(CheatToggles.sabotageMap, " Ouvrir la carte de sabotage");
    }

    private void DrawVents()
    {
        GUILayout.Label("Conduits", GUIStylePreset.TabSubtitle);

        CheatToggles.unlockVents = GUILayout.Toggle(CheatToggles.unlockVents, " Débloquer les conduits");

        CheatToggles.kickVents = GUILayout.Toggle(CheatToggles.kickVents, " Éjecter tout le monde des conduits");

        CheatToggles.walkInVents = GUILayout.Toggle(CheatToggles.walkInVents, " Marcher dans les conduits");
    }
}