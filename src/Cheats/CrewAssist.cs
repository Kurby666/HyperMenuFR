using System;
using HarmonyLib;
using UnityEngine;

namespace MalumMenu.Cheats
{
	// Crew-side helpers: use consoles from afar, skip decon waits, pick Airship spawn.
	// Ported from othermenu Cheats/NocturneAssist.cs.
	internal static class CrewAssist
	{
		private const int HandlingId = 20035;

		[HarmonyPatch(typeof(global::Console), nameof(global::Console.CanUse))]
		internal static class ConsoleReachPatch
		{
			static void Postfix(global::Console __instance, NetworkedPlayerInfo pc, ref bool canUse, ref bool couldUse, ref float __result)
			{
				try
				{
					if(!CheatToggles.consoleReach || __instance == null || !couldUse || canUse)
						return;
					PlayerControl me = PlayerControl.LocalPlayer;
					if(me == null || pc == null || pc.PlayerId != me.PlayerId) return;

					float d = Vector2.Distance(me.GetTruePosition(), (Vector2)__instance.transform.position);
					if(d > CheatToggles.consoleDist) return;
					canUse = true;
					__result = d;
				}
				catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "ConsoleReachPatch.Postfix: extending console reach"); }
			}
		}

		[HarmonyPatch(typeof(DeconSystem), nameof(DeconSystem.Deteriorate))]
		internal static class SkipDeconPatch
		{
			static void Postfix(DeconSystem __instance)
			{
				try
				{
					if(!CheatToggles.skipDecon || __instance == null)
						return;
					try
					{
						if(__instance.UpperDoor != null) __instance.UpperDoor.SetDoorway(true);
					}
					catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "SkipDeconPatch.Postfix: opening upper decon door"); }
					try
					{
						if(__instance.LowerDoor != null)
							__instance.LowerDoor.SetDoorway(true);
					}
					catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "SkipDeconPatch.Postfix: opening lower decon door"); }
				}
				catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "SkipDeconPatch.Postfix: skipping decontamination"); }
			}
		}

		[HarmonyPatch(typeof(SpawnInMinigame), nameof(SpawnInMinigame.Begin))]
		internal static class AirshipSpawnPatch
		{
			static void Postfix(SpawnInMinigame __instance)
			{
				try
				{
					if(!CheatToggles.airshipSpawn || __instance == null) return;
					var locs = __instance.Locations;
					if(locs == null || locs.Length == 0)
						return;
					int i = Mathf.Clamp(CheatToggles.airshipSpawnId, 0, locs.Length - 1);
					__instance.SpawnAt(locs[i]);
				}
				catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "AirshipSpawnPatch.Postfix: picking airship spawn"); }
			}
		}
	}
}
