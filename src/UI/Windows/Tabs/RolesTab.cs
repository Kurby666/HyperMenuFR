using System;
using UnityEngine;
using MalumMenu.features;

namespace MalumMenu;

public class RolesTab : ITab
{
    private const int HandlingId = 60015;
    public string name => "Roles";

    public void Draw()
    {
        try
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

            GUILayout.Space(15);

            DrawPhantom();

            GUILayout.Space(15);

            DrawJudge();

            GUILayout.EndVertical();

            GUILayout.EndHorizontal();
        }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "RolesTab.Draw: draw role settings"); }
    }

    private void DrawGeneral()
    {
        CheatToggles.setFakeRole = GUILayout.Toggle(CheatToggles.setFakeRole, " Set Fake Role");

        CheatToggles.setFakeAlive = GUILayout.Toggle(CheatToggles.setFakeAlive, " Set Fake Alive");
    }

    private void DrawImpostor()
    {
        GUILayout.Label("Impostor", GUIStylePreset.TabSubtitle);

        CheatToggles.killReach = GUILayout.Toggle(CheatToggles.killReach, " Kill Reach");

        CheatToggles.killAura = GUILayout.Toggle(CheatToggles.killAura, " Kill Aura (auto-kill nearby)");
        if (CheatToggles.killAura)
        {
            GUILayout.Label($"  Aura Radius: {CheatToggles.killAuraDist:F1}");
            CheatToggles.killAuraDist = GUILayout.HorizontalSlider(CheatToggles.killAuraDist, 0.5f, 10f);
        }

        CheatToggles.autoVentKill = GUILayout.Toggle(CheatToggles.autoVentKill, " Auto-Vent After Kill");

        CheatToggles.bodyToVent = GUILayout.Toggle(CheatToggles.bodyToVent, " Body To Vent (host, hides bodies)");

        Roles.SkipSabotageChecks.SabotageInVents = GUILayout.Toggle(Roles.SkipSabotageChecks.SabotageInVents, " Allow Sabotaging In Vents As Imposter");

        CheatToggles.impostorTasks = GUILayout.Toggle(CheatToggles.impostorTasks, " Allow Tasks");
    }

    private void DrawShapeshifter()
    {
        GUILayout.Label("Shapeshifter", GUIStylePreset.TabSubtitle);

        CheatToggles.noShapeshiftAnim = GUILayout.Toggle(CheatToggles.noShapeshiftAnim, " No Ss Animation");

        CheatToggles.endlessSsDuration = GUILayout.Toggle(CheatToggles.endlessSsDuration, " Endless Ss Duration");

        CheatToggles.morphDead = GUILayout.Toggle(CheatToggles.morphDead, " Morph Into Dead");
    }

    private void DrawCrewmate()
    {
        GUILayout.Label("Crewmate", GUIStylePreset.TabSubtitle);

        Roles.SkipSabotageChecks.SabotageAsCrewmate = GUILayout.Toggle(Roles.SkipSabotageChecks.SabotageAsCrewmate, " Sabotage As Crewmate");

        CheatToggles.showTasksMenu = GUILayout.Toggle(CheatToggles.showTasksMenu, " Show Tasks Menu");

        CheatToggles.commsBypass = GUILayout.Toggle(CheatToggles.commsBypass, " Bypass Comms Sabotage");
    }

    private void DrawTracker()
    {
        GUILayout.Label("Tracker", GUIStylePreset.TabSubtitle);

        CheatToggles.endlessTracking = GUILayout.Toggle(CheatToggles.endlessTracking, " Endless Tracking");

        CheatToggles.noTrackingDelay = GUILayout.Toggle(CheatToggles.noTrackingDelay, " No Track Delay");

        CheatToggles.noTrackingCooldown = GUILayout.Toggle(CheatToggles.noTrackingCooldown, " No Track Cooldown");

        CheatToggles.trackReach = GUILayout.Toggle(CheatToggles.trackReach, " Track Reach");
    }

    private void DrawEngineer()
    {
        GUILayout.Label("Engineer", GUIStylePreset.TabSubtitle);

        CheatToggles.endlessVentTime = GUILayout.Toggle(CheatToggles.endlessVentTime, " Endless Vent Time");

        CheatToggles.noVentCooldown = GUILayout.Toggle(CheatToggles.noVentCooldown, " No Vent Cooldown");
    }

    private void DrawScientist()
    {
        GUILayout.Label("Scientist", GUIStylePreset.TabSubtitle);

        CheatToggles.endlessBattery = GUILayout.Toggle(CheatToggles.endlessBattery, " Endless Battery");

        CheatToggles.noVitalsCooldown = GUILayout.Toggle(CheatToggles.noVitalsCooldown, " No Vitals Cooldown");
    }

    private void DrawDetective()
    {
        GUILayout.Label("Detective", GUIStylePreset.TabSubtitle);

        CheatToggles.interrogateReach = GUILayout.Toggle(CheatToggles.interrogateReach, " Interrogate Reach");

        CheatToggles.detNoCd = GUILayout.Toggle(CheatToggles.detNoCd, " No Interrogate Cooldown");
    }

    private void DrawPhantom()
    {
        GUILayout.Label("Phantom", GUIStylePreset.TabSubtitle);

        CheatToggles.endlessInvis = GUILayout.Toggle(CheatToggles.endlessInvis, " Endless Invisibility");

        CheatToggles.smokeSpam = GUILayout.Toggle(CheatToggles.smokeSpam, " Smoke Bomb (vanish spam)");
        GUILayout.Label("  Phantom only, in match. Smoke cloud for everyone.");
    }

    private void DrawJudge()
    {
        GUILayout.Label("Judge", GUIStylePreset.TabSubtitle);

        CheatToggles.judgeNoTasks = GUILayout.Toggle(CheatToggles.judgeNoTasks, " Overrule Without Tasks");
    }
}
