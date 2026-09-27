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
            GUILayout.BeginHorizontal();

            GUILayout.BeginVertical(GUILayout.Width(MenuUI.windowWidth * 0.425f));

            DrawGeneral();

            GUILayout.Space(15);

            DrawSabotage();

            GUILayout.EndVertical();

            GUILayout.BeginVertical();

            DrawVents();

            GUILayout.Space(15);

            DrawRides();

            GUILayout.Space(15);

            DrawAssist();

            GUILayout.Space(15);

            DrawPet();

            GUILayout.EndVertical();

            GUILayout.EndHorizontal();
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

    private void DrawVents()
    {
        GUILayout.Label("Vents", GUIStylePreset.TabSubtitle);

        CheatToggles.unlockVents = GUILayout.Toggle(CheatToggles.unlockVents, " Unlock Vents");

        CheatToggles.kickVents = GUILayout.Toggle(CheatToggles.kickVents, " Kick All From Vents");

        CheatToggles.walkInVents = GUILayout.Toggle(CheatToggles.walkInVents, " Walk In Vents");

        CheatToggles.ventNetwork = GUILayout.Toggle(CheatToggles.ventNetwork, " Vent Network (travel map via vents)");

        GUILayout.Space(5);
        GUILayout.Label("Vent-TP:");
        if (GUILayout.Button($"Who may vent: {VentTpTools.ModeName(CheatToggles.ventTpMode)}"))
        {
            CheatToggles.ventTpMode = (CheatToggles.ventTpMode + 1) % 4;
        }
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("VENT -"))
        {
            MalumMenu.notifications.Send("Vent-TP", VentTpTools.CycleVent(-1));
        }
        if (GUILayout.Button($"VENT SELECTED ({VentTpTools.MarkedCount})"))
        {
            MalumMenu.notifications.Send("Vent-TP", VentTpTools.SendMarked());
        }
        if (GUILayout.Button("VENT +"))
        {
            MalumMenu.notifications.Send("Vent-TP", VentTpTools.CycleVent(1));
        }
        GUILayout.EndHorizontal();
        CheatToggles.ventTpAuto = GUILayout.Toggle(CheatToggles.ventTpAuto, " Auto-scatter marked");
        GUILayout.Label($"Scatter delay: {CheatToggles.ventTpAutoDelay:F1}s");
        CheatToggles.ventTpAutoDelay = GUILayout.HorizontalSlider(CheatToggles.ventTpAutoDelay, 0.3f, 10f);
        CheatToggles.impTrap = GUILayout.Toggle(CheatToggles.impTrap, " Rally to impostor (ImpTrap)");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("SELECT ALL"))
        {
            VentTpTools.MarkAll();
        }
        if (GUILayout.Button("CLEAR ALL"))
        {
            VentTpTools.ClearMarks();
        }
        GUILayout.EndHorizontal();
    }

    private void DrawRides()
    {
        GUILayout.Label("Zipline (Fungle)", GUIStylePreset.TabSubtitle);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button($"DOWN ({Cheats.RideTargets.Count})"))
        {
            MalumMenu.notifications.Send("Zipline", Cheats.ZiplineRide.RideSelected(true));
        }
        if (GUILayout.Button("UP"))
        {
            MalumMenu.notifications.Send("Zipline", Cheats.ZiplineRide.RideSelected(false));
        }
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("SELECT ALL"))
        {
            Cheats.RideTargets.All();
        }
        if (GUILayout.Button("CLEAR ALL"))
        {
            Cheats.RideTargets.Clear();
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(5);
        GUILayout.Label("Platform (Airship)", GUIStylePreset.TabSubtitle);

        CheatToggles.platformUnlock = GUILayout.Toggle(CheatToggles.platformUnlock, " Unlock (H&S)");
        string state = Cheats.PlatformRide.IsLeft ? "left" : "right";
        if (Cheats.PlatformRide.Locked)
            state += " (locked)";
        GUILayout.Label($"Platform: {state}");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("LEFT"))
        {
            MalumMenu.notifications.Send("Platform", Cheats.PlatformRide.Move(true));
        }
        if (GUILayout.Button("RIGHT"))
        {
            MalumMenu.notifications.Send("Platform", Cheats.PlatformRide.Move(false));
        }
        GUILayout.EndHorizontal();
    }

    private void DrawAssist()
    {
        GUILayout.Label("Crew Assist", GUIStylePreset.TabSubtitle);

        CheatToggles.skipDecon = GUILayout.Toggle(CheatToggles.skipDecon, " Skip Decontamination (Polus/Mira)");

        CheatToggles.consoleReach = GUILayout.Toggle(CheatToggles.consoleReach, " Console Reach");
        if (CheatToggles.consoleReach)
        {
            GUILayout.Label($"  Distance: {CheatToggles.consoleDist:F1}");
            CheatToggles.consoleDist = GUILayout.HorizontalSlider(CheatToggles.consoleDist, 1f, 15f, GUILayout.Width(250f));
        }

        CheatToggles.airshipSpawn = GUILayout.Toggle(CheatToggles.airshipSpawn, " Pick Airship Spawn");
        if (CheatToggles.airshipSpawn)
        {
            GUILayout.Label($"  Spawn Point: {CheatToggles.airshipSpawnId}");
            CheatToggles.airshipSpawnId = (int)GUILayout.HorizontalSlider(CheatToggles.airshipSpawnId, 0, 6, GUILayout.Width(250f));
        }
    }

    private void DrawPet()
    {
        GUILayout.Label("Pet Hand", GUIStylePreset.TabSubtitle);

        if (!Cheats.PetHand.HasPet())
            GUILayout.Label("(requires an equipped pet)");
        if (Cheats.PetHand.On)
            GUILayout.Label($"Active: {Cheats.PetHand.ModeName()}");

        GUILayout.BeginHorizontal();
        if (GUILayout.Button(Cheats.PetHand.Manual ? "STOP Joystick" : "MANUAL JOYSTICK"))
        {
            MalumMenu.notifications.Send("Pet Hand", Cheats.PetHand.ToggleManual());
        }
        if (GUILayout.Button("STOP"))
        {
            MalumMenu.notifications.Send("Pet Hand", Cheats.PetHand.Stop2());
        }
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        if (GUILayout.Button($"PAINT ({Cheats.PetHand.PaintCount})"))
        {
            MalumMenu.notifications.Send("Pet Hand", Cheats.PetHand.TogglePaint());
        }
        if (GUILayout.Button("CLEAR"))
        {
            MalumMenu.notifications.Send("Pet Hand", Cheats.PetHand.ClearPaint());
        }
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        if (GUILayout.Button($"ROOM: {Cheats.PetHand.RoomName()}"))
        {
            Cheats.PetHand.RoomStep(1);
        }
        if (GUILayout.Button("FILL ROOM"))
        {
            MalumMenu.notifications.Send("Pet Hand", Cheats.PetHand.FillRoom());
        }
        GUILayout.EndHorizontal();
    }
}
