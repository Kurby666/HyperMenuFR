using System;
using HarmonyLib;
using UnityEngine;

namespace MalumMenu.Cheats
{
	// Port of othermenu Mirage/LagComp (HyperLagComp.cs): manipulate the
	// position others see while you move normally locally.
	// Freeze frame = never send movement updates (appear stationary to others).
	// Flicker = send position in bursts separated by random frame gaps.
	internal static class Mirage
	{
		private const int HandlingId = 20022;

		private static int _hold;

		[HarmonyPatch(typeof(CustomNetworkTransform), nameof(CustomNetworkTransform.Serialize))]
		private static class Suppress
		{
			static bool Prefix(CustomNetworkTransform __instance, bool __1, ref bool __result)
			{
				try
				{
					if(!CheatToggles.mirage || __1) return true;
					if(__instance == null || __instance.myPlayer != PlayerControl.LocalPlayer) return true;
					if(MeetingHud.Instance != null) return true;

					if(CheatToggles.mirageFreeze)
					{
						__result = false;
						return false;
					}

					if(CheatToggles.mirageFlicker)
					{
						if(_hold > 0)
						{
							_hold--;
							__result = false;
							return false;
						}
						int lo = Mathf.Clamp(CheatToggles.mirageFlickerMin, 1, 30);
						int hi = Mathf.Clamp(CheatToggles.mirageFlickerMax, lo, 30);
						_hold = UnityEngine.Random.Range(lo, hi + 1);
					}
					return true;
				}
				catch(Exception ex)
				{
					ErrorReporter.Report(ex, HandlingId, "Mirage.Suppress.Prefix: filtering movement updates");
					return true;
				}
			}
		}
	}
}
