using System;
using UnityEngine;

namespace MalumMenu;

public class AnimationsTab : ITab
{
    private const int HandlingId = 60003;
    public string name => "Animations";

    public void Draw()
    {
        try
        {
            GUILayout.BeginVertical(GUILayout.Width(MenuUI.windowWidth * 0.425f));

            DrawGeneral();

            GUILayout.Space(15);

            DrawClientSided();

            GUILayout.EndVertical();
        }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "AnimationsTab.Draw: draw animation toggles"); }
    }

    private void DrawGeneral()
    {
        CheatToggles.animShields = GUILayout.Toggle(CheatToggles.animShields, " Shields");

        CheatToggles.animAsteroids = GUILayout.Toggle(CheatToggles.animAsteroids, " Asteroids");

        CheatToggles.animEmptyGarbage = GUILayout.Toggle(CheatToggles.animEmptyGarbage, " Empty Garbage");

        CheatToggles.animMedScan = GUILayout.Toggle(CheatToggles.animMedScan, " Medbay Scan");

        CheatToggles.animCamsInUse = GUILayout.Toggle(CheatToggles.animCamsInUse, " Cams In Use");

        CheatToggles.animPet = GUILayout.Toggle(CheatToggles.animPet, " Pet");
        MalumMenu.routines.petPlayer.Enabled = CheatToggles.animPet;
        if(CheatToggles.animPet && MalumMenu.routines.petPlayer.target == null)
        {
            if(PlayersSection.selectedPlayer != null)
            {
                MalumMenu.routines.petPlayer.target = PlayersSection.selectedPlayer;
            }
            else
            {
                MalumMenu.notifications.Send("Pet Player", "Select a player in the Players tab first.", 10);
                CheatToggles.animPet = false;
            }
        }
    }

    private void DrawClientSided()
    {
        GUILayout.Label("Client-Sided", GUIStylePreset.TabSubtitle);

        CheatToggles.moonWalk = GUILayout.Toggle(CheatToggles.moonWalk, " Moonwalk");

        GUILayout.Space(5);
        GUILayout.Label("Animation Loops (local)", GUIStylePreset.TabSubtitle);

        GUILayout.BeginHorizontal();
        if(GUILayout.Button($"Climb Up{(Cheats.AnimLoops.Active(Cheats.AnimLoop.ClimbUp) ? " (on)" : "")}"))
            Cheats.AnimLoops.Toggle(Cheats.AnimLoop.ClimbUp);
        if(GUILayout.Button($"Climb Down{(Cheats.AnimLoops.Active(Cheats.AnimLoop.ClimbDown) ? " (on)" : "")}"))
            Cheats.AnimLoops.Toggle(Cheats.AnimLoop.ClimbDown);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if(GUILayout.Button($"Enter Vent{(Cheats.AnimLoops.Active(Cheats.AnimLoop.EnterVent) ? " (on)" : "")}"))
            Cheats.AnimLoops.Toggle(Cheats.AnimLoop.EnterVent);
        if(GUILayout.Button($"Exit Vent{(Cheats.AnimLoops.Active(Cheats.AnimLoop.ExitVent) ? " (on)" : "")}"))
            Cheats.AnimLoops.Toggle(Cheats.AnimLoop.ExitVent);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if(GUILayout.Button($"Jump{(Cheats.AnimLoops.Active(Cheats.AnimLoop.Jump) ? " (on)" : "")}"))
            Cheats.AnimLoops.Toggle(Cheats.AnimLoop.Jump);
        if(GUILayout.Button($"Spawn{(Cheats.AnimLoops.Active(Cheats.AnimLoop.Spawn) ? " (on)" : "")}"))
            Cheats.AnimLoops.Toggle(Cheats.AnimLoop.Spawn);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if(GUILayout.Button("Mushroom In"))
            Cheats.AnimLoops.MushroomIn();
        if(GUILayout.Button("Mushroom Out"))
            Cheats.AnimLoops.MushroomOut();
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if(GUILayout.Button("Alert Flash"))
            Cheats.AnimLoops.AlertFlash();
        if(GUILayout.Button("Meeting Sting"))
            Cheats.AnimLoops.MeetingSting();
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if(GUILayout.Button("Eject Sound"))
            Cheats.AnimLoops.EjectSfx();
        if(GUILayout.Button("Stop All Loops"))
            Cheats.AnimLoops.ResetAll();
        GUILayout.EndHorizontal();
    }
}
