using System.Collections.Generic;

namespace MalumMenu;

/// <summary>
/// Registry of 5-digit handling IDs, one per mod file.
/// Each file that reports errors declares its own
/// <c>private const int HandlingId = XXXXX;</c> using the ID assigned here.
/// IDs must be unique and in the range 10000-99999.
/// <c>00000</c> is reserved for unhandled/global errors (see ErrorReporter.GlobalHandlingId).
///
/// Ranges:
///   10000-10999  core (MalumMenu.cs, Utils, UI shell, Console)
///   20000-20999  Cheats/
///   30000-30999  Patches/
///   40000-40999  features/
///   50000-50999  anticheat/
///   60000-69999  routines/, Components/, Utilities/, misc (Teleporter, Sabotage, ...)
///
/// When adding a new file, pick the next free ID in its folder's range and add a row below.
/// </summary>
public static class HandlingIds
{
    private static readonly Dictionary<int, string> _registry = new()
    {
        // core
        { 10001, "MalumMenu.cs" },
        { 10002, "Utilities/Utils.cs" },
        { 10003, "UI/Windows/ConsoleUI.cs" },
        { 10004, "UI/Windows/MenuUI.cs" },
        { 10005, "UI/NotificationManager.cs" },
        { 10006, "UI/Windows/RolesUI.cs" },
        { 10007, "UI/Windows/ProtectUI.cs" },
        { 10008, "UI/Windows/OverloadUI.cs" },
        { 10009, "UI/Windows/DoorsUI.cs" },
        { 10010, "UI/Windows/TasksUI.cs" },
        { 10011, "UI/Windows/StreamerUI.cs" },
        { 10012, "UpdateCheck.cs" },
        // Cheats/
        { 20001, "Cheats/MalumRandomizer.cs" },
        { 20002, "Cheats/MalumCheats.cs" },
        { 20003, "Cheats/EndermanKill.cs" },
        { 20004, "Cheats/MeetingTools.cs" },
        { 20005, "Cheats/VentKick.cs" },
        { 20016, "Cheats/VotekickTools.cs" },
        { 20017, "Cheats/JudgeWatcher.cs" },
        { 20018, "Cheats/BlindTools.cs" },
        { 20019, "Cheats/VentTpTools.cs" },
        { 20020, "Cheats/GodMode.cs" },
        { 20021, "Cheats/Invisibility.cs" },
        { 20022, "Cheats/Mirage.cs" },
        { 20023, "Cheats/MouseTools.cs" },
        { 20024, "Cheats/GhostTools.cs" },
        { 20025, "Cheats/AntWalk.cs" },
        { 20026, "Cheats/AnimLoops.cs" },
        { 20027, "Cheats/GlideOthers.cs" },
        { 20034, "Cheats/AutoTasks.cs" },
        { 20040, "Cheats/RadarPanel.cs" },
        { 20041, "Cheats/OverheadChat.cs" },
        { 20042, "Cheats/NeonOutline.cs" },
        { 20043, "Cheats/WorldTilt.cs" },
        { 20044, "Cheats/CameraJammer.cs" },
        { 20045, "Cheats/VoteSpam.cs" },
        { 20050, "Cheats/VotekickGuard.cs" },
        { 20051, "Cheats/OutfitTools.cs" },
        { 20052, "Cheats/MorphTools.cs" },
        { 20053, "Cheats/NameHistory.cs" },
        { 20054, "Cheats/ChatTools.cs" },
        { 20055, "Cheats/LobbySettings.cs" },
        { 20056, "Cheats/ForceRoles.cs" },
        { 20057, "Cheats/LobbyPresets.cs" },
        { 20058, "Cheats/Dummies.cs" },
        { 20059, "Cheats/DummyNav.cs" },
        { 20061, "Cheats/DummyAI.cs" },
        { 20062, "Cheats/DummyChat.cs" },
        { 20060, "Cheats/LobbyTools.cs" },
        { 20063, "Cheats/LocalClones.cs" },
        { 20064, "Cheats/NetworkedClones.cs" },
        { 20065, "Cheats/LobbyPranks.cs" },
        { 20066, "Cheats/StartControls.cs" },
        { 20067, "Cheats/HnSTweaks.cs" },
        { 20068, "Cheats/AutoHost.cs" },
        { 20069, "Cheats/LobbyBrowser.cs" },
        { 20070, "Cheats/LobbyMusic.cs" },
        { 20071, "Cheats/AccessLists.cs" },
        { 20072, "Cheats/JoinIntel.cs" },
        { 20073, "Cheats/EventLog.cs" },
        { 20074, "Cheats/MatchReplay.cs" },
        { 20080, "Cheats/Extras.cs" },
        { 20081, "Cheats/Extras.cs" },
        { 20082, "Cheats/Extras.cs" },
        { 20083, "Cheats/Extras.cs" },
        { 20084, "Cheats/Extras.cs" },
        { 20075, "Cheats/Guards.cs" },
        { 20076, "Cheats/Guards.cs" },
        { 20077, "Cheats/Guards.cs" },
        { 20078, "Cheats/Guards.cs" },
        { 20079, "Cheats/Guards.cs" },
        { 20093, "Cheats/Hotkeys.cs" },
        { 20094, "Cheats/StatusHud.cs" },
        { 20095, "Cheats/RadialMenu.cs" },
        { 20096, "Cheats/MenuExtras.cs" },
        { 20097, "Cheats/GlichRooms.cs" },
        { 20046, "Cheats/SmokeSpam.cs" },
        { 20047, "Cheats/LeaveBody.cs" },
        { 20048, "Cheats/PetHand.cs" },
        { 20049, "Cheats/ColorTools.cs" },
        { 20035, "Cheats/CrewAssist.cs" },
        { 20036, "Cheats/RoleBuffs.cs" },
        { 20037, "Cheats/SeeThrough.cs" },
        { 20038, "Cheats/VentNetwork.cs" },
        { 20039, "Cheats/AutoVent.cs" },
        { 20028, "Cheats/ZiplineRide.cs" },
        { 20029, "Cheats/PlatformRide.cs" },
        { 20030, "Cheats/SabotageSpam.cs" },
        { 20031, "Cheats/FrameSabotage.cs" },
        { 20032, "Cheats/CommsBypass.cs" },
        { 20033, "Cheats/TaskDrain.cs" },
        { 20006, "Cheats/ArrowHandler.cs" },
        { 20007, "Cheats/DoorsHandler.cs" },
        { 20008, "Cheats/MalumESP.cs" },
        { 20009, "Cheats/MalumPPMCheats.cs" },
        { 20010, "Cheats/MalumSabotageCheats.cs" },
        { 20011, "Cheats/MalumSpoof.cs" },
        { 20012, "Cheats/MinimapHandler.cs" },
        { 20013, "Cheats/OffensiveNameKicker.cs" },
        { 20014, "Cheats/OverloadHandler.cs" },
        { 20015, "Cheats/TracersHandler.cs" },
        // Patches/ + Network.cs
        { 30001, "Patches/AmongUsClientPatches.cs" },
        { 30002, "Patches/AntiOverloadPatches.cs" },
        { 30003, "Patches/ChatControllerPatches.cs" },
        { 30004, "Patches/EOSManagerPatches.cs" },
        { 30005, "Patches/HudManagerPatches.cs" },
        { 30006, "Patches/LogicGameFlowPatches.cs" },
        { 30007, "Patches/LogicOptionsPatches.cs" },
        { 30008, "Patches/MapBehaviourPatches.cs" },
        { 30009, "Patches/MatchInfoGuidePatches.cs" },
        { 30010, "Patches/MeetingHudPatches.cs" },
        { 30011, "Patches/NormalPlayerTaskPatches.cs" },
        { 30012, "Patches/NumberOptionPatches.cs" },
        { 30013, "Patches/OtherPatches.cs" },
        { 30014, "Patches/PlayerControlPatches.cs" },
        { 30015, "Patches/PlayerPhysicsPatches.cs" },
        { 30016, "Patches/RoleBehaviourPatches.cs" },
        { 30017, "Patches/ShapeshifterMinigamePatches.cs" },
        { 30018, "Patches/ShipStatusPatches.cs" },
        { 30019, "Patches/TextBoxTMPPatches.cs" },
        { 30020, "Patches/VentPatches.cs" },
        { 30021, "Patches/VoteBanSystemPatches.cs" },
        { 30050, "Network.cs" },
        // features/
        { 40001, "features/Chat.cs" },
        { 40002, "features/Host.cs" },
        { 40003, "features/Immortality.cs" },
        { 40004, "features/PlayerLogger.cs" },
        { 40005, "features/Protections.cs" },
        { 40006, "features/Roles.cs" },
        { 40007, "features/Self.cs" },
        { 40008, "features/Spoofer.cs" },
        { 40009, "features/Troll.cs" },
        { 40010, "features/Visuals.cs" },
        // anticheat/
        { 50001, "anticheat/Anticheat.cs" },
        { 50002, "anticheat/GameDataCheck.cs" },
        { 50003, "anticheat/PlatformSpoofer.cs" },
        // UI tabs + routines + components
        { 60001, "UI/Windows/Tabs/MovementTab.cs" },
        { 60002, "UI/Windows/Tabs/AnticheatTab.cs" },
        { 60003, "UI/Windows/Tabs/AnimationsTab.cs" },
        { 60004, "UI/Windows/Tabs/ChatTab.cs" },
        { 60005, "UI/Windows/Tabs/ConfigTab.cs" },
        { 60006, "UI/Windows/Tabs/ConsoleTab.cs" },
        { 60007, "UI/Windows/Tabs/ESPTab.cs" },
        { 60008, "UI/Windows/Tabs/HostOnlyTab.cs" },
        { 60009, "UI/Windows/Tabs/HostOnlyTab2.cs" },
        { 60010, "UI/Windows/Tabs/ModesTab.cs" },
        { 60011, "UI/Windows/Tabs/OverloadTab.cs" },
        { 60012, "UI/Windows/Tabs/PassiveTab.cs" },
        { 60013, "UI/Windows/Tabs/PlayersTab.cs" },
        { 60014, "UI/Windows/Tabs/ProtectionsTab.cs" },
        { 60015, "UI/Windows/Tabs/RolesTab.cs" },
        { 60016, "UI/Windows/Tabs/SabotageTab.cs" },
        { 60017, "UI/Windows/Tabs/SelfTab.cs" },
        { 60018, "UI/Windows/Tabs/SettingsTab.cs" },
        { 60019, "UI/Windows/Tabs/ShipTab.cs" },
        { 60020, "UI/Windows/Tabs/TrollTab.cs" },
        { 60102, "routines/RoutineManager.cs" },
        { 60103, "routines/TeleportSpammer.cs" },
        { 60104, "routines/AutoTriggerSporesRoutine.cs" },
        { 60105, "routines/JailPlayerRoutine.cs" },
        { 60106, "routines/PetPlayer.cs" },
        { 60107, "routines/PlayerFollower.cs" },
        { 60108, "routines/FungleSporeTriggerRoutine.cs" },
        { 60109, "routines/DiscoHost.cs" },
        { 60110, "routines/DoorTroller.cs" },
        { 60111, "routines/ReportBodySpam.cs" },
        { 60201, "Components/KeybindListener.cs" },
    };

    /// <summary>Returns true if the ID is registered in the table above (or is the global 00000).</summary>
    public static bool IsValid(int id)
    {
        if (id == ErrorReporter.GlobalHandlingId) return true;
        lock (_registry) { return _registry.ContainsKey(id); }
    }

    /// <summary>Returns the file name registered for an ID, or null if unregistered.</summary>
    public static string FileFor(int id)
    {
        lock (_registry)
        {
            return _registry.TryGetValue(id, out var file) ? file : null;
        }
    }
}
