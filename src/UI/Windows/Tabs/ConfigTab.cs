using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using MalumMenu.features;

namespace MalumMenu;

public class ConfigTab : ITab
{
    private const int HandlingId = 60005;
    public string name => "Config";

    public readonly Dictionary<string, int> versions = new Dictionary<string, int>()
        {
			// Current version at runtime
			// VersionShower::Start uses ReferenceDataManager.Refdata.userFacingVersion to get version strings such as "17.1" however that doesn't seem to before the game fully loads, so we have to use Constants::AddressablesVersion to get a less human-understandable version string
			{ $"{Constants.AddressablesVersion} (Current)", Constants.GetBroadcastVersion() },
            { "16.1.0", 50632950 },
            { "17.1", 50643450 },
            { "17.1.2", 50647000 },
            { "17.2", 50645050 },
            { "17.2.1", 50652900 },
            { "17.2.2", 50653700 }
        };

    private int versionSelection = 0;

    // Among Us strips UnityEngine.TextEditor from the il2cpp build, so the engine text box
    // throws on set_text. Use the menu own TextField element instead.
    private TextField _friendCodeField;
    private TextField _targetsField;

    public void Draw()
    {
        try
        {
            GUILayout.BeginVertical(GUILayout.Width(MenuUI.windowWidth * 0.425f));

            DrawGeneral();

            GUILayout.Space(10);

            DrawUpdates();

            GUILayout.Space(10);

            DrawGlichRooms();

            GUILayout.Space(10);
            
            Chat.OnChat.LogChatMessages = GUILayout.Toggle(Chat.OnChat.LogChatMessages, "Log chat messages to console");

            if(GUILayout.Button("Clear Notifications"))
            {
                MalumMenu.notifications.ClearNotifications();
                MalumMenu.notifications.Send("Notifications", "All notifications have been cleared.", 5);
            }

            // TEMP TEST — delete me: verifies ErrorReporter end-to-end (file + log + notice).
            if(GUILayout.Button("TEST: Throw test exception"))
            {
                try { throw new Exception("TEST — delete me"); }
                catch (Exception testEx) { ErrorReporter.Report(testEx, HandlingId, "manual test"); }
            }

            GUILayout.Space(10);

            Spoofer.shouldSpoofVersion = GUILayout.Toggle(Spoofer.shouldSpoofVersion, "Enable Version Spoofing");

            GUILayout.Label($"Spoofed Version: {versions.ElementAt(versionSelection).Key} ({Spoofer.spoofedVersion})");
            versionSelection = (int)GUILayout.HorizontalSlider(versionSelection, 0, versions.Count - 1);
            Spoofer.spoofedVersion = versions.ElementAt(versionSelection).Value;

            Spoofer.useModdedProtocol = GUILayout.Toggle(Spoofer.useModdedProtocol, "Use Modded Protocol");

            Spoofer.SpoofLevel.Enabled = GUILayout.Toggle(Spoofer.SpoofLevel.Enabled, "Spoof Level");
            if(Spoofer.SpoofLevel.Enabled)
            {
                GUILayout.Label($"Spoofed Level: {Spoofer.SpoofLevel.newLevel}");
                Spoofer.SpoofLevel.newLevel = (uint)(int)GUILayout.HorizontalSlider((float)Spoofer.SpoofLevel.newLevel, 1, 999);
            }

            GUILayout.Label($"Spoofed Platform: {Spoofer.spoofedPlatform}");
            Spoofer.spoofedPlatform = (Platforms)GUILayout.HorizontalSlider((float)Spoofer.spoofedPlatform, 0, 10);

            GUILayout.Space(8);
            GUILayout.Label("<b>Friend code spoof</b>");
            CheatToggles.fcSpoofEnabled = GUILayout.Toggle(CheatToggles.fcSpoofEnabled, "Enable Friend Code Spoof");
            if (CheatToggles.fcSpoofEnabled)
            {
                if (string.IsNullOrEmpty(Cheats.FriendCodeTools.Value))
                {
                    Cheats.FriendCodeTools.Value = Cheats.FriendCodeTools.Random();
                }

                if (_friendCodeField == null)
                {
                    _friendCodeField = new TextField(Cheats.FriendCodeTools.Value);
                }
                else if (!_friendCodeField.IsFocused)
                {
                    _friendCodeField.Content = Cheats.FriendCodeTools.Value;
                }

                _friendCodeField.Draw(300, 20);
                Cheats.FriendCodeTools.Value = _friendCodeField.Content;

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Randomize"))
                {
                    Cheats.FriendCodeTools.Value = Cheats.FriendCodeTools.Random();
                }

                if (GUILayout.Button("Use Current"))
                {
                    Cheats.FriendCodeTools.Value = Cheats.FriendCodeTools.Fallback(EOSManager.Instance != null ? EOSManager.Instance.FriendCode : string.Empty);
                }

                GUILayout.EndHorizontal();
            }

            GUILayout.EndVertical();
        }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "ConfigTab.Draw: draw config and spoof settings"); }
    }

    private void DrawGlichRooms()
    {
        try
        {
            GUILayout.Label("Glich Rooms", GUIStylePreset.TabSubtitle);

            CheatToggles.glichCycle = GUILayout.Toggle(CheatToggles.glichCycle, " Run cycle");
            CheatToggles.glichHunt = GUILayout.Toggle(CheatToggles.glichHunt, " Hunt new room each cycle");
            CheatToggles.glichLog = GUILayout.Toggle(CheatToggles.glichLog, " Log found rooms");

            GUILayout.Space(3);
            GUILayout.Label("Targets (code endings, comma separated)");
            string cur = Cheats.GlichRooms.TargetList;
            if (_targetsField == null)
            {
                _targetsField = new TextField(cur ?? string.Empty);
            }
            else if (!_targetsField.IsFocused)
            {
                _targetsField.Content = cur ?? string.Empty;
            }

            _targetsField.Draw(300, 20);
            string next = _targetsField.Content;
            if (next != cur)
            {
                Cheats.GlichRooms.Targets.Value = next;
            }

            GUILayout.Label("Delay: " + CheatToggles.glichDelay.ToString("0.0") + "s");
            CheatToggles.glichDelay = GUILayout.HorizontalSlider(CheatToggles.glichDelay, 0.5f, 15f);

            GUILayout.Label("Stage: " + Cheats.GlichRooms.Stage + "   Runs: " + Cheats.GlichRooms.Runs
                + "   Hits: " + Cheats.GlichRooms.Hits + "   Last level: " + Cheats.GlichRooms.Lvl);

            string[] found = Cheats.GlichRooms.Found();
            if (found.Length > 0)
            {
                GUILayout.Label("Found (" + found.Length + "):");
                int shown = 0;
                for (int i = 0; i < found.Length && shown < 5; i++)
                {
                    GUILayout.Label("  " + found[i]);
                    shown++;
                }

                if (GUILayout.Button("Clear found"))
                {
                    Cheats.GlichRooms.ClearFound();
                }
            }
        }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "ConfigTab.DrawGlichRooms"); }
    }

    private void DrawUpdates()
    {
        try
        {
            GUILayout.Label($"HyperMenu v{MalumMenu.hyperVersion} — Updates");

            string status = UpdateCheck.State switch
            {
                UpdateState.Checking => "Checking...",
                UpdateState.Found => $"Update available: v{UpdateCheck.Latest}",
                UpdateState.Loading => "Downloading...",
                UpdateState.Done => "Installed. Restart the game.",
                UpdateState.Fail => $"Check failed: {UpdateCheck.Error}",
                _ => string.IsNullOrEmpty(UpdateCheck.Latest) ? "Up to date." : $"Latest: v{UpdateCheck.Latest}",
            };
            GUILayout.Label(status);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("CHECK"))
                UpdateCheck.Recheck();
            if (UpdateCheck.State == UpdateState.Found || UpdateCheck.State == UpdateState.Fail)
            {
                if (GUILayout.Button("DOWNLOAD"))
                    UpdateCheck.Download();
            }
            if (UpdateCheck.State == UpdateState.Done)
            {
                if (GUILayout.Button("QUIT GAME"))
                    UpdateCheck.Restart();
            }
            GUILayout.EndHorizontal();
        }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "ConfigTab.DrawUpdates: draw update checker"); }
    }

    private void DrawGeneral()
    {
        CheatToggles.openConfig = GUILayout.Toggle(CheatToggles.openConfig, " Open Config");

        CheatToggles.reloadConfig = GUILayout.Toggle(CheatToggles.reloadConfig, " Reload Config");

        CheatToggles.saveProfile = GUILayout.Toggle(CheatToggles.saveProfile, " Save to Profile");

        CheatToggles.loadProfile = GUILayout.Toggle(CheatToggles.loadProfile, " Load from Profile");
    }
}
