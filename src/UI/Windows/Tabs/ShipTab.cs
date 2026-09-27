using System;
using UnityEngine;

namespace MalumMenu;

public class ShipTab : ITab
{
    private const int HandlingId = 60019;
    public string name => "Ship";

    public void Draw()
    {
        try
        {
            GUILayout.BeginVertical(GUILayout.Width(MenuUI.windowWidth * 0.425f));

            DrawGeneral();

            GUILayout.Space(15);

            DrawSabotage();

            GUILayout.EndVertical();
        }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "ShipTab.Draw: draw ship controls"); }
    }

    private void DrawGeneral()
    {
        // Will implement this later, currently gets user kicked by AC. -ADHyperActive
        // CheatToggles.completeAllTasks = GUILayout.Toggle(CheatToggles.completeAllTasks, " Allow All Tasks");
        
        CheatToggles.fakeTasks = GUILayout.Toggle(CheatToggles.fakeTasks, " Fake Tasks");

        CheatToggles.doAnyTask = GUILayout.Toggle(CheatToggles.doAnyTask, " Do Any Task");

        CheatToggles.unfixableLights = GUILayout.Toggle(CheatToggles.unfixableLights, " Unfixable Lights");

        CheatToggles.callMeeting = GUILayout.Toggle(CheatToggles.callMeeting, " Call Meeting");

        CheatToggles.reportBody = GUILayout.Toggle(CheatToggles.reportBody, " Report Body");

        CheatToggles.closeMeeting = GUILayout.Toggle(CheatToggles.closeMeeting, " Close Meeting");

        if (GUILayout.Button("Close Voting (Tally)"))
        {
            MalumMenu.notifications.Send("Meetings", MeetingTools.CloseVoting());
        }

        if (GUILayout.Button("Close Meeting (No Eject)"))
        {
            MalumMenu.notifications.Send("Meetings", MeetingTools.CloseMeetingNoEject());
        }

        if (GUILayout.Button("Exit & Roam"))
        {
            MalumMenu.notifications.Send("Meetings", MeetingTools.Roam());
        }

        CheatToggles.spamMeetings = GUILayout.Toggle(CheatToggles.spamMeetings, " Spam Meetings");

        CheatToggles.autoReportBodies = GUILayout.Toggle(CheatToggles.autoReportBodies, " Auto-Report Dead Bodies");

        CheatToggles.autoOpenDoorsOnUse = GUILayout.Toggle(CheatToggles.autoOpenDoorsOnUse, " Auto-Open Doors On Use");

        CheatToggles.kickOffensiveNames = GUILayout.Toggle(CheatToggles.kickOffensiveNames, " Kick Offensive Names");
    }

    private void DrawSabotage()
    {
        GUILayout.Label("Sabotage", GUIStylePreset.TabSubtitle);

        CheatToggles.reactorSab = GUILayout.Toggle(CheatToggles.reactorSab, " Reactor");

        CheatToggles.oxygenSab = GUILayout.Toggle(CheatToggles.oxygenSab, " Oxygen");

        CheatToggles.elecSab = GUILayout.Toggle(CheatToggles.elecSab, " Lights");

        CheatToggles.commsSab = GUILayout.Toggle(CheatToggles.commsSab, " Comms");

        CheatToggles.showDoorsMenu = GUILayout.Toggle(CheatToggles.showDoorsMenu, " Show Doors Menu");

        CheatToggles.mushSab = GUILayout.Toggle(CheatToggles.mushSab, " Mushroom Mixup");

        CheatToggles.mushSpore = GUILayout.Toggle(CheatToggles.mushSpore, " Trigger Spores");

        CheatToggles.spamMainSab = GUILayout.Toggle(CheatToggles.spamMainSab, " Spam Main Sabotage");

        CheatToggles.keepLightsOff = GUILayout.Toggle(CheatToggles.keepLightsOff, " Keep Lights Off");

        CheatToggles.infMushroom = GUILayout.Toggle(CheatToggles.infMushroom, " Infinite Mushroom (Fungle)");

        CheatToggles.multiSabotage = GUILayout.Toggle(CheatToggles.multiSabotage, " Multi Sabotage (non-host imp)");

        CheatToggles.autoFixSabotage = GUILayout.Toggle(CheatToggles.autoFixSabotage, " Auto-Fix Sabotage (crew)");

        GUILayout.Space(5);
        GUILayout.Label("Hide & Seek", GUIStylePreset.TabSubtitle);
        if (Cheats.TaskDrain.Running)
            GUILayout.Label("Draining - the crew timer is running out.");
        CheatToggles.taskDrain = GUILayout.Toggle(CheatToggles.taskDrain, " Drain the Timer");
        GUILayout.Label($"Send Step: {CheatToggles.taskDrainStep:F2}s");
        CheatToggles.taskDrainStep = GUILayout.HorizontalSlider(CheatToggles.taskDrainStep, 0.15f, 1.5f, GUILayout.Width(250f));

        GUILayout.Space(5);
        GUILayout.Label("Frame Sabotage (flags innocent in foreign guard)", GUIStylePreset.TabSubtitle);
        if (GUILayout.Button($"System: {Cheats.FrameSabotage.SystemName(CheatToggles.frameSystemIdx)}"))
        {
            CheatToggles.frameSystemIdx = (CheatToggles.frameSystemIdx + 1) % Cheats.FrameSabotage.Systems.Length;
        }
        GUILayout.Label($"Value: {CheatToggles.frameValue}");
        CheatToggles.frameValue = (int)GUILayout.HorizontalSlider(CheatToggles.frameValue, 0, 200, GUILayout.Width(250f));

        CheatToggles.sabotageMap = GUILayout.Toggle(CheatToggles.sabotageMap, " Open Sabotage Map");
    }
}
