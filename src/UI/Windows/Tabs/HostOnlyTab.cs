using System;
using UnityEngine;

namespace MalumMenu;

public class HostOnlyTab : ITab
{
    private const int HandlingId = 60008;
    public string name => "Host-Only";

    public void Draw()
    {
        try
        {
            GUILayout.BeginHorizontal();

            GUILayout.BeginVertical(GUILayout.Width(MenuUI.windowWidth * 0.425f));

            if (PlayerControl.LocalPlayer == null)
            {
                GUILayout.Label("You are not currently in a game, these options will not work.");
            }
            else if (!AmongUsClient.Instance.AmHost)
            {
                GUILayout.Label("You are not the host of the current lobby. Using these options will either do nothing or get you banned by the anticheat");
            }

            DrawGeneral();

            GUILayout.Space(15);

            DrawMurder();

            GUILayout.Space(15);

            DrawGameState();

            GUILayout.EndVertical();

            GUILayout.BeginVertical();

            DrawMeetings();

            GUILayout.Space(15);

            DrawHideAndSeek();

            GUILayout.Space(15);

            DrawColors();

            GUILayout.EndVertical();

            GUILayout.EndHorizontal();
        }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "HostOnlyTab.Draw: draw host-only controls"); }
    }

    private void DrawGeneral()
    {
        CheatToggles.bypassHostOnly = GUILayout.Toggle(CheatToggles.bypassHostOnly, " Bypass Host Only");

        GUILayout.Space(5);

        CheatToggles.killVanished = GUILayout.Toggle(CheatToggles.killVanished, " Kill While Vanished");

        CheatToggles.killAnyone = GUILayout.Toggle(CheatToggles.killAnyone, " Kill Anyone");

        CheatToggles.noKillCd = GUILayout.Toggle(CheatToggles.noKillCd, " No Kill Cooldown");

        CheatToggles.showProtectMenu = GUILayout.Toggle(CheatToggles.showProtectMenu, " Show Protect Menu");

        // CheatToggles.forceRole = GUILayout.Toggle(CheatToggles.forceRole, " Force Role");

        // CheatToggles.noOptionsLimits = GUILayout.Toggle(CheatToggles.noOptionsLimits, " No Options Limits");
    }

    private void DrawMurder()
    {
        GUILayout.Label("Murder", GUIStylePreset.TabSubtitle);

        CheatToggles.killPlayer = GUILayout.Toggle(CheatToggles.killPlayer, " Kill Player");

        CheatToggles.telekillPlayer = GUILayout.Toggle(CheatToggles.telekillPlayer, " Telekill Player");

        CheatToggles.killAllCrew = GUILayout.Toggle(CheatToggles.killAllCrew, " Kill All Crewmates");

        CheatToggles.killAllImps = GUILayout.Toggle(CheatToggles.killAllImps, " Kill All Impostors");

        CheatToggles.killAll = GUILayout.Toggle(CheatToggles.killAll, " Kill Everyone");
    }

    private void DrawGameState()
    {
        GUILayout.Label("Game State", GUIStylePreset.TabSubtitle);

        CheatToggles.forceStartGame = GUILayout.Toggle(CheatToggles.forceStartGame, " Force Start Game");

        CheatToggles.noGameEnd = GUILayout.Toggle(CheatToggles.noGameEnd, " No Game End");

        CheatToggles.unlockStartButton = GUILayout.Toggle(CheatToggles.unlockStartButton, " Unlock Start Button");

        CheatToggles.startOnEnter = GUILayout.Toggle(CheatToggles.startOnEnter, " Start On Enter");

        if (CheatToggles.startOnEnter)
            CheatToggles.instantStart = GUILayout.Toggle(CheatToggles.instantStart, " Instant Start");
    }

    private void DrawHideAndSeek()
    {
        GUILayout.Label("Hide & Seek", GUIStylePreset.TabSubtitle);

        CheatToggles.customSeekers = GUILayout.Toggle(CheatToggles.customSeekers, " Custom Seekers");
        if (CheatToggles.customSeekers)
        {
            GUILayout.Label($"Seekers: {Mathf.Clamp(CheatToggles.seekerCount, 1, 15)}");
            CheatToggles.seekerCount = Mathf.Clamp((int)GUILayout.HorizontalSlider(CheatToggles.seekerCount, 1, 15), 1, 15);
        }

        CheatToggles.noSeekerHeadStart = GUILayout.Toggle(CheatToggles.noSeekerHeadStart, " Seeker: Skip Head Start");

        CheatToggles.fourImpostors = GUILayout.Toggle(CheatToggles.fourImpostors, " 4 Impostors (9+ players)");
    }

    private void DrawMeetings()
    {
        GUILayout.Label("Meetings", GUIStylePreset.TabSubtitle);

        CheatToggles.skipMeeting = GUILayout.Toggle(CheatToggles.skipMeeting, " Skip Meeting");

        CheatToggles.voteImmune = GUILayout.Toggle(CheatToggles.voteImmune, " Vote Immune");

        CheatToggles.ejectPlayer = GUILayout.Toggle(CheatToggles.ejectPlayer, " Eject Player");
    }

    private void DrawColors()
    {
        GUILayout.Label("Colors", GUIStylePreset.TabSubtitle);

        CheatToggles.colorAll = GUILayout.Toggle(CheatToggles.colorAll, " Force One Color on All");
        if (CheatToggles.colorAll)
        {
            GUILayout.Label($"Forced color: {CheatToggles.colorAllId}");
            CheatToggles.colorAllId = Mathf.Clamp((int)GUILayout.HorizontalSlider(CheatToggles.colorAllId, 0, Cheats.ColorTools.MaxColor()), 0, Cheats.ColorTools.MaxColor());
        }

        CheatToggles.colorReservations = GUILayout.Toggle(CheatToggles.colorReservations, " Reserve Colors (FriendCode file)");

        var reservations = Cheats.ColorTools.Reservations();
        if (reservations.Count > 0)
        {
            GUILayout.Label($"Reservations ({reservations.Count}):");
            // Copy to array so removal during iteration cannot break the layout loop.
            var list = new System.Collections.Generic.List<Cheats.ColorTools.Reservation>(reservations);
            string removeFc = null;
            foreach (var entry in list)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{entry.Name}: {entry.ColorId}", GUILayout.Width(MenuUI.windowWidth * 0.22f));
                if (GUILayout.Button("X"))
                {
                    removeFc = entry.Fc;
                }
                GUILayout.EndHorizontal();
            }
            if (removeFc != null)
            {
                MalumMenu.notifications.Send("Colors", Cheats.ColorTools.Unreserve(removeFc));
            }
        }
    }
}
