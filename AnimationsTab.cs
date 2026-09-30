using UnityEngine;

namespace MalumMenu;

public class AnimationsTab : ITab
{
    public string name => "Animations";

    public void Draw()
    {
        GUILayout.BeginVertical(GUILayout.Width(MenuUI.windowWidth * 0.425f));

        DrawGeneral();

        GUILayout.Space(15);

        DrawClientSided();

        GUILayout.EndVertical();
    }

    private void DrawGeneral()
    {
        CheatToggles.animShields = GUILayout.Toggle(CheatToggles.animShields, " Boucliers");

        CheatToggles.animAsteroids = GUILayout.Toggle(CheatToggles.animAsteroids, " Astéroïdes");

        CheatToggles.animEmptyGarbage = GUILayout.Toggle(CheatToggles.animEmptyGarbage, " Vider les poubelles");

        CheatToggles.animMedScan = GUILayout.Toggle(CheatToggles.animMedScan, " Scan MedBay");

        CheatToggles.animCamsInUse = GUILayout.Toggle(CheatToggles.animCamsInUse, " Caméras utilisées");

        // CheatToggles.animPet = GUILayout.Toggle(CheatToggles.animPet, " Familier");
    }

    private void DrawClientSided()
    {
        GUILayout.Label("Côté client", GUIStylePreset.TabSubtitle);

        CheatToggles.moonWalk = GUILayout.Toggle(CheatToggles.moonWalk, " Moonwalk");
    }
}