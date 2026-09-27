using System;
using HarmonyLib;
using UnityEngine;

namespace MalumMenu.Cheats
{
	// Auto-vent after kill + body-to-vent (host): teleport the victim into the
	// nearest vent before the kill lands so no body is left behind.
	// Ported from othermenu Cheats/HyperAutoVent.cs.
	internal static class AutoVent
	{
		private const int HandlingId = 20039;

		private static float _at = -1f;
		private static byte _pk = 255;
		private static float _pkAt;

		internal static bool On => CheatToggles.autoVentKill;
		internal static bool BodyOn => CheatToggles.bodyToVent;

		internal static void Arm()
		{
			if(On)
				_at = Time.time + 0.25f;
		}

		internal static bool KillToVent(PlayerControl killer, PlayerControl target)
		{
			try
			{
				if(!BodyOn || _pk != 255) return false;
				if(AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost)
					return false;
				if(killer == null || killer != PlayerControl.LocalPlayer) return false;
				if(target == null || target.Data == null || target.Data.IsDead) return false;
				if(ShipStatus.Instance == null || MeetingHud.Instance != null) return false;

				int idx = VentTpTools.NearestVentIndex(target.GetTruePosition());
				if(idx < 0) return false;

				try
				{
					VentTpTools.Send(target, idx);
				}
				catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "AutoVent.KillToVent: sending body to vent"); }
				_pk = target.PlayerId;
				_pkAt = Time.time + 0.4f;
				return true;
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "AutoVent.KillToVent: arming vent kill"); return false; }
		}

		internal static void Tick()
		{
			try
			{
				if(_pk != 255 && Time.time >= _pkAt)
				{
					byte id = _pk;
					_pk = 255;
					DoKill(id);
				}

				if(_at < 0f || Time.time < _at) return;
				_at = -1f;
				Escape();
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "AutoVent.Tick: ticking auto-vent"); }
		}

		private static PlayerControl ById(byte id)
		{
			foreach(PlayerControl pc in PlayerControl.AllPlayerControls)
			{
				if(pc != null && pc.PlayerId == id)
					return pc;
			}
			return null;
		}

		private static void DoKill(byte id)
		{
			try
			{
				PlayerControl me = PlayerControl.LocalPlayer;
				PlayerControl t = ById(id);
				if(me == null || t == null || t.Data == null || t.Data.IsDead)
					return;
				me.RpcMurderPlayer(t, true);
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "AutoVent.DoKill: delayed vent kill"); }
		}

		private static void Escape()
		{
			try
			{
				PlayerControl me = PlayerControl.LocalPlayer;
				if(me == null || me.Data == null || me.Data.IsDead || me.MyPhysics == null) return;
				if(ShipStatus.Instance == null || MeetingHud.Instance != null || me.inVent) return;

				int idx = VentTpTools.NearestVentIndex(me.GetTruePosition());
				var vents = ShipStatus.Instance.AllVents;
				if(idx < 0 || vents == null || idx >= vents.Count) return;
				Vent best = vents[idx];
				if(best == null) return;

				try
				{
					me.NetTransform.RpcSnapTo(best.transform.position);
				}
				catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "AutoVent.Escape: snapping to vent"); }
				try
				{
					me.MyPhysics.RpcEnterVent(best.Id);
				}
				catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "AutoVent.Escape: entering vent"); }
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "AutoVent.Escape: escaping to vent"); }
		}

		[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.MurderPlayer))]
		internal static class AutoVentKillPatch
		{
			static void Postfix(PlayerControl __instance)
			{
				try
				{
					if(!On || __instance == null || __instance != PlayerControl.LocalPlayer) return;
					Arm();
				}
				catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "AutoVentKillPatch.Postfix: arming auto-vent"); }
			}
		}

		[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CmdCheckMurder))]
		internal static class BodyToVentPatch
		{
			static bool Prefix(PlayerControl __instance, PlayerControl target)
			{
				try
				{
					return !KillToVent(__instance, target);
				}
				catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "BodyToVentPatch.Prefix: routing kill through vent"); return true; }
			}
		}
	}
}
