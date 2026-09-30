using UnityEngine;
using MalumMenu.features;

namespace MalumMenu;

public class RolesTab : ITab
{
    public string name => "Rôles";

    public void Draw()
    {
        GUILayout.BeginHorizontal();

        GUILayout.BeginVertical(GUILayout.Width(MenuUI.windowWidth * 0.425f));

        DrawGeneral();

        GUILayout.Space(15);

        DrawImpostor();

        GUILayout.Space(15);

        DrawShapeshifter();

        GUILayout.Space(15);

        DrawCrewmate();

        GUILayout.Space(15);

        DrawTracker();

        GUILayout.EndVertical();

        GUILayout.BeginVertical();

        DrawEngineer();

        GUILayout.Space(15);

        DrawScientist();

        GUILayout.Space(15);

        DrawDetective();

        GUILayout.EndVertical();

        GUILayout.EndHorizontal();
    }

    private void DrawGeneral()
    {
        CheatToggles.setFakeRole = GUILayout.Toggle(CheatToggles.setFakeRole, " Définir un faux rôle");

        CheatToggles.setFakeAlive = GUILayout.Toggle(CheatToggles.setFakeAlive, " Définir comme vivant (faux)");
    }

    private void DrawImpostor()
    {
        GUILayout.Label("Imposteur", GUIStylePreset.TabSubtitle);

        CheatToggles.killReach = GUILayout.Toggle(CheatToggles.killReach, " Portée de kill");

        Roles.SkipSabotageChecks.SabotageInVents = GUILayout.Toggle(Roles.SkipSabotageChecks.SabotageInVents, " Autoriser le sabotage dans les conduits en tant qu'Imposteur");

        CheatToggles.impostorTasks = GUILayout.Toggle(CheatToggles.impostorTasks, " Autoriser les tâches");
    }

    private void DrawShapeshifter()
    {
        GUILayout.Label("Métamorphe", GUIStylePreset.TabSubtitle);

        CheatToggles.noShapeshiftAnim = GUILayout.Toggle(CheatToggles.noShapeshiftAnim, " Pas d'animation de métamorphose");

        CheatToggles.endlessSsDuration = GUILayout.Toggle(CheatToggles.endlessSsDuration, " Durée de métamorphose infinie");
    }

    private void DrawCrewmate()
    {
        GUILayout.Label("Équipier", GUIStylePreset.TabSubtitle);

        Roles.SkipSabotageChecks.SabotageAsCrewmate = GUILayout.Toggle(Roles.SkipSabotageChecks.SabotageAsCrewmate, " Sabotage en tant qu'équipier");

        CheatToggles.showTasksMenu = GUILayout.Toggle(CheatToggles.showTasksMenu, " Afficher le menu des tâches");
    }

    private void DrawTracker()
    {
        GUILayout.Label("Traqueur", GUIStylePreset.TabSubtitle);

        CheatToggles.endlessTracking = GUILayout.Toggle(CheatToggles.endlessTracking, " Traque infinie");

        CheatToggles.noTrackingDelay = GUILayout.Toggle(CheatToggles.noTrackingDelay, " Pas de délai de traque");

        CheatToggles.noTrackingCooldown = GUILayout.Toggle(CheatToggles.noTrackingCooldown, " Pas de recharge de traque");

        CheatToggles.trackReach = GUILayout.Toggle(CheatToggles.trackReach, " Portée de traque");
    }

    private void DrawEngineer()
    {
        GUILayout.Label("Ingénieur", GUIStylePreset.TabSubtitle);

        CheatToggles.endlessVentTime = GUILayout.Toggle(CheatToggles.endlessVentTime, " Temps dans les conduits infini");

        CheatToggles.noVentCooldown = GUILayout.Toggle(CheatToggles.noVentCooldown, " Pas de recharge des conduits");
    }

    private void DrawScientist()
    {
        GUILayout.Label("Scientifique", GUIStylePreset.TabSubtitle);

        CheatToggles.endlessBattery = GUILayout.Toggle(CheatToggles.endlessBattery, " Batterie infinie");

        CheatToggles.noVitalsCooldown = GUILayout.Toggle(CheatToggles.noVitalsCooldown, " Pas de recharge des signes vitaux");
    }

    private void DrawDetective()
    {
        GUILayout.Label("Détective", GUIStylePreset.TabSubtitle);

        CheatToggles.interrogateReach = GUILayout.Toggle(CheatToggles.interrogateReach, " Portée d'interrogation");
    }
}