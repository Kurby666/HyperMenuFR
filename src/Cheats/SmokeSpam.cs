using System;
using AmongUs.GameOptions;
using UnityEngine;

namespace MalumMenu.Cheats
{
	// Smoke bomb (vanish spam): phantom-only, in match. Smoke cloud for everyone.
	// Ported from othermenu Cheats/SmokeSpam.cs.
	internal static class SmokeSpam
	{
		private const int HandlingId = 20046;
		private const float Gap = 0.12f;

		private static float _next;
		private static bool _armed;
		private static int _origColor = -1;

		internal static void Tick()
		{
			try
			{
				if(!CheatToggles.smokeSpam)
				{
					if(_armed) Restore();
					return;
				}

				PlayerControl me = PlayerControl.LocalPlayer;
				if(me == null || me.Data == null || me.Data.IsDead)
					return;
				if(ShipStatus.Instance == null || MeetingHud.Instance != null) return;
				if(me.Data.RoleType != RoleTypes.Phantom) return;

				float now = Time.unscaledTime;
				if(now < _next) return;

				_next = now + Gap;

				if(!_armed)
				{
					_armed = true;
					if(me.Data.DefaultOutfit != null) _origColor = me.Data.DefaultOutfit.ColorId;
				}

				if(Palette.PlayerColors != null && Palette.PlayerColors.Length > 0)
				{
					try
					{
						me.CmdCheckColor((byte)UnityEngine.Random.Range(0, Palette.PlayerColors.Length));
					}
					catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "SmokeSpam.Tick: scrambling color"); }
				}

				try
				{
					me.RpcAppear(true);
					me.RpcVanish();
				}
				catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "SmokeSpam.Tick: sending appear/vanish"); }
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "SmokeSpam.Tick: ticking smoke spam"); }
		}

		private static void Restore()
		{
			try
			{
				_armed = false;
				PlayerControl me = PlayerControl.LocalPlayer;
				if(me == null || me.Data == null)
				{
					_origColor = -1;
					return;
				}

				try { me.RpcAppear(true); }
				catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "SmokeSpam.Restore: reappearing"); }

				if(_origColor >= 0)
				{
					try
					{
						me.CmdCheckColor((byte)_origColor);
					}
					catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "SmokeSpam.Restore: restoring color"); }
				}
				_origColor = -1;
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "SmokeSpam.Restore: restoring after smoke spam"); }
		}
	}
}
