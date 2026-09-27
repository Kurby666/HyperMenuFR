using System;
using HarmonyLib;
using InnerNet;
using UnityEngine;

namespace MalumMenu.Cheats
{
	// World tilt: tilt the whole world on your own screen (client-side gag).
	// Ported from othermenu Cheats/MapTilt.cs.
	internal static class WorldTilt
	{
		private const int HandlingId = 20043;

		private static GameObject _container;
		private static bool _applied;
		private static float _angle;

		internal static bool Applied => _applied;

		private static bool Want()
		{
			try
			{
				return CheatToggles.worldTilt
					&& ShipStatus.Instance != null
					&& LobbyBehaviour.Instance == null
					&& MeetingHud.Instance == null;
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "WorldTilt.Want: checking tilt conditions"); return false; }
		}

		internal static void Tick()
		{
			try
			{
				if(!Want())
				{
					if(_applied) Disable();
					return;
				}

				float angle = Mathf.Clamp(CheatToggles.worldTiltAngle, -180f, 180f);

				if(!_applied)
				{
					Enable(angle);
					return;
				}

				if(!Mathf.Approximately(angle, _angle) && _container != null)
				{
					_angle = angle;
					_container.transform.rotation = Quaternion.Euler(0f, 0f, angle);
				}
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "WorldTilt.Tick: ticking world tilt"); }
		}

		private static void Enable(float angle)
		{
			try
			{
				ShipStatus ship = ShipStatus.Instance;
				if(ship == null) return;

				_container = new GameObject("MalumTilt");
				_container.hideFlags = HideFlags.HideAndDontSave;
				_container.transform.position = Vector3.zero;
				_container.transform.rotation = Quaternion.Euler(0f, 0f, angle);

				((Component)ship).transform.SetParent(_container.transform, true);

				_angle = angle;
				_applied = true;
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "WorldTilt.Enable: enabling world tilt"); }
		}

		private static void Disable()
		{
			try
			{
				ShipStatus ship = ShipStatus.Instance;
				if(ship != null)
					((Component)ship).transform.SetParent(null, true);

				if(_container != null)
					UnityEngine.Object.Destroy(_container);

				_container = null;
				_applied = false;
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "WorldTilt.Disable: disabling world tilt"); }
		}

		internal static void PlaceOther(PlayerControl pc)
		{
			try
			{
				if(!_applied || pc == null || ((InnerNetObject)pc).AmOwner)
					return;

				Transform tr = ((Component)pc).transform;
				Vector3 p = tr.position;

				float rad = _angle * Mathf.Deg2Rad;
				float cos = Mathf.Cos(rad);
				float sin = Mathf.Sin(rad);
				tr.position = new Vector3(p.x * cos - p.y * sin, p.x * sin + p.y * cos, p.z);
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "WorldTilt.PlaceOther: placing remote player"); }
		}

		[HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
		internal static class TiltTickPatch
		{
			static void Postfix()
			{
				try
				{
					Tick();
				}
				catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "TiltTickPatch.Postfix: ticking world tilt"); }
			}
		}

		[HarmonyPatch(typeof(PlayerPhysics), nameof(PlayerPhysics.LateUpdate))]
		internal static class TiltPlacePatch
		{
			static void Postfix(PlayerPhysics __instance)
			{
				try
				{
					if(!Applied) return;
					if(__instance == null || __instance.myPlayer == null) return;
					if(__instance.myPlayer == PlayerControl.LocalPlayer) return;
					PlaceOther(__instance.myPlayer);
				}
				catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "TiltPlacePatch.Postfix: placing remote player"); }
			}
		}
	}
}
