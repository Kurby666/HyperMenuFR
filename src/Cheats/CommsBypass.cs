using System;
using HarmonyLib;

namespace MalumMenu.Cheats
{
	// Bypass comms sabotage: tasks and role abilities keep working locally
	// while communications are sabotaged.
	// Ported from othermenu Cheats/HyperComms.cs.
	internal static class CommsBypass
	{
		private const int HandlingId = 20032;

		[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.AreCommsAffected))]
		internal static class CommsTaskPatch
		{
			static void Postfix(PlayerControl __instance, ref bool __result)
			{
				try
				{
					if(!__result || !CheatToggles.commsBypass)
						return;
					if(__instance != PlayerControl.LocalPlayer)
						return;
					__result = false;
				}
				catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "CommsTaskPatch.Postfix: bypassing comms for tasks"); }
			}
		}

		[HarmonyPatch(typeof(RoleBehaviour), nameof(RoleBehaviour.CommsSabotaged), MethodType.Getter)]
		internal static class CommsRolePatch
		{
			static void Postfix(RoleBehaviour __instance, ref bool __result)
			{
				try
				{
					if(!__result || !CheatToggles.commsBypass) return;
					if(__instance == null || __instance.Player != PlayerControl.LocalPlayer) return;
					__result = false;
				}
				catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "CommsRolePatch.Postfix: bypassing comms for abilities"); }
			}
		}
	}
}
