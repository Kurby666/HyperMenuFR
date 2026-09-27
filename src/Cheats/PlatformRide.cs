using System;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MalumMenu.Cheats
{
	// Airship moving platform: unlock (H&S) + drive left/right.
	// Ported from othermenu Cheats/Platform.cs.
	internal static class PlatformRide
	{
		private const int HandlingId = 20029;

		private static ShipStatus _ship;
		private static MovingPlatformBehaviour _plat;
		private static AirshipStatus _air;
		private static Il2CppArrayBase<PlatformConsole> _consoles;
		private static float _nextFix;

		// Cached platform state, refreshed from the normal ShipStatus tick.
		//
		// The Ship tab used to read IsLeft/Locked straight out of Plat() every IMGUI frame.
		// Plat() walks ShipStatus -> AirshipStatus -> GapPlatform and touches two generic
		// instantiations, and doing that lazily from inside MenuUI.WindowFunction took the
		// whole process down: the CLR aborted while JIT-compiling Plat() under Harmony's
		// MonoMod compile hook (Fatal error. Internal CLR error 0x80131506). That is a runtime
		// abort, not a thrown exception, so no try/catch can contain it. Resolving the platform
		// from a plain Update tick keeps the same behaviour and keeps the work out of the
		// GUI event. Refresh() is throttled to 4 Hz because the tab only needs a label.
		private static bool _cachedPresent;
		private static bool _cachedIsLeft;
		private static bool _cachedLocked;
		private static float _nextRefresh;

		internal static bool OnMap => _cachedPresent;

		internal static bool IsLeft => _cachedIsLeft;

		internal static bool Locked => _cachedLocked;

		internal static void Refresh()
		{
			try
			{
				if(Time.time < _nextRefresh)
					return;
				_nextRefresh = Time.time + 0.25f;

				MovingPlatformBehaviour p = Plat();
				_cachedPresent = p != null;
				if(p == null)
				{
					_cachedIsLeft = false;
					_cachedLocked = false;
					return;
				}

				_cachedIsLeft = p.IsLeft;
				_cachedLocked = LockedFor(p);
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "PlatformRide.Refresh: reading platform state"); }
		}

		private static bool LockedFor(MovingPlatformBehaviour p)
		{
			if(_air != null && _air.outOfOrderPlat != null && _air.outOfOrderPlat.activeSelf)
				return true;
			return !((Behaviour)p).enabled || !((Component)p).gameObject.activeSelf;
		}

		private static MovingPlatformBehaviour Plat()
		{
			try
			{
				ShipStatus ship = ShipStatus.Instance;
				if(ship == null)
				{
					_ship = null;
					_plat = null;
					_air = null;
					_consoles = null;
					return null;
				}

				if(_ship == ship) return _plat;

				_ship = ship;
				_consoles = null;
				_air = ((Il2CppObjectBase)ship).TryCast<AirshipStatus>();
				if(_air != null && _air.GapPlatform != null)
					_plat = _air.GapPlatform;
				else
					_plat = Object.FindObjectOfType<MovingPlatformBehaviour>();
				return _plat;
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "PlatformRide.Plat: finding platform"); return null; }
		}

		internal static void Tick()
		{
			try
			{
				Refresh();

				if(!CheatToggles.platformUnlock || Time.time < _nextFix)
					return;
				_nextFix = Time.time + 1f;

				MovingPlatformBehaviour p = Plat();
				if(p != null)
					Free(p);
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "PlatformRide.Tick: keeping platform unlocked"); }
		}

		private static void Free(MovingPlatformBehaviour p)
		{
			try
			{
				if(ShipStatus.Instance == null)
					return;

				((Component)p).gameObject.SetActive(true);
				((Behaviour)p).enabled = true;
				if(_air != null && _air.outOfOrderPlat != null)
					_air.outOfOrderPlat.SetActive(false);

				if(_consoles == null)
					_consoles = ((Component)ShipStatus.Instance).GetComponentsInChildren<PlatformConsole>(true);
				foreach(PlatformConsole c in _consoles)
				{
					if(c == null) continue;
					((Component)c).gameObject.SetActive(true);
					((Behaviour)c).enabled = true;
				}

				if(p.Target != null)
					return;

				Transform t = ((Component)p).transform;
				if((t.localPosition - p.DisabledPosition).sqrMagnitude > 0.01f)
					return;
				t.localPosition = p.IsLeft ? p.LeftPosition : p.RightPosition;
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "PlatformRide.Free: unlocking platform"); }
		}

		internal static string Unlock()
		{
			if(ShipStatus.Instance == null) return "In match only.";

			MovingPlatformBehaviour p = Plat();
			if(p == null)
				return "No platform here — Airship only.";

			Free(p);
			return "Unlocked.";
		}

		internal static string Move(bool left)
		{
			if(ShipStatus.Instance == null)
				return "In match only.";

			MovingPlatformBehaviour p = Plat();
			if(p == null) return "No platform here — Airship only.";

			try
			{
				p.SetSide(left);
			}
			catch (Exception ex)
			{
				ErrorReporter.Report(ex, HandlingId, "PlatformRide.Move: moving platform");
				return "Failed.";
			}

			return left ? "Moved left." : "Moved right.";
		}

		internal static string Toggle()
		{
			MovingPlatformBehaviour p = Plat();
			if(p == null)
				return "No platform here — Airship only.";
			return Move(!p.IsLeft);
		}
	}
}
