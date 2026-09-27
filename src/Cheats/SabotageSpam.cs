using System;
using HarmonyLib;
using UnityEngine;

namespace MalumMenu.Cheats
{
	// Persistent sabotage pressure: re-fire main sabotage, keep lights off,
	// infinite mushroom on Fungle, and multi-sabotage for non-host impostors.
	// Ported from othermenu Cheats/HyperSabotage.cs + Patches/HyperMultiSabotagePatch.cs.
	internal static class SabotageSpam
	{
		private const int HandlingId = 20030;
		private const float TickInterval = 0.6f;

		private static float _next;

		internal static void Tick()
		{
			try
			{
				if(ShipStatus.Instance == null) return;
				if(Time.unscaledTime < _next) return;
				_next = Time.unscaledTime + TickInterval;

				byte mapId = Utils.GetCurrentMapID();
				if(CheatToggles.keepLightsOff && mapId != 5)
					KeepLightsOff();
				if(CheatToggles.infMushroom && mapId == 5)
					ShipStatus.Instance.RpcUpdateSystem(SystemTypes.MushroomMixupSabotage, 1);
				if(CheatToggles.spamMainSab)
					FireMain(mapId);
				if(CheatToggles.autoFixSabotage && NotImpostor() && AnyActive())
					Fix();
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "SabotageSpam.Tick: ticking sabotage spam"); }
		}

		internal static void FireMain(byte mapId)
		{
			try
			{
				switch(mapId)
				{
					case 2:
						ShipStatus.Instance.RpcUpdateSystem(SystemTypes.Laboratory, 128);
						break;
					case 4:
						ShipStatus.Instance.RpcUpdateSystem(SystemTypes.HeliSabotage, 128);
						break;
					default:
						ShipStatus.Instance.RpcUpdateSystem(SystemTypes.Reactor, 128);
						break;
				}
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "SabotageSpam.FireMain: firing main sabotage"); }
		}

		private static void KeepLightsOff()
		{
			try
			{
				ShipStatus ss = ShipStatus.Instance;
				if(ss == null) return;
				SwitchSystem sys = ss.Systems[SystemTypes.Electrical].Cast<SwitchSystem>();
				if(sys == null) return;
				int a = sys.ActualSwitches, e = sys.ExpectedSwitches;
				for(byte i = 0; i < 5; i++)
					if(((a >> i) & 1) == ((e >> i) & 1))
						ss.RpcUpdateSystem(SystemTypes.Electrical, i);
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "SabotageSpam.KeepLightsOff: re-breaking lights"); }
		}

		internal static void Fix()
		{
			try
			{
				if(ShipStatus.Instance == null)
					return;
				FixLights();
				FixConsoles(SystemTypes.Reactor);
				FixConsoles(SystemTypes.Laboratory);
				FixConsoles(SystemTypes.HeliSabotage);
				FixConsoles(SystemTypes.LifeSupp);
				ShipStatus.Instance.RpcUpdateSystem(SystemTypes.Comms, 0);
				FixConsoles(SystemTypes.Comms);
				ShipStatus.Instance.RpcUpdateSystem(SystemTypes.MushroomMixupSabotage, 0);
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "SabotageSpam.Fix: auto-fixing sabotage"); }
		}

		private static void FixConsoles(SystemTypes sys)
		{
			try
			{
				ShipStatus.Instance.RpcUpdateSystem(sys, 0x40);
				ShipStatus.Instance.RpcUpdateSystem(sys, 0x41);
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "SabotageSpam.FixConsoles: fixing sabotage consoles"); }
		}

		private static void FixLights()
		{
			ShipStatus ss = ShipStatus.Instance;
			try
			{
				SwitchSystem sys = ss.Systems[SystemTypes.Electrical].Cast<SwitchSystem>();
				if(sys == null) return;
				int a = sys.ActualSwitches, e = sys.ExpectedSwitches;
				for(byte i = 0; i < 5; i++)
					if(((a >> i) & 1) != ((e >> i) & 1))
						ss.RpcUpdateSystem(SystemTypes.Electrical, i);
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "SabotageSpam.FixLights: fixing lights"); }
		}

		private static readonly SystemTypes[] Watch =
		{
			SystemTypes.Electrical, SystemTypes.Reactor, SystemTypes.Laboratory,
			SystemTypes.HeliSabotage, SystemTypes.LifeSupp, SystemTypes.Comms,
			SystemTypes.MushroomMixupSabotage
		};

		private static bool AnyActive()
		{
			ShipStatus ss = ShipStatus.Instance;
			if(ss == null || ss.Systems == null)
				return false;
			for(int i = 0; i < Watch.Length; i++)
			{
				try
				{
					if(!ss.Systems.ContainsKey(Watch[i])) continue;
					IActivatable a = ss.Systems[Watch[i]].TryCast<IActivatable>();
					if(a != null && a.IsActive) return true;
				}
				catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "SabotageSpam.AnyActive: checking sabotage state"); }
			}
			return false;
		}

		private static bool NotImpostor()
		{
			PlayerControl me = PlayerControl.LocalPlayer;
			return me == null || me.Data == null || me.Data.Role == null || !me.Data.Role.IsImpostor;
		}

		internal static bool MultiSabotageActive()		{
			if(!CheatToggles.multiSabotage) return false;
			if(AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost) return false;
			PlayerControl me = PlayerControl.LocalPlayer;
			return me != null && me.Data != null && me.Data.Role != null && me.Data.Role.IsImpostor;
		}

		[HarmonyPatch(typeof(SabotageSystemType), nameof(SabotageSystemType.AnyActive), MethodType.Getter)]
		internal static class MultiSabotageActivePatch
		{
			static bool Prefix(ref bool __result)
			{
				try
				{
					if(!MultiSabotageActive()) return true;
					__result = false;
					return false;
				}
				catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "MultiSabotageActivePatch.Prefix: faking no active sabotage"); return true; }
			}
		}

		[HarmonyPatch(typeof(SabotageSystemType), nameof(SabotageSystemType.UpdateSystem))]
		internal static class MultiSabotageTimerPatch
		{
			static void Prefix(SabotageSystemType __instance, PlayerControl player)
			{
				try
				{
					if(!MultiSabotageActive()) return;
					if(__instance != null && player != null && player.AmOwner)
						__instance.Timer = 0f;
				}
				catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "MultiSabotageTimerPatch.Prefix: zeroing sabotage timer"); }
			}
		}
	}
}
