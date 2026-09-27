using System;
using System.Collections;
using System.Collections.Generic;
using AmongUs.GameOptions;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using HarmonyLib;
using UnityEngine;

namespace MalumMenu.Cheats
{
	// Host-only lobby/match pranks: turn everyone into eggs, rainbow colors,
	// cosmetic cycles, plus host-view-only size / spin / float / jelly gags and
	// a repeat-murder loop. Ported from othermenu Lobby/HyperLobbyPranks.cs
	// (703 lines) as a static class driven by src ticks instead of a MonoBehaviour
	// (Tick from ShipStatus_FixedUpdate, LateTick from PlayerPhysics_LateUpdate).
	// Adaptations: English-only feedback strings returned to the caller for
	// toasts; HyperForceRoles.Assign replaced by Network.BatchedMessage
	// QueueSetRole (src host-broadcast / GameDataTo-host routing); cosmetics sent
	// through QueueSetColor/QueueSetHatStr/... with the sequence-id pattern from
	// OutfitTools; role grants only when an anticheat is present, as upstream.
	internal static class LobbyPranks
	{
		private const int HandlingId = 20065;

		private const float TickInterval = 1.2f;
		private const float TinyScale = 0.45f;
		private const float GiantScale = 2.0f;
		private const float SpinSpeed = 200f;
		private const float WobbleAmp = 18f;
		private const float WobbleFreq = 9f;
		private const float FloatAmp = 0.45f;
		private const float FloatFreq = 5f;
		private const float JellyAmp = 0.22f;
		private const float JellyFreq = 9f;

		private static bool _rainbow;
		private static bool _skinCycle;
		private static bool _sync = true;
		private static int _scaleMode;
		private static int _spinMode;
		private static int _animMode;
		private static bool _rolesGranted;
		private static float _effectTimer;
		private static int _colorId;

		private static readonly Dictionary<byte, int> _originalColors = new Dictionary<byte, int>();
		private static readonly Dictionary<byte, string> _originalSkins = new Dictionary<byte, string>();
		private static readonly Dictionary<byte, string> _originalHats = new Dictionary<byte, string>();
		private static readonly Dictionary<byte, string> _originalVisors = new Dictionary<byte, string>();
		private static readonly Dictionary<byte, RoleTypes> _savedRoles = new Dictionary<byte, RoleTypes>();
		private static readonly Dictionary<byte, float> _nextChange = new Dictionary<byte, float>();
		private static readonly Dictionary<byte, int> _playerColor = new Dictionary<byte, int>();
		private static readonly HashSet<byte> _egged = new HashSet<byte>();

		// Murder loop state
		private static byte _loopId;
		private static int _loopLeft;
		private static float _loopAt;

		internal static bool RainbowActive => _rainbow;
		internal static bool SkinCycleActive => _skinCycle;
		internal static bool SyncMode => _sync;
		internal static int ScaleMode => _scaleMode;
		internal static int SpinMode => _spinMode;
		internal static int AnimMode => _animMode;
		internal static int LoopLeft => _loopLeft;

		private static bool Ready(out string why)
		{
			if (!Utils.isInGame)
			{
				why = "In match only (not in lobby).";
				return false;
			}

			if (!Utils.isHost)
			{
				why = "Host only.";
				return false;
			}

			why = null;
			return true;
		}

		private static bool Valid(PlayerControl pc) => pc != null && pc.Data != null && !pc.Data.Disconnected;

		private static bool IsLocal(PlayerControl pc) =>
			PlayerControl.LocalPlayer != null && pc != null && pc.PlayerId == PlayerControl.LocalPlayer.PlayerId;

		private static PlayerControl ById(byte id)
		{
			if (PlayerControl.AllPlayerControls == null) return null;
			foreach (PlayerControl pc in PlayerControl.AllPlayerControls)
				if (pc != null && pc.PlayerId == id) return pc;
			return null;
		}

		/// <summary>Broadcast when we are the host, otherwise forwarded to the host (src routing convention).</summary>
		private static Network.BatchedMessage Batch()
		{
			return AmongUsClient.Instance.AmHost
				? new Network.BatchedMessage()
				: new Network.BatchedMessage(AmongUsClient.Instance.HostId);
		}

		// ---------------------------------------------------------------- cosmetics

		private static string RandomHat()
		{
			var hats = HatManager.Instance.allHats;
			if (hats == null || hats.Length == 0) return "";
			return hats[UnityEngine.Random.Range(0, hats.Length)].ProductId;
		}

		private static string RandomSkin()
		{
			var skins = HatManager.Instance.allSkins;
			if (skins == null || skins.Length == 0) return "";
			return skins[UnityEngine.Random.Range(0, skins.Length)].ProductId;
		}

		private static string RandomVisor()
		{
			var visors = HatManager.Instance.allVisors;
			if (visors == null || visors.Length == 0) return "";
			return visors[UnityEngine.Random.Range(0, visors.Length)].ProductId;
		}

		private static void ApplyOnce(PlayerControl pc, int colorId)
		{
			try
			{
				Network.BatchedMessage batch = Batch();

				if (_rainbow)
				{
					batch.QueueSetColor(pc, (byte)colorId);
				}

				if (_skinCycle)
				{
					NetworkedPlayerInfo.PlayerOutfit o = pc.CurrentOutfit;
					if (o != null)
					{
						batch.QueueSetHatStr(pc, RandomHat(), ++o.HatSequenceId);
						batch.QueueSetSkinStr(pc, RandomSkin(), ++o.SkinSequenceId);
						batch.QueueSetVisorStr(pc, RandomVisor(), ++o.VisorSequenceId);
					}
				}

				batch.QueueShapeshift(pc, pc, true);
				batch.FinishBatch();

				// Keep the local impostor from being left stuck mid-shapeshift by the RPC.
				if (IsLocal(pc))
				{
					try { pc.RpcRejectShapeshift(); } catch { }
				}

				_egged.Add(pc.PlayerId);
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "LobbyPranks.ApplyOnce: applying prank cosmetics"); }
		}

		private static void EnsureEggSetup()
		{
			if (_rolesGranted) return;

			bool ac = Utilities.IsAnticheatPresent();
			ClearMaps();

			try
			{
				Network.BatchedMessage batch = Batch();
				bool queued = false;

				foreach (PlayerControl pc in PlayerControl.AllPlayerControls)
				{
					if (pc == null || pc.Data == null) continue;

					try
					{
						NetworkedPlayerInfo.PlayerOutfit def = pc.Data.DefaultOutfit;
						_originalColors[pc.PlayerId] = def.ColorId;
						_originalSkins[pc.PlayerId] = def.SkinId;
						_originalHats[pc.PlayerId] = def.HatId;
						_originalVisors[pc.PlayerId] = def.VisorId;
					}
					catch { }

					_savedRoles[pc.PlayerId] = pc.Data.RoleType;

					if (ac && pc.Data.RoleType != RoleTypes.Shapeshifter)
					{
						batch.QueueSetRole(pc, RoleTypes.Shapeshifter, true);
						queued = true;
					}
				}

				if (queued) batch.FinishBatch();
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "LobbyPranks.EnsureEggSetup: saving appearances"); }

			_effectTimer = 0f;
			_rolesGranted = true;
		}

		private static void TeardownEggIfIdle()
		{
			if (_rainbow || _skinCycle || !_rolesGranted) return;

			_rolesGranted = false;
			bool ac = Utilities.IsAnticheatPresent();

			if (Utils.isHost && PlayerControl.AllPlayerControls != null)
			{
				try
				{
					Network.BatchedMessage batch = Batch();

					foreach (PlayerControl pc in PlayerControl.AllPlayerControls)
					{
						if (!Valid(pc)) continue;

						NetworkedPlayerInfo.PlayerOutfit o = pc.CurrentOutfit;
						if (o != null)
						{
							if (_originalColors.TryGetValue(pc.PlayerId, out int col))
								batch.QueueSetColor(pc, (byte)col);
							if (_originalSkins.TryGetValue(pc.PlayerId, out string sk))
								batch.QueueSetSkinStr(pc, sk, ++o.SkinSequenceId);
							if (_originalHats.TryGetValue(pc.PlayerId, out string ht))
								batch.QueueSetHatStr(pc, ht, ++o.HatSequenceId);
							if (_originalVisors.TryGetValue(pc.PlayerId, out string vs))
								batch.QueueSetVisorStr(pc, vs, ++o.VisorSequenceId);
						}

						if (IsLocal(pc))
						{
							try { pc.RpcRejectShapeshift(); } catch { }
						}

						if (ac && _savedRoles.TryGetValue(pc.PlayerId, out RoleTypes r) && r != RoleTypes.Shapeshifter)
							batch.QueueSetRole(pc, r, true);
					}

					batch.FinishBatch();
				}
				catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "LobbyPranks.TeardownEggIfIdle: restoring appearances"); }
			}

			ClearMaps();
		}

		private static void ClearMaps()
		{
			_originalColors.Clear();
			_originalSkins.Clear();
			_originalHats.Clear();
			_originalVisors.Clear();
			_savedRoles.Clear();
			_nextChange.Clear();
			_playerColor.Clear();
			_egged.Clear();
		}

		private static void ResetScaleLocal()
		{
			if (PlayerControl.AllPlayerControls == null) return;
			try
			{
				foreach (PlayerControl pc in PlayerControl.AllPlayerControls)
				{
					if (!Valid(pc)) continue;
					try { pc.transform.localScale = Vector3.one; } catch { }
				}
			}
			catch { }
		}

		private static void ResetRotationLocal()
		{
			if (PlayerControl.AllPlayerControls == null) return;
			try
			{
				foreach (PlayerControl pc in PlayerControl.AllPlayerControls)
				{
					if (!Valid(pc)) continue;
					try { pc.transform.localEulerAngles = Vector3.zero; } catch { }
				}
			}
			catch { }
		}

		// ---------------------------------------------------------------- ticks

		internal static void Tick()
		{
			try
			{
				if (_loopLeft > 0) LoopTick();

				bool live = _rainbow || _skinCycle;
				if (!live && _scaleMode == 0 && _spinMode == 0 && _animMode == 0) return;

				if (!Utils.isInGame)
				{
					ResetScaleLocal();
					ResetRotationLocal();
					ResetAll(false);
					return;
				}

				if (!Utils.isHost || PlayerControl.AllPlayerControls == null) return;

				if (_scaleMode != 0 || _spinMode != 0 || _animMode != 0)
				{
					float s = _scaleMode == 1 ? TinyScale : _scaleMode == 2 ? GiantScale : 1f;
					float t = Time.time;
					foreach (PlayerControl pc in PlayerControl.AllPlayerControls)
					{
						if (!Valid(pc)) continue;
						bool egg = _egged.Contains(pc.PlayerId);
						try
						{
							float bs = egg ? s : 1f;
							float sx = bs, sy = bs;
							if (egg && _animMode == 2)
							{
								float j = Mathf.Sin(t * JellyFreq) * JellyAmp;
								sx = bs * (1f + j);
								sy = bs * (1f - j);
							}
							pc.transform.localScale = new Vector3(sx, sy, 1f);

							if (egg && _spinMode == 1)
								pc.transform.Rotate(0f, 0f, SpinSpeed * Time.deltaTime);
							else if (egg && _spinMode == 2)
								pc.transform.localEulerAngles = new Vector3(0f, 0f, Mathf.Sin(Time.time * WobbleFreq) * WobbleAmp);
							else
								pc.transform.localEulerAngles = Vector3.zero;
						}
						catch { }
					}
				}

				if (!live) return;

				if (_sync)
				{
					_effectTimer += Time.deltaTime;
					if (_effectTimer < TickInterval) return;
					_effectTimer = 0f;

					if (_rainbow)
					{
						_colorId++;
						if (_colorId > 17) _colorId = 0;
					}

					foreach (PlayerControl pc in PlayerControl.AllPlayerControls)
					{
						if (!Valid(pc)) continue;
						ApplyOnce(pc, _colorId);
					}
				}
				else
				{
					float now = Time.time;
					foreach (PlayerControl pc in PlayerControl.AllPlayerControls)
					{
						if (!Valid(pc)) continue;
						byte id = pc.PlayerId;
						if (!_nextChange.TryGetValue(id, out float due))
						{
							_nextChange[id] = now + UnityEngine.Random.Range(0f, TickInterval);
							continue;
						}
						if (now < due) continue;
						_nextChange[id] = now + TickInterval;

						int col = 0;
						if (_rainbow)
						{
							_playerColor.TryGetValue(id, out col);
							col = (col + 1) % 18;
							_playerColor[id] = col;
						}
						ApplyOnce(pc, col);
					}
				}
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "LobbyPranks.Tick: pranks tick"); }
		}

		internal static void LateTick()
		{
			try
			{
				if (_animMode != 1) return;
				if (!Utils.isInGame || !Utils.isHost || PlayerControl.AllPlayerControls == null) return;

				float bob = Mathf.Abs(Mathf.Sin(Time.time * FloatFreq)) * FloatAmp;
				foreach (PlayerControl pc in PlayerControl.AllPlayerControls)
				{
					if (!Valid(pc) || !_egged.Contains(pc.PlayerId)) continue;
					try
					{
						Vector3 p = pc.transform.position;
						p.y += bob;
						pc.transform.position = p;
					}
					catch { }
				}
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "LobbyPranks.LateTick: float anim"); }
		}

		// ---------------------------------------------------------------- UI actions

		internal static string ToggleRainbow()
		{
			try
			{
				if (!Ready(out string why)) return why;

				if (_rainbow)
				{
					_rainbow = false;
					TeardownEggIfIdle();
					return "Rainbow off.";
				}

				_rainbow = true;
				EnsureEggSetup();
				return "Rainbow on.";
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "LobbyPranks.ToggleRainbow: toggling rainbow"); return "Failed."; }
		}

		internal static string ToggleSkinCycle()
		{
			try
			{
				if (!Ready(out string why)) return why;

				if (_skinCycle)
				{
					_skinCycle = false;
					TeardownEggIfIdle();
					return "Cosmetic cycle off.";
				}

				_skinCycle = true;
				EnsureEggSetup();
				return "Cosmetic cycle on.";
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "LobbyPranks.ToggleSkinCycle: toggling cosmetic cycle"); return "Failed."; }
		}

		internal static string SyncName() => _sync ? "Sync" : "Staggered";

		internal static string ToggleSync()
		{
			try
			{
				_sync = !_sync;
				_nextChange.Clear();
				_effectTimer = 0f;
				return _sync ? "Beat: sync (all together)." : "Beat: staggered (each on its own).";
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "LobbyPranks.ToggleSync: toggling beat"); return "Failed."; }
		}

		internal static string ScaleName() => _scaleMode == 1 ? "Tiny" : _scaleMode == 2 ? "Giant" : "Normal";

		internal static string CycleScale()
		{
			try
			{
				if (!Ready(out string why)) return why;
				_scaleMode = (_scaleMode + 1) % 3;
				if (_scaleMode == 0) ResetScaleLocal();
				return "Size: " + ScaleName() + " (host view only).";
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "LobbyPranks.CycleScale: cycling scale"); return "Failed."; }
		}

		internal static string SpinName() => _spinMode == 1 ? "Spin" : _spinMode == 2 ? "Wobble" : "None";

		internal static string CycleSpin()
		{
			try
			{
				if (!Ready(out string why)) return why;
				_spinMode = (_spinMode + 1) % 3;
				if (_spinMode == 0) ResetRotationLocal();
				return "Motion: " + SpinName() + " (host view only).";
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "LobbyPranks.CycleSpin: cycling motion"); return "Failed."; }
		}

		internal static string AnimName() => _animMode == 1 ? "Float" : _animMode == 2 ? "Jelly" : "None";

		internal static string CycleAnim()
		{
			try
			{
				if (!Ready(out string why)) return why;
				_animMode = (_animMode + 1) % 3;
				if (_animMode != 2) ResetScaleLocal();
				return "Anim: " + AnimName() + " (host view only).";
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "LobbyPranks.CycleAnim: cycling anim"); return "Failed."; }
		}

		internal static string MassMorphToEgg()
		{
			try
			{
				if (!Ready(out string why)) return why;
				StartMassMorph(null);
				return "Everyone is turning into an egg...";
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "LobbyPranks.MassMorphToEgg: mass morph"); return "Failed."; }
		}

		internal static string MorphAllIntoSelected()
		{
			try
			{
				if (!Ready(out string why)) return why;
				PlayerControl sel = MouseTools.Selected;
				if (sel == null || sel.Data == null) return "No target (LMB a player).";
				StartMassMorph(sel);
				return "Morphing everyone into " + ChatTools.MuteList.Strip(sel.Data.PlayerName) + "...";
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "LobbyPranks.MorphAllIntoSelected: mass morph into target"); return "Failed."; }
		}

		private static void StartMassMorph(PlayerControl into)
		{
			AmongUsClient.Instance.StartCoroutine(
				ErrorReporter.GuardCoroutine(MassMorph(into), HandlingId, "LobbyPranks.MassMorph").WrapToIl2Cpp());
		}

		private static IEnumerator MassMorph(PlayerControl into)
		{
			if (!Utils.isHost || PlayerControl.AllPlayerControls == null) yield break;

			bool ac = Utilities.IsAnticheatPresent();
			Dictionary<byte, RoleTypes> roles = new Dictionary<byte, RoleTypes>();
			Network.BatchedMessage roleBatch = Batch();
			bool queued = false;

			foreach (PlayerControl pc in PlayerControl.AllPlayerControls)
			{
				if (!Valid(pc)) continue;
				roles[pc.PlayerId] = pc.Data.RoleType;
				if (ac && pc.Data.RoleType != RoleTypes.Shapeshifter)
				{
					roleBatch.QueueSetRole(pc, RoleTypes.Shapeshifter, true);
					queued = true;
				}
			}

			if (queued) roleBatch.FinishBatch();
			if (ac) yield return new WaitForSeconds(0.5f);

			Network.BatchedMessage shapeBatch = Batch();
			foreach (PlayerControl pc in PlayerControl.AllPlayerControls)
			{
				if (!Valid(pc)) continue;
				shapeBatch.QueueShapeshift(pc, into != null ? into : pc, true);
				_egged.Add(pc.PlayerId);
			}
			shapeBatch.FinishBatch();

			if (ac)
			{
				yield return new WaitForSeconds(0.5f);

				Network.BatchedMessage backBatch = Batch();
				bool restore = false;
				foreach (PlayerControl pc in PlayerControl.AllPlayerControls)
				{
					if (!Valid(pc)) continue;
					if (roles.TryGetValue(pc.PlayerId, out RoleTypes r) && r != RoleTypes.Shapeshifter)
					{
						backBatch.QueueSetRole(pc, r, true);
						restore = true;
					}
				}
				if (restore) backBatch.FinishBatch();
			}
		}

		internal static string MurderLoop(PlayerControl target, int times)
		{
			try
			{
				if (AmongUsClient.Instance == null || !Utils.isHost) return "Host only.";
				if (ShipStatus.Instance == null || MeetingHud.Instance != null) return "In match only.";
				if (target == null || target.Data == null || target == PlayerControl.LocalPlayer) return "No target.";

				if (_loopLeft > 0 && _loopId == target.PlayerId)
				{
					_loopLeft = 0;
					return "Stopped.";
				}

				_loopId = target.PlayerId;
				_loopLeft = Mathf.Clamp(times, 1, 50);
				_loopAt = 0f;
				return "Looping: " + ChatTools.MuteList.Strip(target.Data.PlayerName);
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "LobbyPranks.MurderLoop: starting murder loop"); return "Failed."; }
		}

		private static void LoopTick()
		{
			if (_loopLeft <= 0) return;
			if (ShipStatus.Instance == null || MeetingHud.Instance != null)
			{
				_loopLeft = 0;
				return;
			}
			if (Time.time < _loopAt) return;
			_loopAt = Time.time + 0.4f;

			PlayerControl me = PlayerControl.LocalPlayer;
			PlayerControl t = ById(_loopId);
			if (me == null || t == null || t.Data == null || t.Data.Disconnected)
			{
				_loopLeft = 0;
				return;
			}

			_loopLeft--;
			try
			{
				me.RpcMurderPlayer(t, true);
			}
			catch
			{
				_loopLeft = 0;
			}
		}

		internal static string ResetAppearance()
		{
			try
			{
				if (!Utils.isHost) return "Host only.";

				_rainbow = false;
				_skinCycle = false;
				TeardownEggIfIdle();

				_scaleMode = 0;
				ResetScaleLocal();
				_spinMode = 0;
				ResetRotationLocal();
				_animMode = 0;
				_egged.Clear();

				if (PlayerControl.AllPlayerControls != null)
				{
					foreach (PlayerControl pc in PlayerControl.AllPlayerControls)
					{
						if (!Valid(pc)) continue;
						if (IsLocal(pc))
						{
							try { pc.RpcRejectShapeshift(); } catch { }
						}
					}
				}

				return "Appearance reset.";
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "LobbyPranks.ResetAppearance: resetting appearance"); return "Failed."; }
		}

		/// <summary>Clears all prank state (lobby start / leaving a match).</summary>
		private static void ResetAll(bool keepRoles)
		{
			_rainbow = false;
			_skinCycle = false;
			_animMode = 0;
			_spinMode = 0;
			_scaleMode = 0;
			_rolesGranted = false;
			_loopLeft = 0;
			_loopId = 0;
			ClearMaps();
		}

		[HarmonyPatch(typeof(LobbyBehaviour), nameof(LobbyBehaviour.Start))]
		internal static class PranksLobbyResetPatch
		{
			static void Postfix()
			{
				try { ResetAll(false); }
				catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "PranksLobbyResetPatch.Postfix: resetting pranks"); }
			}
		}

		[HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.Start))]
		internal static class PranksMatchResetPatch
		{
			static void Postfix()
			{
				try
				{
					_loopLeft = 0;
					_nextChange.Clear();
					_effectTimer = 0f;
				}
				catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "PranksMatchResetPatch.Postfix: resetting prank timers"); }
			}
		}
	}
}
