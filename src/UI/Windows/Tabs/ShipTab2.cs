using System;
using UnityEngine;

namespace MalumMenu;

public class ShipTab2 : ITab
{
    private const int HandlingId = 60021;
    public string name => "Ship 2";

    public void Draw()
    {
        try
        {
            GUILayout.BeginVertical(GUILayout.Width(MenuUI.windowWidth * 0.425f));

            DrawVents();

            GUILayout.Space(15);

            DrawRides();

            GUILayout.Space(15);

            DrawAssist();

            GUILayout.Space(15);

            DrawPet();

            GUILayout.EndVertical();
        }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "ShipTab2.Draw: draw ship controls continued"); }
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
