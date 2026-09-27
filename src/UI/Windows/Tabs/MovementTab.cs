using UnityEngine;
using System;
using System.Collections.Generic;

namespace MalumMenu;

public class MovementTab : ITab
{
    // 5-digit handling ID for MovementTab.cs (see HandlingIds.cs).
    private const int HandlingId = 60001;

    public string name => "Movement";

    public void Draw()
    {
        GUILayout.BeginVertical(GUILayout.Width(MenuUI.windowWidth * 0.425f));

        if (PlayerControl.LocalPlayer == null)
        {
            GUILayout.Label("You need to be in game for this to work.");
            GUILayout.EndVertical();
            return;
        }

        Vector2 position = PlayerControl.LocalPlayer.transform.position;

        GUILayout.Label($"Current Map: {Utilities.GetCurrentMap()}\nCurrent Position:\nX: {position.x:F2}\nY: {position.y:F2}");

        GUILayout.Space(15);

        DrawGeneral();

        GUILayout.Space(15);

        DrawMouse();

        GUILayout.Space(15);

        DrawTeleport();

        GUILayout.EndVertical();
    }

    private void DrawGeneral()
    {
        MalumMenu.Log.LogInfo($"Drawing General Movement Tab");
        CheatToggles.noClip = GUILayout.Toggle(CheatToggles.noClip, " NoClip");

        CheatToggles.invertControls = GUILayout.Toggle(CheatToggles.invertControls, " Invert Controls");

        try
        {
            if (PlayerControl.LocalPlayer.Data.IsDead)
            {
                PlayerControl.LocalPlayer.MyPhysics.GhostSpeed = GUILayout.HorizontalSlider(PlayerControl.LocalPlayer.MyPhysics.GhostSpeed, 0f, 20f, GUILayout.Width(250f));
                Utils.SnapSpeedToDefault(0.05f, true);
                GUILayout.Label($"Current Speed: {PlayerControl.LocalPlayer?.MyPhysics.GhostSpeed} {(Utils.IsSpeedDefault(true) ? "(Default)" : "")}");
            }
            else
            {
                PlayerControl.LocalPlayer.MyPhysics.Speed = GUILayout.HorizontalSlider(PlayerControl.LocalPlayer.MyPhysics.Speed, 0f, 20f, GUILayout.Width(250f));
                Utils.SnapSpeedToDefault(0.05f);
                GUILayout.Label($"Current Speed: {PlayerControl.LocalPlayer?.MyPhysics.Speed} {(Utils.IsSpeedDefault() ? "(Default)" : "")}");
            }
        } catch (NullReferenceException ex) { ErrorReporter.Report(ex, HandlingId, "DrawGeneral speed slider"); }
        MalumMenu.Log.LogInfo($"Finished Drawing General Movement Tab");
    }

    private void DrawMouse()
    {
        GUILayout.Label("Mouse", GUIStylePreset.TabSubtitle);

        CheatToggles.mouseSelect = GUILayout.Toggle(CheatToggles.mouseSelect, " Mouse Select (click player, scroll = resize)");
        if (CheatToggles.mouseSelect && MouseTools.Selected != null && MouseTools.Selected.Data != null)
            GUILayout.Label($"Selected: {MouseTools.Selected.Data.PlayerName} (click again to clear)");

        CheatToggles.selfDrag = GUILayout.Toggle(CheatToggles.selfDrag, " Drag Self (hold LMB)");
        if (CheatToggles.selfDrag)
        {
            CheatToggles.selfDragSmooth = GUILayout.Toggle(CheatToggles.selfDragSmooth, "  Smooth Glide");
            GUILayout.Label($"  Glide Speed: {CheatToggles.selfDragSpeed:F1}");
            CheatToggles.selfDragSpeed = GUILayout.HorizontalSlider(CheatToggles.selfDragSpeed, 0.5f, 10f, GUILayout.Width(250f));
        }
        MalumMenu.Log.LogInfo($"Finished Drawing Mouse Controls");

        GUILayout.Space(5);
        GUILayout.Label("Movement FX", GUIStylePreset.TabSubtitle);

        CheatToggles.antWalk = GUILayout.Toggle(CheatToggles.antWalk, " Ant Walk (others see you jitter)");
        if (CheatToggles.antWalk)
        {
            GUILayout.Label($"  Step Time: {CheatToggles.antWalkStep:F2}s");
            CheatToggles.antWalkStep = GUILayout.HorizontalSlider(CheatToggles.antWalkStep, 0.1f, 1.2f, GUILayout.Width(250f));
            GUILayout.Label($"  Twitch Time: {CheatToggles.antWalkTwitch:F2}s");
            CheatToggles.antWalkTwitch = GUILayout.HorizontalSlider(CheatToggles.antWalkTwitch, 0.03f, 0.15f, GUILayout.Width(250f));
        }

        CheatToggles.glideForOthers = GUILayout.Toggle(CheatToggles.glideForOthers, " Glide for Others (no walk anim remotely)");
    }

    private void DrawTeleport()
    {
        MalumMenu.Log.LogInfo($"Drawing Teleport Tab");        GUILayout.Label("Teleport", GUIStylePreset.TabSubtitle);

        CheatToggles.teleportCursor = GUILayout.Toggle(CheatToggles.teleportCursor, " to Cursor");

        CheatToggles.teleportPlayer = GUILayout.Toggle(CheatToggles.teleportPlayer, " to Player");

        Teleporter.UseSnapToRPC = GUILayout.Toggle(Teleporter.UseSnapToRPC, "Use SnapTo RPC For Teleports");
        GUILayout.Label("Teleport To Location:");

        Dictionary<string, Vector2> teleportLocations = Teleporter.GetTeleportLocations();

        byte i = 0;
        foreach (var (key, value) in teleportLocations)
        {
            if (i % 2 == 0)
            {
                GUILayout.BeginHorizontal();
            }

            if (GUILayout.Button(key))
            {
                Teleporter.TeleportTo(value);
            }

            if (i % 2 != 0)
            {
                GUILayout.EndHorizontal();
            }

            i++;
        }

        // If the amount of teleport locations is an odd number then we won't be ending the horizontal layout, so we check if we need to end it here
        if (i % 2 != 0)
        {
            GUILayout.EndHorizontal();
        }
        MalumMenu.Log.LogInfo($"Finished Drawing Teleport Tab");
    }
}
