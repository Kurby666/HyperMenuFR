using System;
using HarmonyLib;
using UnityEngine;

namespace MalumMenu.Cheats
{
	// Glide for others: suppress normal movement broadcast and send snap positions
	// instead, so other players see you glide with no walk animation.
	// (Local walk animation is already removed by the Moonwalk toggle.)
	// Ported from othermenu Cheats/GlideMovement.cs.
	internal static class GlideOthers
	{
		private const int HandlingId = 20027;
		private const float SendStep = 0.02f;
		private const float MinShift = 0.01f;

		private static bool _muteSnap;
		private static Vector2 _last;
		private static float _acc;

		internal static bool MuteSnap => _muteSnap;

		internal static bool On()
		{
			PlayerControl me = PlayerControl.LocalPlayer;
			return CheatToggles.glideForOthers
				&& me != null && me.Data != null && me.NetTransform != null;
		}

		internal static void Tick()
		{
			try
			{
				if(!On()) return;

				_acc += Time.deltaTime;
				if(_acc < SendStep) return;
				_acc = 0f;

				PlayerControl me = PlayerControl.LocalPlayer;
				Vector2 pos = ((Component)me).transform.position;
				if(Vector2.Distance(pos, _last) <= MinShift) return;

				_last = pos;
				_muteSnap = true;
				try
				{
					me.NetTransform.RpcSnapTo(pos);
				}
				catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "GlideOthers.Tick: sending glide snap"); }
				_muteSnap = false;
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "GlideOthers.Tick: ticking glide broadcast"); }
		}

		[HarmonyPatch(typeof(CustomNetworkTransform), nameof(CustomNetworkTransform.FixedUpdate))]
		internal static class GlideNetPatch
		{
			static bool Prefix(CustomNetworkTransform __instance)
			{
				try
				{
					if(__instance != null && __instance.AmOwner && CheatToggles.glideForOthers)
						return false;
					return true;
				}
				catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "GlideNetPatch.Prefix: suppressing movement broadcast"); return true; }
			}
		}

		[HarmonyPatch(typeof(CustomNetworkTransform), nameof(CustomNetworkTransform.SnapTo), new Type[] { typeof(Vector2), typeof(ushort) })]
		internal static class GlideSnapPatch
		{
			static bool Prefix(CustomNetworkTransform __instance, ushort __1)
			{
				try
				{
					if(MuteSnap && __instance != null && __instance.AmOwner)
					{
						__instance.lastSequenceId = __1;
						return false;
					}
					return true;
				}
				catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "GlideSnapPatch.Prefix: muting snap echo"); return true; }
			}
		}

		[HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
		internal static class GlideTickPatch
		{
			static void Postfix()
			{
				try
				{
					Tick();
				}
				catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "GlideTickPatch.Postfix: ticking glide broadcast"); }
			}
		}
	}
}
