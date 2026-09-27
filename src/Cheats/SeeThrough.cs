using System;
using System.Collections.Generic;
using HarmonyLib;

namespace MalumMenu.Cheats
{
	// See-through: see players hiding in vents + see vanished phantoms.
	// Ported from othermenu Cheats/NocturneSeeThrough.cs (vents + phantoms parts;
	// see-ghosts and see-shield already exist in src).
	internal static class SeeThrough
	{
		private const int HandlingId = 20037;

		private static readonly Dictionary<byte, float> _faded = new Dictionary<byte, float>();

		internal static void Reset() => _faded.Clear();

		internal static void RestoreAll()
		{
			try
			{
				foreach(PlayerControl pc in PlayerControl.AllPlayerControls)
				{
					if(pc == null || pc.cosmetics == null) continue;
					if(_faded.TryGetValue(pc.PlayerId, out float a))
					{
						pc.invisibilityAlpha = a;
						try
						{
							pc.cosmetics.SetPhantomRoleAlpha(1f);
						}
						catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "SeeThrough.RestoreAll: restoring phantom alpha"); }
						ShowName(pc, true);
					}
				}
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "SeeThrough.RestoreAll: restoring visibility"); }
			_faded.Clear();
		}

		private static void ShowName(PlayerControl p, bool on)
		{
			try
			{
				var t = p.cosmetics.nameText;
				if(t != null) t.gameObject.SetActive(on);
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "SeeThrough.ShowName: toggling name text"); }
		}

		[HarmonyPatch(typeof(PlayerPhysics), nameof(PlayerPhysics.FixedUpdate))]
		internal static class SeeThroughPatch
		{
			static void Postfix(PlayerPhysics __instance)
			{
				try
				{
					bool vents = CheatToggles.seeInVents;
					if(!vents)
					{
						if(_faded.Count > 0)
							RestoreAll();
						return;
					}
					if(ShipStatus.Instance == null) return;

					PlayerControl p = __instance.myPlayer;
					PlayerControl me = PlayerControl.LocalPlayer;
					if(p == null || me == null || p.Data == null || me.Data == null) return;
					if(p.cosmetics == null || p == me || me.Data.IsDead) return;

					if(p.inVent)
					{
						if(!_faded.ContainsKey(p.PlayerId)) _faded[p.PlayerId] = p.invisibilityAlpha;
						if(!p.Visible)
						{
							p.Visible = true;
							p.invisibilityAlpha = 0.5f;
							p.cosmetics.SetPhantomRoleAlpha(0.5f);
							ShowName(p, true);
						}
					}
					else if(_faded.TryGetValue(p.PlayerId, out float a))
					{
						_faded.Remove(p.PlayerId);
						p.invisibilityAlpha = a;
						p.cosmetics.SetPhantomRoleAlpha(1f);
						ShowName(p, true);
					}
				}
				catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "SeeThroughPatch.Postfix: revealing vented players"); }
			}
		}

		[HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.OnEnable))]
		internal static class SeeThroughReset
		{
			static void Postfix()
			{
				try { Reset(); }
				catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "SeeThroughReset.Postfix: resetting vent visibility"); }
			}
		}

		[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Start))]
		internal static class SeeThroughMeetingReset
		{
			static void Postfix()
			{
				try { RestoreAll(); }
				catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "SeeThroughMeetingReset.Postfix: restoring visibility"); }
			}
		}

		[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CalculatedAlpha), MethodType.Getter)]
		internal static class SeePhantomsPatch
		{
			static void Postfix(PlayerControl __instance, ref float __result)
			{
				try
				{
					if(!CheatToggles.seeVanished) return;
					if(__instance == null || __instance == PlayerControl.LocalPlayer) return;
					if(__result >= 0.5f)
						return;
					__result = 0.5f;
				}
				catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "SeePhantomsPatch.Postfix: revealing vanished phantoms"); }
			}
		}
	}
}
