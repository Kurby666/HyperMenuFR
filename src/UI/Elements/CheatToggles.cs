using System.Collections.Generic;
using System.IO;
using System.Reflection;
using AmongUs.GameOptions;
using UnityEngine;

namespace MalumMenu;

public struct CheatToggles
{
    // Movement
    public static bool noClip;
    public static bool teleportPlayer;
    public static bool teleportCursor;
    public static bool invertControls;

    // Roles
    public static bool setFakeRole;
    public static bool setFakeAlive;
    public static bool noKillCd;
    public static bool showTasksMenu;
    public static bool completeMyTasks;
    public static bool completeAllTasks;
    public static bool impostorTasks;
    public static bool killReach;
    public static bool killAnyone;
    public static bool endlessSsDuration;
    public static bool endlessBattery;
    public static bool endlessTracking;
    public static bool noTrackingCooldown;
    public static bool noTrackingDelay;
    public static bool trackReach;
    public static bool interrogateReach;
    public static bool commsBypass;
    public static bool noVitalsCooldown;
    public static bool noVentCooldown;
    public static bool endlessVentTime;
    public static bool endlessVanish;
    public static bool killVanished;
    public static bool noVanishAnim;
    public static bool noShapeshiftAnim;
    public static bool killAura;
    public static float killAuraDist = 2.5f;
    public static bool autoVentKill;
    public static bool bodyToVent;
    public static bool morphDead;
    public static bool endlessInvis;
    public static bool smokeSpam;
    public static bool judgeNoTasks;
    public static bool detNoCd;
    public static bool seeInVents;
    public static bool seeVanished;
    public static bool ventNetwork;

    // ESP
    public static bool noShadows;
    public static bool seeGhosts;
    public static bool showGhosts;
    public static bool seeRoles;
    public static bool seePlayerInfo;
    public static bool seeDisguises;
    public static bool taskArrows;
    public static bool revealVotes;
    public static bool seeLobbyInfo;
    public static bool fullbright;
    public static bool noSeekerAnimation;

    // Camera
    public static bool spectate;
    public static bool zoomOut;
    public static bool freecam;

    // Minimap
    public static bool mapCrew;
    public static bool mapImps;
    public static bool mapGhosts;
    public static bool colorBasedMap;

    // Tracers
    public static bool tracersImps;
    public static bool tracersCrew;
    public static bool tracersGhosts;
    public static bool tracersBodies;
    public static bool colorBasedTracers;
    public static bool distanceBasedTracers;

    // Radar
    public static bool showRadar;
    public static bool radarCrew = true;
    public static bool radarImps = true;
    public static bool radarGhosts = true;
    public static bool radarBodies = true;
    public static int radarRangeIdx = 1;
    public static float radarSize = 220f;
    public static float radarOpacity = 0.85f;
    public static float radarX = 20f;
    public static float radarY = 100f;

    // Chat
    public static bool changeChatColor;
    public static bool colorAsPlayer;
    public static bool enableChat;
    public static bool unlockCharacters;
    public static bool bypassUrlBlock;
    public static bool longerMessages;
    public static bool unlockClipboard;
    public static bool lowerRateLimits;
    public static bool showGhostsChat;
    public static bool alwaysVisibleChat;
    public static bool overheadChat;
    public static float overheadChatTime = 6f;
    public static int overheadChatWhere;
    public static bool chatSpam;
    public static float chatSpamDelay = 2f;
    public static bool chatCmds = true;
    public static bool colorCmd = true;
    public static bool colorCmdNotify = true;
    public static bool nameHistory = true;
    public static bool notifyKnown = true;

    // Ship
    public static bool closeMeeting;
    public static bool autoOpenDoorsOnUse;
    public static bool unfixableLights;
    public static bool callMeeting;
    public static bool spamMeetings;
    public static bool reportBody;
    public static bool autoReportBodies;
    public static bool kickOffensiveNames;
    public static bool fakeTasks;
    public static bool doAnyTask;
    public static bool votekickAutoRejoin;
    public static bool votekickProtect;
    public static int votekickPunishIdx = 2;
    public static bool votekickCopyCode;
    public static bool voteSpam;
    public static bool cameraJam;
    public static bool autoReturnLobby;
    public static bool enableDummies;
    public static bool dummyDoTasks = true;
    public static bool dummyFixSabotage = true;
    public static bool dummyReportBodies = true;
    public static bool cloneMode;
    public static bool cloneShadow;
    public static bool cloneGuard = true;
    public static float cloneGuardRadius = 2.5f;
    public static bool cloneDrift;
    public static int cloneMax = 50;
    public static int clonePerClick = 1;
    public static int cloneColorId = -1;
    public static float cloneScale = 1f;
    public static int cloneFormation = 4;
    public static float cloneFormationScale = 1f;
    public static int cloneFormationCopies = 1;
    public static bool cloneAnim = true;
    public static float cloneAnimSpeed = 1f;
    public static bool cloneNaked;
    public static bool netCloneMode;
    public static int netCloneCount = 6;

    // Self
    public static bool invisible;
    public static bool invisiblePoof = true;
    public static bool mirage;
    public static bool mirageFreeze;
    public static bool mirageFlicker = true;
    public static int mirageFlickerMin = 2;
    public static int mirageFlickerMax = 4;
    public static bool mouseSelect;
    public static bool selfDrag;
    public static bool selfDragSmooth = true;
    public static float selfDragSpeed = 3f;
    public static bool ghostAfterStart;
    public static bool worldTilt;
    public static float worldTiltAngle = 90f;
    public static bool neonOutline;
    public static int neonMode;
    public static bool autoTasks;
    public static float autoTasksDelay = 1.5f;
    public static bool outfitResetMatch;
    public static bool outfitResetLobby;
    public static int outfitFavSlot;
    public static bool antWalk;
    public static float antWalkStep = 0.35f;
    public static float antWalkTwitch = 0.06f;
    public static bool glideForOthers;
    public static bool platformUnlock;
    public static bool snipeColor;
    public static int snipeColorId;
    public static bool colorAll;
    public static int colorAllId;
    public static bool colorReservations;
    public static bool nameColor;
    public static int nameColorStyle;
    public static bool nameColorAnimated = true;

    // Start controls and hide and seek
    public static bool unlockStartButton;
    public static bool startOnEnter;
    public static bool instantStart;
    public static bool customSeekers;
    public static int seekerCount = 2;
    public static bool noSeekerHeadStart;
    public static bool fourImpostors;

    // Auto-host
    public static bool autoHost;
    public static int autoHostMinPlayers = 4;
    public static int autoHostStartDelay = 15;
    public static int autoHostBackoff = 8;
    public static bool autoHostInstant = true;
    public static int autoHostWarmup = 5;
    public static int autoHostLoadGrace = 20;
    public static int autoHostFastStartPlayers = 13;
    public static int autoHostFastStartDelay = 5;
    public static int autoHostForceAfterMinutes;
    public static int autoHostForceMinPlayers = 2;
    public static bool autoHostCancelBelowMin = true;
    public static bool autoHostWaitLoad = true;
    public static bool autoHostReturn = true;
    public static bool autoHostForceLastMinute = true;
    public static bool autoHostNotify = true;

    // Lobby browser and music
    public static bool richLobbyRows;
    public static bool muteLobbyMusic;

    // Access lists and join gate
    public static bool accessBanEnabled;
    public static bool accessWhitelistOnly;
    public static bool accessNickBanEnabled;
    public static bool accessPlatformBanEnabled;
    public static bool minLevelEnabled;
    public static int minLevel = 1;
    public static bool maxLevelEnabled;
    public static int maxLevel = 5000;
    public static int levelActionIdx = 2;
    public static bool kickFortegreen;

    // Join intel
    public static bool joinIntel;
    public static bool joinIntelToasts = true;

    // Event notifications and log
    public static bool mirrorEventsToConsole = true;
    public static bool notifyKills;
    public static bool notifyMeetings;
    public static bool notifySabotage;
    public static bool notifyVents;
    public static bool notifyRoles;
    public static bool notifyReports;
    public static bool notifyEjects;
    public static bool notifyVotekicks;
    public static bool showEventLog;

    // Match replay
    public static bool showReplay;
    public static bool replayPlayback;
    public static bool replayClearAfterMeeting;

    // Guards
    public static bool modDetectToast;
    public static bool rpcGuard;
    public static bool rpcGuardToast = true;
    public static int rpcGuardActionIdx = 2;
    public static bool antiBanHost;
    public static bool blockForcedVents;
    public static bool blockForcedZipline;
    public static bool blockFakeMeetings;
    public static bool blockSpawnFloods;
    public static bool securityNotify = true;

	// Body mode, chat extras, ban words, xmas, friend code spoof
	public static int bodyModeIdx = 0;
	public static bool noChatCooldown = false;
	public static bool chatTimestamps = false;
	public static bool chatSenderInfo = true;
	public static bool darkChatTheme = true;
	public static int chatHistorySize = 32;
	public static bool banWords = false;
	public static bool xmasHostCommand = true;
	public static bool fcSpoofEnabled = false;
	public static bool autoReportErrors = true;

	// Status HUD
	public static bool gradientStamp = false;
	public static bool showFps;
	public static bool showLobbyTimer;
	public static bool showHostLine;
	public static bool radialMenu;

	// Glich Rooms bot
	public static bool glichCycle;
	public static bool glichHunt;
	public static bool glichLog;
	public static float glichDelay = 3f;

	// Master switch for BOTH hotkey systems: the per-cheat Keybinds dictionary polled by
	// Components/KeybindListener.cs and the 37 action hotkeys dispatched by Cheats/Hotkeys.cs
	// (whose radial menu routes through the same Down/Held guards). Default is on so existing
	// profiles behave exactly as before.
	public static bool hotkeysEnabled = true;

    // Sabotage
    public static bool commsSab;
    public static bool elecSab;
    public static bool reactorSab;
    public static bool oxygenSab;
    public static bool mushSab;
    public static bool mushSpore;
    public static bool spamMainSab;
    public static bool keepLightsOff;
    public static bool infMushroom;
    public static bool multiSabotage;
    public static bool autoFixSabotage;
    public static bool consoleReach;
    public static float consoleDist = 4f;
    public static bool skipDecon;
    public static bool airshipSpawn;
    public static int airshipSpawnId;
    public static bool taskDrain;
    public static float taskDrainStep = 0.25f;
    public static int frameSystemIdx;
    public static int frameValue = 128;
    public static bool showDoorsMenu;
    public static bool openAllDoors;
    public static bool closeAllDoors;
    public static bool spamOpenAllDoors;
    public static bool spamCloseAllDoors;
    public static bool sabotageMap;

    // Vents
    public static bool unlockVents;
    public static bool walkInVents;
    public static bool kickVents;
    public static bool ventTpAuto;
    public static float ventTpAutoDelay = 2f;
    public static int ventTpMode;
    public static bool impTrap;

    // Animations
    public static bool animShields;
    public static bool animAsteroids;
    public static bool animEmptyGarbage;
    public static bool animMedScan;
    public static bool animCamsInUse;
    public static bool animPet;
    public static bool moonWalk;

    // Overload
    public static bool showOverload;
    public static bool showOverloadSettings;
    public static bool olAutoStart;
    public static bool olAutoAdapt;
    public static bool olShowRpcTotal;
    public static bool olAutoStop;
    public static bool olLockTargets;
    public static bool olKillSwitch;
    public static bool olPlayerCooldown;
    public static bool olAutoClear;
    public static bool olLogStartStop;
    public static bool olLogAddRemove;
    public static bool olLogDisconnect;
    public static bool olLogAttack;
    public static bool olVerboseLogs;
    public static bool runOverload;
    public static bool overloadAll;
    public static bool overloadHost;
    public static bool overloadCrew;
    public static bool overloadImps;
    public static bool overloadReset;

    // Console
    public static bool showConsole;
    public static bool logDeaths;
    public static bool logShapeshifts;
    public static bool logVents;
    public static bool logTasks;
    public static bool logGameState;
    public static bool judgeWatch;

    // Host-Only
    public static bool voteImmune;
    public static bool forceRole;
    public static RoleTypes? forcedRole;
    public static bool showRolesMenu;
    public static bool skipMeeting;
    public static bool forceStartGame;
    public static bool noGameEnd;
    public static bool showProtectMenu;
    public static bool noOptionsLimits;
    public static bool ejectPlayer;
    public static bool killPlayer;
    public static bool telekillPlayer;
    public static bool killAll;
    public static bool killAllCrew;
    public static bool killAllImps;
    public static bool bypassHostOnly;

    // Passive
    public static bool antiOverload;
    public static bool unlockFeatures;
    public static bool freeCosmetics;
    public static bool avoidPenalties;
    public static bool copyLobbyCodeOnDisconnect;
    public static bool spoofAprilFoolsDate;
    public static bool randomizeCosmetics;

    // Modes
    public static bool rgbMode;
    public static bool stealthMode;
    public static bool panicMode;
    public static bool streamerMode;

    // Config
    public static bool reloadConfig;
    public static bool openConfig;
    public static bool loadProfile;
    public static bool saveProfile;
    public static bool spoofLevel;

    // Keybind Map: Toggle Name -> KeyCode (KeyCode.None == No Key)
    public static readonly Dictionary<string, KeyCode> Keybinds = new();

    // Map for Reflection Access: Toggle Name -> FieldInfo
    public static readonly Dictionary<string, FieldInfo> ToggleFields = new();

    // Populate reflection map once at startup and initialize Keybinds with KeyCode.None
    static CheatToggles()
    {
        var fields = typeof(CheatToggles).GetFields(BindingFlags.Static | BindingFlags.Public);

        foreach (var field in fields)
        {
            if (field.FieldType != typeof(bool)) continue;

            ToggleFields[field.Name] = field;
            Keybinds[field.Name] = KeyCode.None;
        }
    }

    public static void DisablePPMCheats(string variableToKeep)
    {
        ejectPlayer = variableToKeep == "ejectPlayer" && ejectPlayer;
        reportBody = variableToKeep == "reportBody" && reportBody;
        killPlayer = variableToKeep == "killPlayer" && killPlayer;
        telekillPlayer = variableToKeep == "telekillPlayer" && telekillPlayer;
        spectate = variableToKeep == "spectate" && spectate;
        setFakeRole = variableToKeep == "setFakeRole" && setFakeRole;
        setFakeAlive = variableToKeep == "setFakeAlive" && setFakeAlive;
        forceRole = variableToKeep == "forceRole" && forceRole;
        teleportPlayer = variableToKeep == "teleportPlayer" && teleportPlayer;
    }

    public static bool ShouldPPMClose()
    {
        return !setFakeRole && !setFakeAlive && !forceRole && !ejectPlayer && !reportBody && !telekillPlayer && !killPlayer && !spectate && !teleportPlayer;
    }

    // Disables all cheat toggles by setting all to false using the cached ToggleFields
    public static void DisableAll()
    {
        foreach (var field in ToggleFields.Values)
        {
            field.SetValue(null, false);
        }
    }

    // Saves cheat toggles and their keybinds to MalumProfile.txt
    // Format per line: ToggleName = True/False = KeyCode.KEY
    public static void SaveTogglesToProfile()
    {
        using var writer = new StreamWriter(MalumMenu.ProfilePath);

        writer.WriteLine("# MalumProfile");
        writer.WriteLine("# Format: ToggleName = True/False = KeyCode.KEY");
        writer.WriteLine("# - List of supported keycodes: https://docs.unity3d.com/Packages/com.unity.tiny@0.16/api/Unity.Tiny.Input.KeyCode.html");
        writer.WriteLine("# - Setting a keybind is optional. Use KeyCode.None to not set a keybind");
        writer.WriteLine("# - Multiple toggles may have the same key, but multiple keys per toggle are NOT supported");
        writer.WriteLine("# - Keybinds are only applied after loading this profile by pressing 'Load from Profile' in the Config menu");
        writer.WriteLine();

        foreach (var field in ToggleFields.Values)
        {
            Keybinds.TryGetValue(field.Name, out var key);  // If no key is set then write KeyCode.None
            writer.WriteLine($"{field.Name} = {field.GetValue(null)} = KeyCode.{key}");
        }
    }

    // Loads cheat toggles and their keybinds from MalumProfile.txt if the file is present
    // Format per line: ToggleName = True/False = KeyCode.KEY
    public static void LoadTogglesFromProfile()
    {
        if (!File.Exists(MalumMenu.ProfilePath)) return;

        using var reader = new StreamReader(MalumMenu.ProfilePath);

        while (reader.ReadLine() is { } line)
        {
            // Skips empty lines
            if (string.IsNullOrWhiteSpace(line)) continue;

            // Skips lines that are commented out
            line = line.Trim();
            if (line.StartsWith("#")) continue;

            // Extracts the three relevant config values for each remaining line
            var parts = line.Split('=', 3);
            if (parts.Length < 2) continue;

            // Gets the cheat's FieldInfo from its name
            var name = parts[0].Trim();
            if (!ToggleFields.TryGetValue(name, out var field)) continue;

            // Loads whether the cheat is enabled or disabled by default
            if (bool.TryParse(parts[1].Trim(), out var boolVal))
            {
                field.SetValue(null, boolVal);
            }

            // Loads the keybind associated with each cheat
            KeyCode key = KeyCode.None;
            if (parts.Length >= 3)
            {
                var keyPart = parts[2].Trim();
                if (keyPart.StartsWith("KeyCode."))
                {
                    keyPart = keyPart["KeyCode.".Length..];
                }

                if (!string.IsNullOrEmpty(keyPart) && System.Enum.TryParse<KeyCode>(keyPart, true, out var parsed))
                {
                    key = parsed;
                }
            }

            Keybinds[name] = key;
        }
    }
}
