using System;
using System.Collections.Generic;
using UnityEngine;
using System.Globalization;

namespace MalumMenu;

public class SettingsTab : ITab
{
    private const int HandlingId = 60018;
    public string name => "Settings";

    private bool _initialized = false;

    // Custom text fields
    private TextField _menuKeybindField;
    private TextField _menuColorField;
    private TextField _spoofLevelField;
    private TextField _spoofPlatformField;

    // Pending scale values (applied on Apply button)
    private float _pendingScale;
    private float _pendingWidthMult;
    private float _pendingHeightMult;
    private float _pendingTextScale;

    public void Draw()
    {
        try
        {
            if (!_initialized)
            {
                InitializeInputFields();
                _initialized = true;
            }

            GUILayout.BeginVertical(GUILayout.Width(MenuUI.windowWidth * 0.425f));

            DrawGUISettings();

            GUILayout.Space(15);

            DrawSpoofingSettings();

            GUILayout.Space(15);

            DrawPrivacySettings();

            GUILayout.Space(15);

            DrawCheatKeybinds();
            DrawHotkeys();

            GUILayout.Space(15);

            DrawStatusHud();
            DrawRadial();
            DrawTheme();

            GUILayout.EndVertical();
        }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "SettingsTab.Draw: draw settings controls"); }
    }

    private void InitializeInputFields()
    {
        _menuKeybindField = new TextField(MalumMenu.menuKeybind.Value);
        _menuColorField = new TextField(MalumMenu.menuHtmlColor.Value);
        _spoofLevelField = new TextField(MalumMenu.spoofLevel.Value);
        _spoofPlatformField = new TextField(MalumMenu.spoofPlatform.Value);

        _pendingScale = MalumMenu.menuScale.Value;
        _pendingWidthMult = MalumMenu.menuWidthMult.Value;
        _pendingHeightMult = MalumMenu.menuHeightMult.Value;
        _pendingTextScale = MalumMenu.menuTextScale.Value;
    }


    private void DrawGUISettings()
    {
        GUILayout.Label("GUI Settings", GUIStylePreset.TabSubtitle);

        GUILayout.BeginHorizontal();
        GUILayout.Label("Menu Keybind:", GUILayout.Width(150));
        _menuKeybindField.Draw(150);
        if (GUILayout.Button("Save", GUILayout.Width(100)))
        {
            MalumMenu.menuKeybind.Value = _menuKeybindField.Content;
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(5);

        GUILayout.BeginHorizontal();
        GUILayout.Label("Menu Color (HTML):", GUILayout.Width(150));
        _menuColorField.Draw(150);
        if (GUILayout.Button("Save", GUILayout.Width(100)))
        {
            MalumMenu.menuHtmlColor.Value = _menuColorField.Content;
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(5);

        MalumMenu.menuOpenOnMouse.Value = GUILayout.Toggle(MalumMenu.menuOpenOnMouse.Value, " Open Menu on Mouse Position");

        GUILayout.Space(5);

        MalumMenu.autoLoadProfile.Value = GUILayout.Toggle(MalumMenu.autoLoadProfile.Value, " Auto-Load Profile on Startup");

        GUILayout.Space(5);

        GUILayout.BeginHorizontal();
        GUILayout.Label($"Menu Scale: {_pendingScale:F2}", GUILayout.Width(150));
        _pendingScale = GUILayout.HorizontalSlider(_pendingScale, 0.5f, 2.0f);
        GUILayout.EndHorizontal();

        GUILayout.Space(5);

        GUILayout.BeginHorizontal();
        GUILayout.Label($"Width: {_pendingWidthMult:F2}", GUILayout.Width(150));
        _pendingWidthMult = GUILayout.HorizontalSlider(_pendingWidthMult, 0.5f, 2.0f);
        GUILayout.EndHorizontal();

        GUILayout.Space(5);

        GUILayout.BeginHorizontal();
        GUILayout.Label($"Height: {_pendingHeightMult:F2}", GUILayout.Width(150));
        _pendingHeightMult = GUILayout.HorizontalSlider(_pendingHeightMult, 0.5f, 2.0f);
        GUILayout.EndHorizontal();

        GUILayout.Space(5);

        GUILayout.BeginHorizontal();
        GUILayout.Label($"Text Scale: {_pendingTextScale:F2}", GUILayout.Width(150));
        _pendingTextScale = GUILayout.HorizontalSlider(_pendingTextScale, 0.5f, 2.0f);
        GUILayout.EndHorizontal();

        GUILayout.Space(10);

        if (GUILayout.Button("Apply", GUILayout.Width(200)))
        {
            MalumMenu.menuScale.Value = _pendingScale;
            MalumMenu.menuWidthMult.Value = _pendingWidthMult;
            MalumMenu.menuHeightMult.Value = _pendingHeightMult;
            MalumMenu.menuTextScale.Value = _pendingTextScale;
        }
    }

    private int _rebindIndex = -1;
    private string _cheatBindSearch = "";
    private TextField _cheatBindField;
    private bool _cheatBindArmed = false;

    private void DrawCheatKeybinds()
    {
        // The per-cheat Keybinds dictionary has never had a UI - it could only be set by hand
        // editing a profile and pressing "Load from Profile". Rather than render ~200 rows, this
        // searches CheatToggles.ToggleFields by the same humanised name the menu search uses and
        // offers Rebind / Clear for the single best match, plus a blanket clear.
        var cheat = Cheats.MenuSearch.FindCheat(_cheatBindSearch);
        int bound = 0;
        foreach (var kv in CheatToggles.Keybinds)
        {
            if (kv.Value != KeyCode.None)
            {
                bound++;
            }
        }

        GUILayout.Label("Cheat Keybinds", GUIStylePreset.TabSubtitle);
        GUILayout.Label("Type a cheat name to bind a key straight to it.");
        _cheatBindField ??= new TextField(_cheatBindSearch);
        if (!_cheatBindField.IsFocused)
        {
            _cheatBindField.Content = _cheatBindSearch;
        }

        _cheatBindField.Draw(300, 20);
        _cheatBindSearch = _cheatBindField.Content;

        if (MalumMenu.isPanicked)
        {
            _cheatBindArmed = false;
        }

        GUILayout.BeginHorizontal();
        GUILayout.Label(cheat != null ? $"{cheat.Field}  [{CheatKeyLabel(cheat)}]" : "No cheat matches", GUILayout.Width(250));
        if (cheat != null)
        {
            if (GUILayout.Button(_cheatBindArmed ? "press a key" : "Rebind", GUILayout.Width(90)))
            {
                _cheatBindArmed = !_cheatBindArmed;
            }

            if (GUILayout.Button("Clear", GUILayout.Width(60)))
            {
                CheatToggles.Keybinds[cheat.Field] = KeyCode.None;
            }
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label($"{bound} cheat key(s) bound", GUILayout.Width(250));
        if (GUILayout.Button("Clear all cheats", GUILayout.Width(120)) && bound > 0)
        {
            int cleared = 0;
            foreach (string name in new List<string>(CheatToggles.Keybinds.Keys))
            {
                if (CheatToggles.Keybinds[name] != KeyCode.None)
                {
                    CheatToggles.Keybinds[name] = KeyCode.None;
                    cleared++;
                }
            }

            MalumMenu.notifications.Send("Hotkeys", $"Cleared {cleared} cheat keybind(s).", 2.5f);
        }
        GUILayout.EndHorizontal();

        if (!_cheatBindArmed || cheat == null)
        {
            _cheatBindArmed = false;
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape) || !Input.anyKeyDown)
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                _cheatBindArmed = false;
            }

            return;
        }

        foreach (KeyCode kc in (KeyCode[])Enum.GetValues(typeof(KeyCode)))
        {
            if (kc == KeyCode.None || !Input.GetKeyDown(kc))
            {
                continue;
            }

            if (Cheats.Hotkeys.IsModKey(kc))
            {
                continue;
            }

            CheatToggles.Keybinds[cheat.Field] = kc;
            _cheatBindArmed = false;
            break;
        }
    }

    private string CheatKeyLabel(Cheats.SearchHit hit)
    {
        if (CheatToggles.Keybinds.TryGetValue(hit.Field, out KeyCode k) && k != KeyCode.None)
        {
            return k.ToString();
        }

        return "unbound";
    }

    private void DrawHotkeys()
    {
        // Second binding layer: one-shot actions (menus, hotkeys) as opposed to src's original
        // per-cheat Keybinds dictionary, which KeybindListener still owns.
        var entries = Cheats.Hotkeys.All;
        if (entries.Count == 0)
        {
            return;
        }

        GUILayout.Label("Hotkeys", GUIStylePreset.TabSubtitle);
        GUILayout.Label("Master switch for every hotkey: the action list below and the per-cheat keys.");
        CheatToggles.hotkeysEnabled = GUILayout.Toggle(CheatToggles.hotkeysEnabled, " Enable Hotkeys");
        GUILayout.Space(4);
        GUILayout.BeginHorizontal();
        GUILayout.Label($"{Cheats.Hotkeys.BoundCount()} of {entries.Count} actions bound", GUILayout.Width(185));
        if (GUILayout.Button("Clear all actions", GUILayout.Width(120)))
        {
            int cleared = Cheats.Hotkeys.ClearAll();
            _rebindIndex = -1;
            MalumMenu.notifications.Send("Hotkeys", $"Cleared {cleared} action hotkey(s).", 2.5f);
        }
        GUILayout.EndHorizontal();
        GUILayout.Space(6);
        GUILayout.Label("Action Hotkeys", GUIStylePreset.TabSubtitle);
        GUILayout.Label("Rebind an action, prefix it with a modifier, or clear it.");
        GUILayout.Space(4);

        if (MalumMenu.isPanicked)
        {
            _rebindIndex = -1;
        }

        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            GUILayout.BeginHorizontal();
            GUILayout.Label(e.Label, GUILayout.Width(185));

            if (_rebindIndex == i)
            {
                GUILayout.Label("<press a key, Esc cancels>", GUILayout.Width(210));
            }
            else
            {
                GUILayout.Label(Cheats.Hotkeys.Display(e), GUILayout.Width(120));
                if (GUILayout.Button("Rebind", GUILayout.Width(70)))
                {
                    _rebindIndex = i;
                }

                if (GUILayout.Button("Clear", GUILayout.Width(60)))
                {
                    Cheats.Hotkeys.Clear(e);
                }
            }

            if (GUILayout.Button(Cheats.Hotkeys.Prefix(Cheats.Hotkeys.Mod(e)), GUILayout.Width(60)))
            {
                Cheats.Hotkeys.CycleMod(e);
            }

            GUILayout.EndHorizontal();
        }

        // Capture the next keypress while a row is armed. Rebinding suppresses the action
        // dispatcher so the keypress that finishes a rebind cannot also fire that action.
        Cheats.Hotkeys.Rebinding = _rebindIndex >= 0 || _cheatBindArmed;
        if (_rebindIndex < 0 || _rebindIndex >= entries.Count)
        {
            _rebindIndex = -1;
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            _rebindIndex = -1;
            return;
        }

        if (!Input.anyKeyDown)
        {
            return;
        }

        KeyCode pressed = KeyCode.None;
        foreach (KeyCode kc in (KeyCode[])Enum.GetValues(typeof(KeyCode)))
        {
            if (Input.GetKeyDown(kc))
            {
                pressed = kc;
                break;
            }
        }

        Cheats.Hotkeys.Set(entries[_rebindIndex], pressed);
        _rebindIndex = -1;
    }

    private void DrawSpoofingSettings()
    {
        GUILayout.Label("Spoofing Settings", GUIStylePreset.TabSubtitle);

        GUILayout.BeginHorizontal();
        GUILayout.Label("Spoof Level (1-100001):", GUILayout.Width(150));
        _spoofLevelField.Draw(150);
        if (GUILayout.Button("Save", GUILayout.Width(100)))
        {
            // Validate that it's a number between 1 and 100001
            if (int.TryParse(_spoofLevelField.Content, NumberStyles.Integer, CultureInfo.InvariantCulture, out int level) &&
                level >= 1 && level <= 100001)
            {
                MalumMenu.spoofLevel.Value = _spoofLevelField.Content;
            }
            else
            {
                _spoofLevelField.Content = MalumMenu.spoofLevel.Value;
            }
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(5);

        GUILayout.BeginHorizontal();
        GUILayout.Label("Spoof Platform:", GUILayout.Width(150));
        _spoofPlatformField.Draw(150);
        if (GUILayout.Button("Save", GUILayout.Width(100)))
        {
            MalumMenu.spoofPlatform.Value = _spoofPlatformField.Content;
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(5);

        GUILayout.Label("Supported Platforms: StandaloneEpicPC, StandaloneSteamPC, StandaloneMac, StandaloneWin10, etc.");
    }

    private void DrawPrivacySettings()
    {
        GUILayout.Label("Privacy Settings", GUIStylePreset.TabSubtitle);

        MalumMenu.spoofDeviceId.Value = GUILayout.Toggle(MalumMenu.spoofDeviceId.Value, " Hide Device ID");

        GUILayout.Space(5);

        MalumMenu.noTelemetry.Value = GUILayout.Toggle(MalumMenu.noTelemetry.Value, " Disable Telemetry");

        GUILayout.Space(10);

        if (GUILayout.Button("Open Config File", GUILayout.Width(200)))
        {
            Utils.OpenConfigFile();
        }

        GUILayout.Space(5);

        GUILayout.Label("For more advanced configuration options, click 'Open Config File'", GUIStylePreset.TabSubtitle);
    }

    private void DrawStatusHud()
    {
        GUILayout.Label("Status HUD", GUIStylePreset.TabSubtitle);

        GUILayout.Space(5);

        CheatToggles.gradientStamp = GUILayout.Toggle(CheatToggles.gradientStamp, " Animated Gradient Stamp (off = plain text)");

        // These used to be nested under the gradient toggle, which would have stranded them once
        // the stamp defaults to plain text. They apply to the stamp either way.
        CheatToggles.showFps = GUILayout.Toggle(CheatToggles.showFps, " Show FPS in Stamp");
        CheatToggles.showLobbyTimer = GUILayout.Toggle(CheatToggles.showLobbyTimer, " Show Lobby Timer in Stamp");
        CheatToggles.showHostLine = GUILayout.Toggle(CheatToggles.showHostLine, " Show Host Under Stamp (In Match)");

        GUILayout.Space(5);

        GUILayout.Label($"Stamp: {Cheats.StatusHud.StampLine()}");

        if (Cheats.StatusHud.TryLobbyTimer(out int remaining))
        {
            GUILayout.Label($"Lobby {remaining / 60}:{remaining % 60:00}   FPS {Cheats.StatusHud.CurrentFps}   Ping {Utils.GetPing()} ms");
        }
    }


    private void DrawRadial()
    {
        GUILayout.Label("Quick Menu", GUIStylePreset.TabSubtitle);
        CheatToggles.radialMenu = GUILayout.Toggle(CheatToggles.radialMenu, " Enable Quick Menu");
        GUILayout.Label("Key: " + Cheats.Hotkeys.Display(Cheats.Hotkeys.Get("Radial")));
        GUILayout.Label("Starred: " + Cheats.QuickMenu.FavList.Count + " / " + Cheats.QuickMenu.MaxFavs);
        if (GUILayout.Button("Clear Starred"))
        {
            Cheats.QuickMenu.ClearFavs();
        }
        GUILayout.Space(6);
    }


    private void DrawTheme()
    {
        Cheats.MenuTheme.Bind();
        GUILayout.Label("Menu Look", GUIStylePreset.TabSubtitle);
        Cheats.MenuTheme.Opacity.Value = GUILayout.HorizontalSlider(
            Cheats.MenuTheme.Opacity.Value, 0.45f, 1f, GUILayout.Width(260f));
        GUILayout.Label("Opacity: " + Mathf.RoundToInt(Cheats.MenuTheme.Opacity.Value * 100f) + "%");
        if (GUILayout.Button("Columns: " + Cheats.MenuTheme.ColumnLabel() + " (search results)", GUILayout.Width(260f)))
        {
            Cheats.MenuTheme.CycleColumns();
        }

        GUILayout.Label("Searchable features: " + Cheats.MenuSearch.Count);
        GUILayout.Space(6);
    }

}
