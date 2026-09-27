using System.Collections;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using MalumMenu.features;
using InnerNet;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;


namespace MalumMenu;

public class HostOnlyTab2 : ITab
{
    private const int HandlingId = 60009;
    public string name => "Host-Only 2";

    private byte selectedMap = 0;

    public void Draw()
    {
        try
        {
            if (PlayerControl.LocalPlayer == null)
        {
            GUILayout.Label("You are not currently in a game, these options will not work.");
        }
        else if (!AmongUsClient.Instance.AmHost)
        {
            GUILayout.Label("You are not the host of the current lobby. Using these options will either do nothing or get you banned by the anticheat");
        }
        Host.BanMidGame.Enabled = GUILayout.Toggle(Host.BanMidGame.Enabled, "Be able to ban players mid-game");

        Host.FlippedSkeld = GUILayout.Toggle(Host.FlippedSkeld, "Use Flipped Skeld Map");

        Host.DisableMeetings.Enabled = GUILayout.Toggle(Host.DisableMeetings.Enabled, "Disable Meetings");
        Host.DisableSabotages.Enabled = GUILayout.Toggle(Host.DisableSabotages.Enabled, "Disable Sabotages");
        Host.DisableCloseDoors.Enabled = GUILayout.Toggle(Host.DisableCloseDoors.Enabled, "Disable Close Doors");
        Host.DisableCameras.Enabled = GUILayout.Toggle(Host.DisableCameras.Enabled, "Disable Security Cameras");
        Host.DisableGameEnd.Enabled = GUILayout.Toggle(Host.DisableGameEnd.Enabled, "Disable Game End");
        Host.NoKillCooldown.Enabled = GUILayout.Toggle(Host.NoKillCooldown.Enabled, "No Kill Cooldown");

        GUILayout.BeginHorizontal();
        Host.BlockLowLevels.Enabled = GUILayout.Toggle(Host.BlockLowLevels.Enabled, $"Kick players with less than {Host.BlockLowLevels.MinLevel} levels");
        Host.BlockLowLevels.MinLevel = (uint)GUILayout.HorizontalSlider(Host.BlockLowLevels.MinLevel, 0, 100);
        GUILayout.EndHorizontal();

        MalumMenu.routines.reportBodySpam.Enabled = GUILayout.Toggle(MalumMenu.routines.reportBodySpam.Enabled, "Spam Report Bodies");

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Force Crewmate Victory"))
        {
            // Just incase the user has this enabled
            Host.DisableGameEnd.Enabled = false;

            GameManager.Instance.RpcEndGame(GameOverReason.CrewmatesByTask, false);
            MalumMenu.notifications.Send("Game Finished", "You ended the game with a crewmate victory.", 5);
        }

        if (GUILayout.Button("Force Imposter Victory"))
        {
            // Just incase the user has this enabled
            Host.DisableGameEnd.Enabled = false;

            GameManager.Instance.RpcEndGame(GameOverReason.ImpostorsByKill, false);
            MalumMenu.notifications.Send("Game Finished", "You ended the game with an imposter victory.", 5);
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(5);
        GUILayout.Label("Map Spawner/Despawner:");

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Despawn Lobby"))
        {
            if (LobbyBehaviour.Instance != null)
            {
                LobbyBehaviour.Instance.Despawn();
                MalumMenu.notifications.Send("Lobby Map", "The lobby map has been despawned.", 5);
            }
            else
            {
                MalumMenu.notifications.Send("Lobby Map", "The lobby map has already been despawned.", 5);
            }
        }

        if (GUILayout.Button("Spawn Lobby"))
        {
            // From GameStartManager::Start
            LobbyBehaviour.Instance = Object.Instantiate<LobbyBehaviour>(GameStartManager.Instance.LobbyPrefab);
            AmongUsClient.Instance.Spawn(LobbyBehaviour.Instance, -2, SpawnFlags.None);

            MalumMenu.notifications.Send("Lobby Map", "A new instance of the lobby map has been spawned", 5);
        }
        GUILayout.EndHorizontal();

        GUILayout.Label($"Selected map: {(MapNames)selectedMap}");
        selectedMap = (byte)GUILayout.HorizontalSlider(selectedMap, 0, 5);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Despawn Map"))
        {
            if (ShipStatus.Instance != null)
            {
                ShipStatus.Instance.Despawn();
                MalumMenu.notifications.Send("Game Map", "The current map has been despawned.", 5);
            }
            else
            {
                MalumMenu.notifications.Send("Game Map", "The game map has already been despawned.", 5);
            }
        }

        if (GUILayout.Button("Spawn Map"))
        {
            AmongUsClient.Instance.StartCoroutine(ErrorReporter.GuardCoroutine(SpawnMap(selectedMap), HandlingId, "SpawnMap").WrapToIl2Cpp());
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(5);
        GUILayout.Label("Lobby Tools (HOST):");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Destroy Lobby"))
            MalumMenu.notifications.Send("Lobby", Cheats.LobbyTools.DestroyLobby());
        if (GUILayout.Button("Create Lobby"))
            MalumMenu.notifications.Send("Lobby", Cheats.LobbyTools.CreateLobby());
        if (GUILayout.Button("Leave Lobby"))
            Cheats.LobbyTools.RequestLeave();
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        string[] fakeMapNames = { "Skeld", "Mira", "Polus", "Dleks", "Airship", "Fungle" };
        int fakeMap = Mathf.Clamp(Cheats.LobbyTools.FakeMap.FakeMapId, 0, 5);
        if (GUILayout.Button($"Fake Map: {fakeMapNames[fakeMap]}"))
            Cheats.LobbyTools.FakeMap.FakeMapId = (fakeMap + 1) % 6;
        string fakeState = Cheats.LobbyTools.FakeMap.Loading ? "Loading..."
            : Cheats.LobbyTools.FakeMap.Active ? "ON - Turn OFF" : "OFF - Turn ON";
        if (GUILayout.Button($"Fake Map {fakeState}"))
        {
            if (Cheats.LobbyTools.FakeMap.Active)
                MalumMenu.notifications.Send("Fake Map", Cheats.LobbyTools.FakeMap.DisableAndRestoreLobby());
            else
            {
                Cheats.LobbyTools.FakeMap.Enable(Cheats.LobbyTools.FakeMap.FakeMapId);
                MalumMenu.notifications.Send("Fake Map", "Spawning fake map...");
            }
        }
        GUILayout.EndHorizontal();

        CheatToggles.autoReturnLobby = GUILayout.Toggle(CheatToggles.autoReturnLobby, " Auto-Return to Lobby After Match");

        GUILayout.Label("Lobby History:");
        var history = Cheats.LobbyTools.LobbyHistory.Entries;
        if (history.Count == 0)
            GUILayout.Label("  No past lobbies recorded.");
        int shown = 0;
        foreach (var row in history)
        {
            if (shown++ >= 5) break;
            GUILayout.BeginHorizontal();
            GUILayout.Label($"  {row.Code} {row.When} {row.Host} {row.Map} ({row.Players})", GUILayout.Width(MenuUI.windowWidth * 0.30f));
            if (GUILayout.Button("Rejoin", GUILayout.Width(70)))
                MalumMenu.notifications.Send("Lobby", Cheats.LobbyTools.LobbyHistory.Rejoin(row));
            GUILayout.EndHorizontal();
        }
        if (GUILayout.Button("Clear History"))
        {
            Cheats.LobbyTools.LobbyHistory.Clear();
            MalumMenu.notifications.Send("Lobby", "History cleared.");
        }

        GUILayout.Space(5);

        GUILayout.Label("Dummies (HOST):");
        CheatToggles.enableDummies = GUILayout.Toggle(CheatToggles.enableDummies, " Enable Dummies");
        CheatToggles.dummyDoTasks = GUILayout.Toggle(CheatToggles.dummyDoTasks, " Do Tasks");
        CheatToggles.dummyFixSabotage = GUILayout.Toggle(CheatToggles.dummyFixSabotage, " Fix Sabotage");
        CheatToggles.dummyReportBodies = GUILayout.Toggle(CheatToggles.dummyReportBodies, " Report Bodies + Chat + Vote");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Spawn Dummy"))
            MalumMenu.notifications.Send("Dummies", Cheats.Dummies.SpawnNow());
        GUILayout.Label($"Dummies alive: {Cheats.Dummies.Count}");
        GUILayout.EndHorizontal();

        GUILayout.Space(5);

        GUILayout.Label("Disco Party:");
        MalumMenu.routines.discoHost.Enabled = GUILayout.Toggle(MalumMenu.routines.discoHost.Enabled, "Enabled");
        GUILayout.Label($"Color randomization delay: {MalumMenu.routines.discoHost.randomizationDelay:F2}s");
        MalumMenu.routines.discoHost.randomizationDelay = GUILayout.HorizontalSlider(MalumMenu.routines.discoHost.randomizationDelay, 0.1f, 2.0f);

        GUILayout.Space(5);
        DrawLobbySettings();
        GUILayout.Space(5);
        DrawPresets();
        GUILayout.Space(5);
        DrawClones();
        GUILayout.Space(5);
        DrawNetClones();
        GUILayout.Space(5);
        DrawPranks();
        GUILayout.Space(5);
        DrawAutoHost();
        GUILayout.Space(5);
        DrawLobbyBrowser();
        GUILayout.Space(5);
        DrawGuard();
        }
        catch (System.Exception ex) { ErrorReporter.Report(ex, HandlingId, "HostOnlyTab2.Draw: draw host-only 2 controls"); }
    }
    private TextField _presetNameField;

    // Access lists, join gate, join intel and the RPC guards. Host-only because every one of
    // them acts on other players through the host's own kick/ban authority.
    private void DrawGuard()
    {
        GUILayout.BeginVertical(GUI.skin.box);
        GUILayout.Label("Guard (HOST)");

        BoolSetting("Ban list (kick on join)", CheatToggles.accessBanEnabled, v => CheatToggles.accessBanEnabled = v);
        BoolSetting("Whitelist only", CheatToggles.accessWhitelistOnly, v => CheatToggles.accessWhitelistOnly = v);
        BoolSetting("Nick ban list", CheatToggles.accessNickBanEnabled, v => CheatToggles.accessNickBanEnabled = v);
        BoolSetting("Platform ban list", CheatToggles.accessPlatformBanEnabled, v => CheatToggles.accessPlatformBanEnabled = v);
        BoolSetting("Kick Fortegreen (18)", CheatToggles.kickFortegreen, v => CheatToggles.kickFortegreen = v);
        BoolSetting("Min level gate", CheatToggles.minLevelEnabled, v => CheatToggles.minLevelEnabled = v);
        IntSetting("Min level", CheatToggles.minLevel, 1, 9999, v => CheatToggles.minLevel = v);
        BoolSetting("Max level gate", CheatToggles.maxLevelEnabled, v => CheatToggles.maxLevelEnabled = v);
        IntSetting("Max level", CheatToggles.maxLevel, 1, 9999, v => CheatToggles.maxLevel = v);
        if (GUILayout.Button($"On level violation: {Cheats.AccessLists.LevelActionLabel()}"))
        {
            Cheats.AccessLists.CycleLevelAction();
        }

        GUILayout.Label($"Bans: {Cheats.AccessLists.BanEntries.Count}   Whites: {Cheats.AccessLists.WhiteEntries.Count}   Nicks: {Cheats.AccessLists.NickBanEntries.Count}   Platforms: {Cheats.AccessLists.PlatformBanEntries.Count}");

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("CLR BAN", GUILayout.Width(90f))) { Cheats.AccessLists.ClearBans(); }
        if (GUILayout.Button("CLR WHITE", GUILayout.Width(95f))) { Cheats.AccessLists.ClearWhites(); }
        if (GUILayout.Button("CLR NICK", GUILayout.Width(90f))) { Cheats.AccessLists.ClearNickBans(); }
        if (GUILayout.Button("CLR PLAT", GUILayout.Width(90f))) { Cheats.AccessLists.ClearPlatformBans(); }
        GUILayout.EndHorizontal();

        var bans = Cheats.AccessLists.BanEntries;
        for (int i = 0; i < bans.Count && i < 8; i++)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"{(bans[i].Name.Length > 0 ? bans[i].Name : bans[i].Code)}  {(bans[i].Code.Length > 0 ? bans[i].Code : bans[i].Puid)}");
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("X", GUILayout.Width(28f)))
            {
                Cheats.AccessLists.RemoveBan(bans[i].Code.Length > 0 ? bans[i].Code : bans[i].Puid);
                break;
            }
            GUILayout.EndHorizontal();
        }

        var whites = Cheats.AccessLists.WhiteEntries;
        for (int i = 0; i < whites.Count && i < 6; i++)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"+ {(whites[i].Name.Length > 0 ? whites[i].Name : whites[i].Code)}");
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("X", GUILayout.Width(28f)))
            {
                Cheats.AccessLists.RemoveWhite(whites[i].Code.Length > 0 ? whites[i].Code : whites[i].Puid);
                break;
            }
            GUILayout.EndHorizontal();
        }

        GUILayout.Space(4);
        GUILayout.Label("Join intel");
        BoolSetting("Show join/leave toasts", CheatToggles.joinIntel, v => CheatToggles.joinIntel = v);
        BoolSetting("Toast on top of the log", CheatToggles.joinIntelToasts, v => CheatToggles.joinIntelToasts = v);
        BoolSetting("Notify known players", CheatToggles.notifyKnown, v => CheatToggles.notifyKnown = v);
        BoolSetting("Record nick history", CheatToggles.nameHistory, v => CheatToggles.nameHistory = v);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Show Event Log", GUILayout.Width(130f))) { CheatToggles.showEventLog = !CheatToggles.showEventLog; }
        if (GUILayout.Button("Show Replay", GUILayout.Width(120f))) { CheatToggles.showReplay = !CheatToggles.showReplay; }
        GUILayout.EndHorizontal();

        GUILayout.BeginVertical(GUI.skin.box);
        GUILayout.Label("Event notifications");
        BoolSetting("Mirror to console", CheatToggles.mirrorEventsToConsole, v => CheatToggles.mirrorEventsToConsole = v);
        BoolSetting("Kills", CheatToggles.notifyKills, v => CheatToggles.notifyKills = v);
        BoolSetting("Meetings", CheatToggles.notifyMeetings, v => CheatToggles.notifyMeetings = v);
        BoolSetting("Sabotage", CheatToggles.notifySabotage, v => CheatToggles.notifySabotage = v);
        BoolSetting("Vents", CheatToggles.notifyVents, v => CheatToggles.notifyVents = v);
        BoolSetting("Roles", CheatToggles.notifyRoles, v => CheatToggles.notifyRoles = v);
        BoolSetting("Reports", CheatToggles.notifyReports, v => CheatToggles.notifyReports = v);
        BoolSetting("Ejects", CheatToggles.notifyEjects, v => CheatToggles.notifyEjects = v);
        BoolSetting("Vote kicks", CheatToggles.notifyVotekicks, v => CheatToggles.notifyVotekicks = v);
        GUILayout.EndVertical();

        GUILayout.BeginVertical(GUI.skin.box);
        GUILayout.Label("Protections");
        BoolSetting("Catch impossible RPCs", CheatToggles.rpcGuard, v => CheatToggles.rpcGuard = v);
        BoolSetting("Toast on a caught RPC", CheatToggles.rpcGuardToast, v => CheatToggles.rpcGuardToast = v);
        if (GUILayout.Button($"On a caught RPC: {Cheats.AccessLists.RpcActionLabel()}"))
        {
            Cheats.AccessLists.CycleRpcAction();
        }
        BoolSetting("Host-side anti-ban", CheatToggles.antiBanHost, v => CheatToggles.antiBanHost = v);
        BoolSetting("Block forced vents", CheatToggles.blockForcedVents, v => CheatToggles.blockForcedVents = v);
        BoolSetting("Block forced ziplines", CheatToggles.blockForcedZipline, v => CheatToggles.blockForcedZipline = v);
        BoolSetting("Block fake meetings", CheatToggles.blockFakeMeetings, v => CheatToggles.blockFakeMeetings = v);
        BoolSetting("Block spawn floods", CheatToggles.blockSpawnFloods, v => CheatToggles.blockSpawnFloods = v);
        BoolSetting("Guard toasts", CheatToggles.securityNotify, v => CheatToggles.securityNotify = v);
        BoolSetting("Toast on foreign mods", CheatToggles.modDetectToast, v => CheatToggles.modDetectToast = v);
        GUILayout.EndVertical();

        GUILayout.EndVertical();
    }

    private void DrawLobbySettings()
    {
        try
        {
            GUILayout.Label("Lobby Settings (HOST):");
            if (!Cheats.LobbySettings.Ready())
            {
                GUILayout.Label("Host in lobby only — join a lobby as host to edit options.");
                return;
            }

            string[] mapNames = { "Skeld", "Mira", "Polus", "Dleks", "Airship", "Fungle" };
            int map = Mathf.Clamp(Cheats.LobbySettings.Map(), 0, 5);
            if (GUILayout.Button($"Map: {mapNames[map]}"))
                Cheats.LobbySettings.SetMap((map + 1) % 6);

            IntSetting("Max Players", Cheats.LobbySettings.Players(), 4, 15, Cheats.LobbySettings.SetPlayers);
            IntSetting("Impostors", Cheats.LobbySettings.Imps(), 1, 3, Cheats.LobbySettings.SetImps);
            FloatSetting("Kill Cooldown", Cheats.LobbySettings.KillCd(), 0f, 60f, Cheats.LobbySettings.SetKillCd, "F1");
            string[] distNames = { "Short", "Medium", "Long" };
            int dist = Mathf.Clamp(Cheats.LobbySettings.KillDist(), 0, 2);
            if (GUILayout.Button($"Kill Distance: {distNames[dist]}"))
                Cheats.LobbySettings.SetKillDist((dist + 1) % 3);
            FloatSetting("Player Speed", Cheats.LobbySettings.Speed(), 0.25f, 3f, Cheats.LobbySettings.SetSpeed, "F2");
            FloatSetting("Crew Vision", Cheats.LobbySettings.CrewVis(), 0f, 5f, Cheats.LobbySettings.SetCrewVis, "F2");
            FloatSetting("Impostor Vision", Cheats.LobbySettings.ImpVis(), 0f, 5f, Cheats.LobbySettings.SetImpVis, "F2");

            GUILayout.Space(3);
            GUILayout.Label("Meetings & Voting:");
            IntSetting("Emergency Meetings", Cheats.LobbySettings.Meetings(), 0, 9, Cheats.LobbySettings.SetMeetings);
            IntSetting("Emergency Cooldown", Cheats.LobbySettings.MeetingCd(), 0, 60, Cheats.LobbySettings.SetMeetingCd);
            IntSetting("Discussion Time", Cheats.LobbySettings.Discuss(), 0, 300, Cheats.LobbySettings.SetDiscuss);
            IntSetting("Voting Time", Cheats.LobbySettings.Voting(), 0, 300, Cheats.LobbySettings.SetVoting);
            BoolSetting("Anonymous Votes", Cheats.LobbySettings.Anon(), Cheats.LobbySettings.SetAnon);
            BoolSetting("Confirm Ejects", Cheats.LobbySettings.Confirm(), Cheats.LobbySettings.SetConfirm);

            GUILayout.Space(3);
            GUILayout.Label("Tasks:");
            string[] barNames = { "Always", "Meetings", "Never" };
            int bar = Mathf.Clamp(Cheats.LobbySettings.TaskBar(), 0, 2);
            if (GUILayout.Button($"Task Bar: {barNames[bar]}"))
                Cheats.LobbySettings.SetTaskBar((bar + 1) % 3);
            IntSetting("Common Tasks", Cheats.LobbySettings.Common(), 0, 5, Cheats.LobbySettings.SetCommon);
            IntSetting("Long Tasks", Cheats.LobbySettings.Long(), 0, 5, Cheats.LobbySettings.SetLong);
            IntSetting("Short Tasks", Cheats.LobbySettings.Short(), 0, 5, Cheats.LobbySettings.SetShort);
            BoolSetting("Visual Tasks", Cheats.LobbySettings.Visual(), Cheats.LobbySettings.SetVisual);

            GUILayout.Space(3);
            GUILayout.Label("Roles (count / chance %):");
            foreach (AmongUs.GameOptions.RoleTypes role in Cheats.LobbySettings.RateRoles)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{role}", GUILayout.Width(110));
                int num = Cheats.LobbySettings.RoleNum(role);
                int newNum = Mathf.RoundToInt(GUILayout.HorizontalSlider(num, 0, 3, GUILayout.Width(90)));
                if (newNum != num) Cheats.LobbySettings.SetRole(role, newNum, Cheats.LobbySettings.RoleChance(role));
                int chance = Cheats.LobbySettings.RoleChance(role);
                int newChance = Mathf.RoundToInt(GUILayout.HorizontalSlider(chance, 0, 100, GUILayout.Width(90)));
                if (newChance != chance) Cheats.LobbySettings.SetRole(role, Cheats.LobbySettings.RoleNum(role), newChance);
                GUILayout.Label($"{Cheats.LobbySettings.RoleNum(role)}x {Cheats.LobbySettings.RoleChance(role)}%", GUILayout.Width(70));
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(3);
            GUILayout.Label("Role Options:");
            FloatSetting("Scientist Cooldown", Cheats.LobbySettings.SciCd(), 0f, 60f, Cheats.LobbySettings.SetSciCd, "F0");
            FloatSetting("Scientist Battery", Cheats.LobbySettings.SciBat(), 0f, 60f, Cheats.LobbySettings.SetSciBat, "F0");
            FloatSetting("Engineer Cooldown", Cheats.LobbySettings.EngCd(), 0f, 60f, Cheats.LobbySettings.SetEngCd, "F0");
            FloatSetting("Engineer Vent Time", Cheats.LobbySettings.EngVent(), 0f, 300f, Cheats.LobbySettings.SetEngVent, "F0");
            FloatSetting("Angel Cooldown", Cheats.LobbySettings.GaCd(), 0f, 120f, Cheats.LobbySettings.SetGaCd, "F0");
            FloatSetting("Protect Duration", Cheats.LobbySettings.GaDur(), 0f, 60f, Cheats.LobbySettings.SetGaDur, "F0");
            BoolSetting("Imps See Protect", Cheats.LobbySettings.GaImpSee(), Cheats.LobbySettings.SetGaImpSee);
            FloatSetting("Tracker Cooldown", Cheats.LobbySettings.TrCd(), 0f, 60f, Cheats.LobbySettings.SetTrCd, "F0");
            FloatSetting("Tracker Duration", Cheats.LobbySettings.TrDur(), 0f, 60f, Cheats.LobbySettings.SetTrDur, "F0");
            FloatSetting("Tracker Delay", Cheats.LobbySettings.TrDelay(), 0f, 60f, Cheats.LobbySettings.SetTrDelay, "F0");
            FloatSetting("Noisemaker Duration", Cheats.LobbySettings.NmDur(), 0f, 15f, Cheats.LobbySettings.SetNmDur, "F0");
            BoolSetting("Alert Impostors", Cheats.LobbySettings.NmImpAlert(), Cheats.LobbySettings.SetNmImpAlert);
            FloatSetting("Detective Limit", Cheats.LobbySettings.DetLimit(), 0f, 15f, Cheats.LobbySettings.SetDetLimit, "F0");
            FloatSetting("Shapeshifter Cooldown", Cheats.LobbySettings.SsCd(), 0f, 60f, Cheats.LobbySettings.SetSsCd, "F0");
            FloatSetting("Shapeshifter Duration", Cheats.LobbySettings.SsDur(), 0f, 60f, Cheats.LobbySettings.SetSsDur, "F0");
            BoolSetting("Leave Skin", Cheats.LobbySettings.SsSkin(), Cheats.LobbySettings.SetSsSkin);
            FloatSetting("Phantom Cooldown", Cheats.LobbySettings.PhCd(), 0f, 60f, Cheats.LobbySettings.SetPhCd, "F0");
            FloatSetting("Phantom Duration", Cheats.LobbySettings.PhDur(), 0f, 60f, Cheats.LobbySettings.SetPhDur, "F0");
            FloatSetting("Viper Dissolve", Cheats.LobbySettings.VpDis(), 0f, 30f, Cheats.LobbySettings.SetVpDis, "F0");
            FloatSetting("Judge Task %", Cheats.LobbySettings.JudgeTaskPct(), 0f, 100f, Cheats.LobbySettings.SetJudgeTaskPct, "F0");

            if (GUILayout.Button($"Clear Forced Roles ({Cheats.ForceRoles.Count})"))
            {
                Cheats.ForceRoles.Clear();
                MalumMenu.notifications.Send("Force Roles", "All forced roles cleared.");
            }
        }
        catch (System.Exception ex) { ErrorReporter.Report(ex, HandlingId, "HostOnlyTab2.DrawLobbySettings: draw lobby settings"); }
    }

    private void DrawPresets()
    {
        try
        {
            GUILayout.Label("Lobby Presets (HOST):");
            if (_presetNameField == null) _presetNameField = new TextField(Cheats.LobbyPresets.PresetName);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Name:", GUILayout.Width(60));
            _presetNameField.Draw(140);
            if (GUILayout.Button("SAVE", GUILayout.Width(80)))
            {
                Cheats.LobbyPresets.PresetName = _presetNameField.Content;
                MalumMenu.notifications.Send("Presets", Cheats.LobbyPresets.Save(_presetNameField.Content));
            }
            GUILayout.EndHorizontal();
            foreach (string pname in Cheats.LobbyPresets.Names())
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(pname, GUILayout.Width(160));
                if (GUILayout.Button("Apply", GUILayout.Width(80)))
                    MalumMenu.notifications.Send("Presets", Cheats.LobbyPresets.Apply(pname));
                if (GUILayout.Button("X", GUILayout.Width(30)))
                    Cheats.LobbyPresets.Delete(pname);
                GUILayout.EndHorizontal();
            }
        }
        catch (System.Exception ex) { ErrorReporter.Report(ex, HandlingId, "HostOnlyTab2.DrawPresets: draw lobby presets"); }
    }

    private TextField _cloneTextField;
    private TextField _netCloneTextField;

    private void DrawClones()
    {
        try
        {
            GUILayout.Label("Lobby Clones (local-only):");
            CheatToggles.cloneMode = GUILayout.Toggle(CheatToggles.cloneMode, " Clone Mode (LMB=spawn, RMB=remove, drag=move)");
            CheatToggles.cloneShadow = GUILayout.Toggle(CheatToggles.cloneShadow, " Shadow Clone");
            CheatToggles.cloneGuard = GUILayout.Toggle(CheatToggles.cloneGuard, " Guard Orbit");
            CheatToggles.cloneDrift = GUILayout.Toggle(CheatToggles.cloneDrift, " Wander");
            CheatToggles.cloneNaked = GUILayout.Toggle(CheatToggles.cloneNaked, " Bare Clones");
            IntSetting("Max Clones", CheatToggles.cloneMax, 1, 2000, v => CheatToggles.cloneMax = v);
            IntSetting("Per Click", CheatToggles.clonePerClick, 1, 20, v => CheatToggles.clonePerClick = v);
            FloatSetting("Guard Radius", CheatToggles.cloneGuardRadius, 1f, 8f, v => CheatToggles.cloneGuardRadius = v, "0.0");
            FloatSetting("Clone Scale", CheatToggles.cloneScale, 0.4f, 2f, v => CheatToggles.cloneScale = v, "0.00");
            GUILayout.BeginHorizontal();
            int colorId = Mathf.Clamp(CheatToggles.cloneColorId, -1, 17);
            string colorName = colorId < 0 ? "own" : colorId.ToString();
            if (GUILayout.Button($"Color: {colorName}"))
                CheatToggles.cloneColorId = colorId >= 17 ? -1 : colorId + 1;
            string[] forms = Cheats.LocalClones.FormationNames;
            int fi = Mathf.Clamp(CheatToggles.cloneFormation, 0, forms.Length - 1);
            if (GUILayout.Button($"Formation: {forms[fi]}"))
                CheatToggles.cloneFormation = (fi + 1) % forms.Length;
            GUILayout.EndHorizontal();
            FloatSetting("Formation Size", CheatToggles.cloneFormationScale, 0.3f, 3f, v => CheatToggles.cloneFormationScale = v, "0.00");
            IntSetting("Formation Copies", CheatToggles.cloneFormationCopies, 1, 5, v => CheatToggles.cloneFormationCopies = v);
            CheatToggles.cloneAnim = GUILayout.Toggle(CheatToggles.cloneAnim, " Living Formations");
            if (CheatToggles.cloneAnim)
                FloatSetting("Motion Speed", CheatToggles.cloneAnimSpeed, 0.2f, 3f, v => CheatToggles.cloneAnimSpeed = v, "0.0");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("BUILD"))
                MalumMenu.localClones?.BuildFormation(Mathf.Clamp(CheatToggles.cloneFormation, 0, forms.Length - 1));
            if (GUILayout.Button("CLEAR"))
                MalumMenu.localClones?.ClearAll();
            if (GUILayout.Button("ME FROM CLONES"))
                MalumMenu.localClones?.BuildSelf();
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (_cloneTextField == null) _cloneTextField = new TextField("");
            _cloneTextField.Draw(140);
            if (GUILayout.Button("TEXT FROM CLONES", GUILayout.Width(150)))
                MalumMenu.localClones?.BuildText(_cloneTextField.Content);
            GUILayout.EndHorizontal();
            GUILayout.Label($"Clones: {Cheats.LocalClones.Count}");
        }
        catch (System.Exception ex) { ErrorReporter.Report(ex, HandlingId, "HostOnlyTab2.DrawClones: draw lobby clones"); }
    }

    private void DrawNetClones()
    {
        try
        {
            GUILayout.Label("Networked Clones (HOST, everyone sees):");
            CheatToggles.netCloneMode = GUILayout.Toggle(CheatToggles.netCloneMode, " Click Mode (LMB=spawn, RMB=remove)");
            string[] forms = Cheats.LocalClones.FormationNames;
            int fi = Mathf.Clamp(CheatToggles.cloneFormation, 0, forms.Length - 1);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button($"Formation: {forms[fi]}"))
                CheatToggles.cloneFormation = (fi + 1) % forms.Length;
            GUILayout.EndHorizontal();
            FloatSetting("Formation Size", CheatToggles.cloneFormationScale, 0.3f, 3f, v => CheatToggles.cloneFormationScale = v, "0.00");
            IntSetting("Clones", CheatToggles.netCloneCount, 1, 2000, v => CheatToggles.netCloneCount = v);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("CLONE SELF"))
                MalumMenu.notifications.Send("Clones", Cheats.NetworkedClones.CloneOf(PlayerControl.LocalPlayer));
            if (GUILayout.Button("FORMATION"))
                MalumMenu.notifications.Send("Clones", Cheats.NetworkedClones.Formation(fi, CheatToggles.netCloneCount));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (_netCloneTextField == null) _netCloneTextField = new TextField("");
            _netCloneTextField.Draw(140);
            if (GUILayout.Button("TEXT FROM CLONES", GUILayout.Width(150)))
                MalumMenu.notifications.Send("Clones", Cheats.NetworkedClones.Text(_netCloneTextField.Content));
            GUILayout.EndHorizontal();
            GUILayout.Label($"Clones: {Cheats.NetworkedClones.Count}  Queued: {Cheats.NetworkedClones.Queued}");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("REMOVE LAST"))
                MalumMenu.notifications.Send("Clones", Cheats.NetworkedClones.DropLast());
            if (GUILayout.Button("CLEAR ALL"))
                Cheats.NetworkedClones.ClearAll();
            GUILayout.EndHorizontal();
        }
        catch (System.Exception ex) { ErrorReporter.Report(ex, HandlingId, "HostOnlyTab2.DrawNetClones: draw networked clones"); }
    }

    private void DrawPranks()
    {
        try
        {
            GUILayout.Label("Eggs (HOST, in match):");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button($"All To Eggs"))
                MalumMenu.notifications.Send("Pranks", Cheats.LobbyPranks.MassMorphToEgg());
            if (GUILayout.Button("Morph To Target"))
                MalumMenu.notifications.Send("Pranks", Cheats.LobbyPranks.MorphAllIntoSelected());
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button($"Rainbow: {(Cheats.LobbyPranks.RainbowActive ? "ON" : "off")}"))
                MalumMenu.notifications.Send("Pranks", Cheats.LobbyPranks.ToggleRainbow());
            if (GUILayout.Button($"Cosmetic Cycle: {(Cheats.LobbyPranks.SkinCycleActive ? "ON" : "off")}"))
                MalumMenu.notifications.Send("Pranks", Cheats.LobbyPranks.ToggleSkinCycle());
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button($"Beat: {Cheats.LobbyPranks.SyncName()}"))
                MalumMenu.notifications.Send("Pranks", Cheats.LobbyPranks.ToggleSync());
            if (GUILayout.Button($"Size: {Cheats.LobbyPranks.ScaleName()}"))
                MalumMenu.notifications.Send("Pranks", Cheats.LobbyPranks.CycleScale());
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button($"Motion: {Cheats.LobbyPranks.SpinName()}"))
                MalumMenu.notifications.Send("Pranks", Cheats.LobbyPranks.CycleSpin());
            if (GUILayout.Button($"Anim: {Cheats.LobbyPranks.AnimName()}"))
                MalumMenu.notifications.Send("Pranks", Cheats.LobbyPranks.CycleAnim());
            GUILayout.EndHorizontal();
            if (GUILayout.Button("RESET LOOK"))
                MalumMenu.notifications.Send("Pranks", Cheats.LobbyPranks.ResetAppearance());
        }
        catch (System.Exception ex) { ErrorReporter.Report(ex, HandlingId, "HostOnlyTab2.DrawPranks: draw lobby pranks"); }
    }

    private void DrawAutoHost()
    {
        try
        {
            GUILayout.Label("Auto-host (HOST, in lobby):");
            BoolSetting("Enabled", CheatToggles.autoHost, v => CheatToggles.autoHost = v);
            BoolSetting("Instant start", CheatToggles.autoHostInstant, v => CheatToggles.autoHostInstant = v);
            BoolSetting("Return to lobby after match", CheatToggles.autoHostReturn, v => CheatToggles.autoHostReturn = v);
            BoolSetting("Wait for players to load", CheatToggles.autoHostWaitLoad, v => CheatToggles.autoHostWaitLoad = v);
            BoolSetting("Force in last minute", CheatToggles.autoHostForceLastMinute, v => CheatToggles.autoHostForceLastMinute = v);
            BoolSetting("Cancel countdown below minimum", CheatToggles.autoHostCancelBelowMin, v => CheatToggles.autoHostCancelBelowMin = v);
            BoolSetting("Auto-host notifications", CheatToggles.autoHostNotify, v => CheatToggles.autoHostNotify = v);
            IntSetting("Min Players", CheatToggles.autoHostMinPlayers, 1, 15, v => CheatToggles.autoHostMinPlayers = v);
            IntSetting("Start Delay, s", CheatToggles.autoHostStartDelay, 0, 180, v => CheatToggles.autoHostStartDelay = v);
            IntSetting("Fast Start Players", CheatToggles.autoHostFastStartPlayers, 0, 15, v => CheatToggles.autoHostFastStartPlayers = v);
            IntSetting("Fast Start Delay, s", CheatToggles.autoHostFastStartDelay, 0, 60, v => CheatToggles.autoHostFastStartDelay = v);
            IntSetting("Warmup, s", CheatToggles.autoHostWarmup, 0, 120, v => CheatToggles.autoHostWarmup = v);
            IntSetting("Load Grace, s", CheatToggles.autoHostLoadGrace, 0, 90, v => CheatToggles.autoHostLoadGrace = v);
            IntSetting("Force After, min", CheatToggles.autoHostForceAfterMinutes, 0, 10, v => CheatToggles.autoHostForceAfterMinutes = v);
            IntSetting("Force Min Players", CheatToggles.autoHostForceMinPlayers, 1, 15, v => CheatToggles.autoHostForceMinPlayers = v);
            IntSetting("Backoff, s", CheatToggles.autoHostBackoff, 2, 60, v => CheatToggles.autoHostBackoff = v);
            GUILayout.Label($"Status: {Cheats.AutoHost.StateName}");
        }
        catch (System.Exception ex) { ErrorReporter.Report(ex, HandlingId, "HostOnlyTab2.DrawAutoHost: draw auto-host controls"); }
    }

    private TextField _lobbySearchField;

    private void DrawLobbyBrowser()
    {
        try
        {
            GUILayout.Label("Lobby Browser:");
            BoolSetting("Rich lobby browser (24 rows)", CheatToggles.richLobbyRows, v => CheatToggles.richLobbyRows = v);
            BoolSetting("Mute lobby music", CheatToggles.muteLobbyMusic, v => CheatToggles.muteLobbyMusic = v);
            GUILayout.Label("Filter lobbies by host name:");
            if (_lobbySearchField == null) _lobbySearchField = new TextField(Cheats.LobbyBrowser.SearchHost);
            _lobbySearchField.Draw(140);
            Cheats.LobbyBrowser.SearchHost = _lobbySearchField.Content ?? "";
            if (GUILayout.Button("Clear filter"))
            {
                Cheats.LobbyBrowser.SearchHost = "";
                _lobbySearchField = new TextField("");
            }
        }
        catch (System.Exception ex) { ErrorReporter.Report(ex, HandlingId, "HostOnlyTab2.DrawLobbyBrowser: draw lobby browser controls"); }
    }

    private static void IntSetting(string label, int current, int min, int max, System.Action<int> set)


    {
        GUILayout.BeginHorizontal();
        GUILayout.Label($"{label}: {current}", GUILayout.Width(200));
        int v = Mathf.RoundToInt(GUILayout.HorizontalSlider(current, min, max));
        GUILayout.EndHorizontal();
        if (v != current) set(v);
    }

    private static void FloatSetting(string label, float current, float min, float max, System.Action<float> set, string format)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label($"{label}: {current.ToString(format)}", GUILayout.Width(200));
        float v = GUILayout.HorizontalSlider(current, min, max);
        GUILayout.EndHorizontal();
        if (!Mathf.Approximately(v, current)) set(v);
    }

    private static void BoolSetting(string label, bool current, System.Action<bool> set)
    {
        bool v = GUILayout.Toggle(current, " " + label);
        if (v != current) set(v);
    }
    private static IEnumerator SpawnMap(byte mapId)
    {
        MalumMenu.Log.LogInfo($"Attempting to spawn in map id {mapId}");

        AsyncOperationHandle<GameObject> asyncHandle = AmongUsClient.Instance.ShipPrefabs[mapId].InstantiateAsync(null, false);
        yield return asyncHandle;

        ShipStatus ship = asyncHandle.Result.GetComponent<ShipStatus>();
        AmongUsClient.Instance.Spawn(ship, -2, SpawnFlags.None);

        MalumMenu.notifications.Send("Map Spawner", $"{(MapNames)mapId} has been spawned.", 5);
    }
}