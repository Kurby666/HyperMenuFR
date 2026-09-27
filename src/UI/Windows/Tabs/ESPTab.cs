using System;
using UnityEngine;
using MalumMenu.features;

namespace MalumMenu;

public class ESPTab : ITab
{
    private const int HandlingId = 60007;
    public string name => "ESP";

    public void Draw()
    {
        try
        {
            GUILayout.BeginHorizontal();

            GUILayout.BeginVertical(GUILayout.Width(MenuUI.windowWidth * 0.425f));

            DrawGeneral();

            GUILayout.Space(15);

            DrawCamera();

            GUILayout.EndVertical();

            GUILayout.BeginVertical();

            DrawTracers();

            GUILayout.Space(15);

            DrawRadar();

            GUILayout.Space(15);

            DrawMinimap();

            GUILayout.Space(15);

            DrawNeon();

            GUILayout.EndVertical();

            GUILayout.EndHorizontal();
        }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "ESPTab.Draw: draw ESP settings"); }
    }

    private void DrawGeneral()
    {
        CheatToggles.seePlayerInfo = GUILayout.Toggle(CheatToggles.seePlayerInfo, " See Player Info");

        CheatToggles.seeRoles = GUILayout.Toggle(CheatToggles.seeRoles, " See Roles");

        CheatToggles.seeGhosts = GUILayout.Toggle(CheatToggles.seeGhosts, " See Ghosts");

        CheatToggles.seeInVents = GUILayout.Toggle(CheatToggles.seeInVents, " See Players In Vents");

        CheatToggles.seeVanished = GUILayout.Toggle(CheatToggles.seeVanished, " See Vanished Phantoms");

        CheatToggles.noShadows = GUILayout.Toggle(CheatToggles.noShadows, " No Shadows");

        CheatToggles.taskArrows = GUILayout.Toggle(CheatToggles.taskArrows, " Task Arrows");

        CheatToggles.revealVotes = GUILayout.Toggle(CheatToggles.revealVotes, " Reveal Votes");

        CheatToggles.seeLobbyInfo = GUILayout.Toggle(CheatToggles.seeLobbyInfo, " See Lobby Info");

        Visuals.SkipShhhAnimation.Enabled = GUILayout.Toggle(Visuals.SkipShhhAnimation.Enabled, "Skip Shhh Animation");

        Visuals.AccurateDisconnectReasons.Enabled = GUILayout.Toggle(Visuals.AccurateDisconnectReasons.Enabled, "Use more accurate disconnection reasons");

        Visuals.ShowProtections.Enabled = GUILayout.Toggle(Visuals.ShowProtections.Enabled, "Show Guardian Angel Protections");

        Visuals.Fullbright.Enabled = GUILayout.Toggle(Visuals.Fullbright.Enabled, "Fullbright");

        Visuals.ShowGhosts.Enabled = GUILayout.Toggle(Visuals.ShowGhosts.Enabled, "Show Ghosts (Anti-Cheat)");

        Visuals.NoSeekerAnimationPatch.Enabled = GUILayout.Toggle(Visuals.NoSeekerAnimationPatch.Enabled, "No Seeker Animation");
    }

    private void DrawCamera()
    {
        GUILayout.Label("Camera", GUIStylePreset.TabSubtitle);

        CheatToggles.zoomOut = GUILayout.Toggle(CheatToggles.zoomOut, " Zoom Out");

        CheatToggles.spectate = GUILayout.Toggle(CheatToggles.spectate, " Spectate");

        CheatToggles.freecam = GUILayout.Toggle(CheatToggles.freecam, " Freecam");

        CheatToggles.worldTilt = GUILayout.Toggle(CheatToggles.worldTilt, " World Tilt (you stay upright)");
        if (CheatToggles.worldTilt)
        {
            GUILayout.Label($"  Tilt Angle: {CheatToggles.worldTiltAngle:F0}°");
            CheatToggles.worldTiltAngle = GUILayout.HorizontalSlider(CheatToggles.worldTiltAngle, -180f, 180f);
        }
    }

    private void DrawTracers()
    {
        GUILayout.Label("Tracers", GUIStylePreset.TabSubtitle);

        CheatToggles.tracersCrew = GUILayout.Toggle(CheatToggles.tracersCrew, " Crewmates");

        CheatToggles.tracersImps = GUILayout.Toggle(CheatToggles.tracersImps, " Impostors");

        CheatToggles.tracersGhosts = GUILayout.Toggle(CheatToggles.tracersGhosts, " Ghosts");

        CheatToggles.tracersBodies = GUILayout.Toggle(CheatToggles.tracersBodies, " Dead Bodies");

        CheatToggles.colorBasedTracers = GUILayout.Toggle(CheatToggles.colorBasedTracers, " Color-based");

        CheatToggles.distanceBasedTracers = GUILayout.Toggle(CheatToggles.distanceBasedTracers, " Distance-based");
    }

    private void DrawRadar()
    {
        GUILayout.Label("Radar", GUIStylePreset.TabSubtitle);

        CheatToggles.showRadar = GUILayout.Toggle(CheatToggles.showRadar, " Show Radar");

        if (CheatToggles.showRadar)
        {
            if (GUILayout.Button($"Range: {Cheats.RadarPanel.RangeName()}"))
                Cheats.RadarPanel.CycleRange();

            CheatToggles.radarCrew = GUILayout.Toggle(CheatToggles.radarCrew, " Crewmates");
            CheatToggles.radarImps = GUILayout.Toggle(CheatToggles.radarImps, " Impostors");
            CheatToggles.radarGhosts = GUILayout.Toggle(CheatToggles.radarGhosts, " Ghosts");
            CheatToggles.radarBodies = GUILayout.Toggle(CheatToggles.radarBodies, " Dead Bodies (yellow)");

            GUILayout.Label($" Size: {CheatToggles.radarSize:0}");
            CheatToggles.radarSize = GUILayout.HorizontalSlider(CheatToggles.radarSize, 120f, 400f);
            GUILayout.Label($" Opacity: {CheatToggles.radarOpacity:F2}");
            CheatToggles.radarOpacity = GUILayout.HorizontalSlider(CheatToggles.radarOpacity, 0.1f, 1f);
            GUILayout.Label($" X: {CheatToggles.radarX:0}");
            CheatToggles.radarX = GUILayout.HorizontalSlider(CheatToggles.radarX, 0f, 1800f);
            GUILayout.Label($" Y: {CheatToggles.radarY:0}");
            CheatToggles.radarY = GUILayout.HorizontalSlider(CheatToggles.radarY, 0f, 1000f);
        }
    }

    private void DrawMinimap()
    {
        GUILayout.Label("Minimap", GUIStylePreset.TabSubtitle);

        CheatToggles.mapCrew = GUILayout.Toggle(CheatToggles.mapCrew, " Crewmates");

        CheatToggles.mapImps = GUILayout.Toggle(CheatToggles.mapImps, " Impostors");

        CheatToggles.mapGhosts = GUILayout.Toggle(CheatToggles.mapGhosts, " Ghosts");

        CheatToggles.colorBasedMap = GUILayout.Toggle(CheatToggles.colorBasedMap, " Color-based");
    }

    private void DrawNeon()
    {
        GUILayout.Label("Neon Outline", GUIStylePreset.TabSubtitle);

        CheatToggles.neonOutline = GUILayout.Toggle(CheatToggles.neonOutline, " Neon Outline");

        if (CheatToggles.neonOutline)
        {
            if (GUILayout.Button($"Mode: {NeonOutline.ModeName()}"))
                NeonOutline.CycleMode();
        }
    }
}
