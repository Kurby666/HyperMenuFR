using UnityEngine;

namespace MalumMenu;

public class PassiveTab : ITab
{
    public string name => "Passif";

    public void Draw()
    {
        GUILayout.BeginVertical(GUILayout.Width(MenuUI.windowWidth * 0.425f));

        DrawGeneral();

        GUILayout.EndVertical();
    }

    private void DrawGeneral()
    {
        CheatToggles.antiOverload = GUILayout.Toggle(CheatToggles.antiOverload, " Anti-Surcharge");

        CheatToggles.freeCosmetics = GUILayout.Toggle(CheatToggles.freeCosmetics, " Cosmétiques gratuits");

        CheatToggles.avoidPenalties = GUILayout.Toggle(CheatToggles.avoidPenalties, " Éviter les pénalités");

        CheatToggles.unlockFeatures = GUILayout.Toggle(CheatToggles.unlockFeatures, " Débloquer les fonctionnalités supplémentaires");

        CheatToggles.copyLobbyCodeOnDisconnect = GUILayout.Toggle(CheatToggles.copyLobbyCodeOnDisconnect, " Copier le code du salon en cas de déconnexion");

        CheatToggles.spoofAprilFoolsDate = GUILayout.Toggle(CheatToggles.spoofAprilFoolsDate, " Falsifier la date au 1er avril");

        CheatToggles.randomizeCosmetics = GUILayout.Toggle(CheatToggles.randomizeCosmetics, " Randomiser en rejoignant le salon");

        if (GUILayout.Button(" Randomiser maintenant", GUILayout.Width(200)))
        {
            MalumRandomizer.Randomize();
        }
    }
}