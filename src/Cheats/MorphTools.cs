using System;
using System.Collections;
using System.Collections.Generic;
using AmongUs.GameOptions;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using HarmonyLib;
using UnityEngine;

namespace MalumMenu.Cheats
{
	// Morph anyone into anyone (host, in match). Ported from othermenu
	// Player/NocturneMorph.cs (140 lines): victims are granted Shapeshifter for the
	// duration, shifted into the target's appearance, then restored to their
	// original role. Adaptations: revert list is tracked locally (_morphed) because
	// PlayerControl.shapeshiftTargetPlayerId has no reference in src and would not
	// compile; role set via host RpcSetRole(role, true) (PlayersTab pattern) instead
	// of NocturneForceRoles; coroutine runs through ErrorReporter.GuardCoroutine
	// (HostOnlyTab pattern); English-only feedback.
	internal static class MorphTools
	{
		private const int HandlingId = 20052;
		private const float Gap = 0.45f;

		private static readonly HashSet<byte> _victims = new HashSet<byte>();
		private static readonly HashSet<byte> _morphed = new HashSet<byte>();

		internal static bool IsSelected(byte id) => _victims.Contains(id);
		internal static int SelectedCount => _victims.Count;

		internal static void ToggleSelect(byte id)
		{
			try
			{
				if(!_victims.Add(id)) _victims.Remove(id);
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "MorphTools.ToggleSelect: toggling victim"); }
		}

		internal static void SelectAll()
		{
			try
			{
				_victims.Clear();
				foreach(PlayerControl p in PlayerControl.AllPlayerControls)
				{
					if(p == null || p.Data == null || p.Data.Disconnected) continue;
					_victims.Add(p.PlayerId);
				}
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "MorphTools.SelectAll: selecting victims"); }
		}

		internal static void ClearSelected()
		{
			_victims.Clear();
		}

		private static void Prune()
		{
			try
			{
				HashSet<byte> alive = new HashSet<byte>();
				foreach(PlayerControl p in PlayerControl.AllPlayerControls)
				{
					if(p == null || p.Data == null || p.Data.Disconnected) continue;
					alive.Add(p.PlayerId);
				}
				_victims.RemoveWhere(id => !alive.Contains(id));
				_morphed.RemoveWhere(id => !alive.Contains(id));
			}
			catch { }
		}

		private static bool Ready() => Utils.isHost && ShipStatus.Instance != null;

		private static bool Valid(PlayerControl pc) => pc != null && pc.Data != null && !pc.Data.Disconnected;

		private static PlayerControl ById(byte id)
		{
			foreach(PlayerControl p in PlayerControl.AllPlayerControls)
				if(p != null && p.PlayerId == id) return p;
			return null;
		}

		internal static string IntoSelected(PlayerControl target)
		{
			try
			{
				if(!Ready()) return "Host only, in match.";
				if(!Valid(target)) return "No target.";
				Prune();
				List<PlayerControl> vics = new List<PlayerControl>();
				foreach(byte id in _victims)
				{
					PlayerControl vic = ById(id);
					if(Valid(vic) && vic != target) vics.Add(vic);
				}
				if(vics.Count == 0) return "No victims selected.";
				Run(vics, target);
				return "Morphing " + vics.Count + " into " + ChatTools.MuteList.Strip(target.Data.PlayerName) + ".";
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "MorphTools.IntoSelected: morphing selected"); return "Failed."; }
		}

		internal static string RevertAll()
		{
			try
			{
				if(!Ready()) return "Host only, in match.";
				Prune();
				List<PlayerControl> vics = new List<PlayerControl>();
				foreach(byte id in _morphed)
				{
					PlayerControl vic = ById(id);
					if(Valid(vic)) vics.Add(vic);
				}
				if(vics.Count == 0) return "No morphs to revert.";
				Run(vics, null);
				return "Reverting " + vics.Count + ".";
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "MorphTools.RevertAll: reverting morphs"); return "Failed."; }
		}

		private static void Run(List<PlayerControl> vics, PlayerControl into)
		{
			try
			{
				AmongUsClient.Instance.StartCoroutine(
					ErrorReporter.GuardCoroutine(Co(vics, into), HandlingId, "MorphTools.Co").WrapToIl2Cpp());
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "MorphTools.Run: starting morph coroutine"); }
		}

		private static IEnumerator Co(List<PlayerControl> vics, PlayerControl into)
		{
			Dictionary<byte, RoleTypes> roles = new Dictionary<byte, RoleTypes>();
			foreach(PlayerControl vic in vics)
			{
				if(!Valid(vic)) continue;
				try
				{
					RoleTypes orig = vic.Data.RoleType;
					roles[vic.PlayerId] = orig;
					if(orig != RoleTypes.Shapeshifter)
						vic.RpcSetRole(RoleTypes.Shapeshifter, true);
				}
				catch { }
			}

			yield return new WaitForSeconds(Gap);

			foreach(PlayerControl vic in vics)
			{
				if(!Valid(vic)) continue;
				try
				{
					vic.RpcShapeshift(into != null ? into : vic, false);
					if(into != null) _morphed.Add(vic.PlayerId);
					else _morphed.Remove(vic.PlayerId);
				}
				catch { }
			}

			yield return new WaitForSeconds(Gap);

			foreach(PlayerControl vic in vics)
			{
				if(!Valid(vic)) continue;
				try
				{
					if(roles.TryGetValue(vic.PlayerId, out RoleTypes orig) && orig != RoleTypes.Shapeshifter)
						vic.RpcSetRole(orig, true);
				}
				catch { }
			}
		}

		[HarmonyPatch(typeof(LobbyBehaviour), nameof(LobbyBehaviour.Start))]
		private static class ResetPatch
		{
			public static void Postfix()
			{
				_victims.Clear();
				_morphed.Clear();
			}
		}
	}
}
