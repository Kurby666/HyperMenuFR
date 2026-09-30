using UnityEngine;

namespace MalumMenu;

public class ConsoleTab : ITab
{
    public string name => "Console";

    public void Draw()
    {
        GUILayout.BeginVertical(GUILayout.Width(MenuUI.windowWidth * 0.425f));

        DrawGeneral();

        GUILayout.EndVertical();
    }

    private void DrawGeneral()
    {
        CheatToggles.showConsole = GUILayout.Toggle(CheatToggles.showConsole, " Afficher la console");

        CheatToggles.logDeaths = GUILayout.Toggle(CheatToggles.logDeaths, " Enregistrer les morts");

        CheatToggles.logShapeshifts = GUILayout.Toggle(CheatToggles.logShapeshifts, " Enregistrer les métamorphoses");

        CheatToggles.logVents = GUILayout.Toggle(CheatToggles.logVents, " Enregistrer les conduits");

        CheatToggles.logTasks = GUILayout.Toggle(CheatToggles.logTasks, " Enregistrer les tâches");

        CheatToggles.logGameState  = GUILayout.Toggle(CheatToggles.logGameState, " Enregistrer l'état de la partie");
    }
}