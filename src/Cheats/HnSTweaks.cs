using System;
using AmongUs.GameOptions;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes;
using UnityEngine;

namespace MalumMenu.Cheats;

// Ported from othermenu/Patches/HnSPatches.cs (HnSSeekers) and the FourImpostors branch of
// othermenu/Patches/HyperHostOptions.cs
// Host-only: custom seeker count in Hide and Seek, no seeker head start, and 4 impostors in normal play.
internal static class HnSTweaks
{
    internal const int HandlingId = 20067;

    private static bool _defaultApplied;

    private static bool Enabled => CheatToggles.customSeekers;

    private static int Count
    {
        get
        {
            int c = CheatToggles.seekerCount;
            return c < 1 ? 1 : (c > 15 ? 15 : c);
        }
    }

    // Pins the impostor-count option to 1..Count so the vanilla +/- buttons step inside the seeker range.
    // The vanilla Increase/Decrease already clamp to ValidRange, so no separate step patch is needed.
    internal static void RelaxRange(NumberOption option)
    {
        if (!Enabled || !IsHnS() || !IsImpOption(option))
        {
            _defaultApplied = false;
            return;
        }

        option.ValidRange = new FloatRange(1f, Count);
        if (!_defaultApplied)
        {
            option.Value = Count;
            _defaultApplied = true;
        }
        else
        {
            option.Value = Clamp(option.Value, 1f, Count);
        }
        RelaxData(option);
    }

    private static void RelaxData(NumberOption option)
    {
        try
        {
            var data = ((OptionBehaviour)option).Data;
            if (data == null) return;
            FloatGameSetting f = ((Il2CppObjectBase)(object)data).TryCast<FloatGameSetting>();
            if (f != null)
            {
                f.ValidRange = new FloatRange(1f, Count);
                return;
            }
            IntGameSetting i = ((Il2CppObjectBase)(object)data).TryCast<IntGameSetting>();
            if (i != null)
                i.ValidRange = new IntRange(1, Count);
        }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "HnSTweaks.RelaxData: relax seeker count data range"); }
    }

    internal static bool TryImpostorCount(ref int count)
    {
        if (Enabled && IsHnS())
        {
            int players = CountAlive();
            count = players <= 1 ? 1 : Math.Min(Count, players - 1);
            return true;
        }

        if (CheatToggles.fourImpostors && !IsHnS() && CountPlayers() >= 9)
        {
            count = 4;
            return true;
        }

        return false;
    }

    internal static bool TryAssignSeekers(Il2CppSystem.Collections.Generic.List<NetworkedPlayerInfo> players, IGameOptions opts, RoleTeamTypes team, ref int teamMax)
    {
        if (!Enabled || !IsHnS() || !AmongUsClient.Instance.AmHost || (int)team != 1)
            return false;

        try
        {
            if (players == null || players.Count <= 0) return true;

            int seekerCount = players.Count <= 1 ? 1 : Math.Min(Count, players.Count - 1);
            teamMax = seekerCount;
            LobbySettings.SetImps(seekerCount);

            int assigned = 0;
            while (assigned < seekerCount && players.Count > 0)
            {
                int index = UnityEngine.Random.Range(0, players.Count);
                NetworkedPlayerInfo info = players[index];
                if (info != null && info.Object != null)
                {
                    info.Object.RpcSetRole(RoleTypes.Shapeshifter, true);
                    assigned++;
                }

                players.RemoveAt(index);
            }

            return true;
        }
        catch (Exception ex)
        {
            ErrorReporter.Report(ex, HandlingId, "HnSTweaks.TryAssignSeekers: assign seekers");
            return false;
        }
    }

    private static int CountAlive()
    {
        int count = 0;
        try
        {
            var cursor = PlayerControl.AllPlayerControls.GetEnumerator();
            while (cursor.MoveNext())
            {
                PlayerControl p = cursor.Current;
                if (p != null && p.Data != null && !p.Data.Disconnected && !p.Data.IsDead) count++;
            }
        }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "HnSTweaks.CountAlive: count alive players"); }
        return count;
    }

    private static int CountPlayers()
    {
        int n = 0;
        try
        {
            var cur = PlayerControl.AllPlayerControls.GetEnumerator();
            while (cur.MoveNext())
            {
                PlayerControl p = cur.Current;
                if (p != null && p.Data != null && !p.Data.Disconnected)
                    n++;
            }
        }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "HnSTweaks.CountPlayers: count players"); }
        return n;
    }

    internal static bool IsHnS()
    {
        try
        {
            if (GameManager.Instance != null && GameManager.Instance.IsHideAndSeek()) return true;
            var mgr = GameOptionsManager.Instance;
            if (mgr == null || mgr.CurrentGameOptions == null)
                return false;
            int mode = (int)mgr.CurrentGameOptions.GameMode;
            // HideNSeek = 2, the alternate hide and seek mode = 4
            return mode == 2 || mode == 4;
        }
        catch (Exception ex)
        {
            ErrorReporter.Report(ex, HandlingId, "HnSTweaks.IsHnS: detect hide and seek");
            return false;
        }
    }

    private static bool IsImpOption(NumberOption option) => option != null && option.Title == StringNames.GameNumImpostors;

    private static float Clamp(float v, float min, float max) => v < min ? min : (v > max ? max : v);

    private static void Refresh(NumberOption option)
    {
        option.UpdateValue();
        ((OptionBehaviour)option).OnValueChanged.Invoke((OptionBehaviour)(object)option);
        option.AdjustButtonsActiveState();
    }
}

[HarmonyPatch(typeof(NumberOption), nameof(NumberOption.Initialize))]
[HarmonyPriority(Priority.Last)]
internal static class HnSNumberInitPatch
{
    public static void Postfix(NumberOption __instance)
    {
        HnSTweaks.RelaxRange(__instance);
    }
}

[HarmonyPatch(typeof(NumberOption), nameof(NumberOption.AdjustButtonsActiveState))]
[HarmonyPriority(Priority.Last)]
internal static class HnSNumberAdjustPatch
{
    public static void Prefix(NumberOption __instance)
    {
        HnSTweaks.RelaxRange(__instance);
    }
}

[HarmonyPatch(typeof(IGameOptionsExtensions), nameof(IGameOptionsExtensions.GetAdjustedNumImpostors))]
[HarmonyPriority(Priority.Last)]
internal static class HnSImpostorCountPatch
{
    public static bool Prefix(ref int __result)
    {
        try
        {
            if (GameManager.Instance == null)
                return true;
            return !HnSTweaks.TryImpostorCount(ref __result);
        }
        catch (Exception ex) { ErrorReporter.Report(ex, HnSTweaks.HandlingId, "HnSImpostorCountPatch.Prefix: adjust impostor count"); return true; }
    }
}

[HarmonyPatch(typeof(LogicRoleSelectionHnS), nameof(LogicRoleSelectionHnS.AssignRolesForTeam))]
internal static class HnSRoleSelectionPatch
{
    public static bool Prefix(Il2CppSystem.Collections.Generic.List<NetworkedPlayerInfo> players, IGameOptions opts, RoleTeamTypes team, ref int teamMax)
    {
        return !HnSTweaks.TryAssignSeekers(players, opts, team, ref teamMax);
    }
}

[HarmonyPatch(typeof(LogicOptionsHnS), nameof(LogicOptionsHnS.GetCrewmateLeadTime))]
internal static class HnSLeadTimePatch
{
    public static void Postfix(ref int __result)
    {
        try
        {
            if (CheatToggles.noSeekerHeadStart) __result = 0;
        }
        catch (Exception ex) { ErrorReporter.Report(ex, HnSTweaks.HandlingId, "HnSLeadTimePatch.Postfix: remove seeker head start"); }
    }
}
