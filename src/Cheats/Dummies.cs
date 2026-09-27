using System;
using System.Collections.Generic;
using AmongUs.GameOptions;
using HarmonyLib;
using Hazel;
using InnerNet;
using UnityEngine;

namespace MalumMenu.Cheats
{
	// Host-spawned bot players that wander, do tasks, fix sabotages, report
	// bodies, chat and vote. Ported from othermenu Host/HyperDummies.cs.
	// Adaptations: static class + Tick() from RoutineManager (src has no
	// hotkey system — spawn via UI button); ForceRoles has no RegisterDefault
	// so crewmate-default is replicated with Assign + Set(pid, 1) + pending
	// ForceNow; AddPlayerInfo guarded by GetPlayerById (AddDummy may already
	// register); English-only names; errors via ErrorReporter.
	internal static class Dummies
	{
		private const int HandlingId = 20058;
		private const int Cap = 15;

		internal sealed class Dummy
		{
			public PlayerControl Pc;
			public string Name;
			public byte Color;
			public string Hat, Skin, Visor;
		}

		private static readonly Dictionary<byte, Dummy> _bots = new Dictionary<byte, Dummy>();
		private static readonly List<(byte pid, float at)> _pending = new List<(byte, float)>();
		private static readonly List<PlayerControl> _scratch = new List<PlayerControl>();
		private static byte _next = 100;
		private static float _resync;

		internal static bool IsDummy(byte pid) => _bots.ContainsKey(pid);
		internal static int Count => _bots.Count;

		internal static IEnumerable<PlayerControl> Live()
		{
			foreach(var kv in _bots)
			{
				PlayerControl pc = kv.Value != null ? kv.Value.Pc : null;
				if(pc != null && pc.Data != null && !pc.Data.IsDead)
					yield return pc;
			}
		}

		internal static void Forget()
		{
			_bots.Clear();
			_pending.Clear();
			DummyAI.Reset();
			DummyChat.Reset();
		}

		internal static void Tick()
		{
			try
			{
				for(int i = _pending.Count - 1; i >= 0; i--)
				{
					if(Time.unscaledTime < _pending[i].at) continue;
					try { ForceRoles.ForceNow(_pending[i].pid); }
					catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "Dummies.Tick: forcing dummy role"); }
					_pending.RemoveAt(i);
				}

				if(_bots.Count > 0 && Time.unscaledTime >= _resync)
				{
					_resync = Time.unscaledTime + 2.5f;
					try { Restamp(); }
					catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "Dummies.Tick: restamping dummies"); }
				}

				if(_bots.Count == 0) return;
				_scratch.Clear();
				foreach(var kv in _bots)
					if(kv.Value != null && kv.Value.Pc != null)
						_scratch.Add(kv.Value.Pc);
				try { DummyAI.Tick(_scratch); }
				catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "Dummies.Tick: ticking dummy AI"); }
				try { DummyChat.TickVision(); }
				catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "Dummies.Tick: ticking dummy vision"); }
				try { DummyChat.TickMeeting(); }
				catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "Dummies.Tick: ticking dummy meeting"); }
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "Dummies.Tick: ticking dummies"); }
		}

		internal static string SpawnNow()
		{
			try
			{
				if(!CheatToggles.enableDummies) return "Dummies are off.";
				if(!Utils.isHost || LobbyBehaviour.Instance == null
					|| PlayerControl.LocalPlayer == null || GameData.Instance == null)
					return "Host in lobby only.";
				if(PlayerControl.AllPlayerControls.Count >= Cap)
					return "Lobby is full.";

				AmongUsClient client = AmongUsClient.Instance;
				PlayerControl prefab = client.PlayerPrefab;
				if(prefab == null) return "No prefab.";

				Vector3 pos = PlayerControl.LocalPlayer.transform.position + Vector3.right * 1.5f;
				byte color = (byte)UnityEngine.Random.Range(0, Palette.PlayerColors.Length);
				string name = PickName();

				PlayerControl dm = UnityEngine.Object.Instantiate(prefab, pos, Quaternion.identity);
				dm.PlayerId = _next;
				var info = GameData.Instance.AddDummy(dm);
				if(GameData.Instance.GetPlayerById(_next) == null)
					GameData.Instance.AddPlayerInfo(info);
				client.Spawn(dm, -2, (SpawnFlags)1);
				try { ((Behaviour)dm.NetTransform).enabled = true; }
				catch { }

				string hat = RandomCosmo(0), skin = RandomCosmo(1), visor = RandomCosmo(2);
				dm.RpcSetColor(color);
				SetName(dm, name);
				dm.RpcSetHat(hat);
				dm.RpcSetSkin(skin);
				dm.RpcSetVisor(visor);
				dm.RpcSetPet(string.Empty);
				dm.RpcSetLevel((uint)UnityEngine.Random.Range(1, 200));
				dm.RpcSetNamePlate(string.Empty);

				ForceRoles.Assign(dm, RoleTypes.Crewmate);
				ForceRoles.Set(dm.PlayerId, 1);

				_bots[_next] = new Dummy { Pc = dm, Name = name, Color = color, Hat = hat, Skin = skin, Visor = visor };
				_pending.Add((dm.PlayerId, Time.unscaledTime + 3f));
				if(++_next > 200)
					_next = 100;
				return "Spawned: " + name;
			}
			catch (Exception ex)
			{
				ErrorReporter.Report(ex, HandlingId, "Dummies.SpawnNow: spawning dummy");
				return "Spawn failed.";
			}
		}

		internal static void SetName(PlayerControl pc, string name)
		{
			try
			{
				AmongUsClient client = AmongUsClient.Instance;
				if(client != null)
				{
					foreach(PlayerControl other in PlayerControl.AllPlayerControls)
					{
						if(other == null || other == PlayerControl.LocalPlayer) continue;
						try
						{
							int cid = ((InnerNetClient)client).GetClientIdFromCharacter(other);
							MessageWriter w = client.StartRpcImmediately(pc.NetId, 6, SendOption.None, cid);
							w.Write(pc.NetId);
							w.Write(name);
							client.FinishRpcImmediately(w);
						}
						catch { }
					}
				}
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "Dummies.SetName: sending targeted name"); }
			try { pc.SetName(name); }
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "Dummies.SetName: setting local name"); }
			try
			{
				if(pc.cosmetics != null && pc.cosmetics.nameText != null)
					pc.cosmetics.nameText.text = name;
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "Dummies.SetName: stamping name text"); }
		}

		private static string RandomCosmo(int kind)
		{
			try
			{
				HatManager hm = DestroyableSingleton<HatManager>.Instance;
				if(hm == null) return string.Empty;
				if(kind == 0)
				{
					var a = hm.allHats;
					return a.Length > 0 ? a[UnityEngine.Random.Range(0, a.Length)].ProdId : string.Empty;
				}
				if(kind == 1)
				{
					var a = hm.allSkins;
					return a.Length > 0 ? a[UnityEngine.Random.Range(0, a.Length)].ProdId : string.Empty;
				}
				var v = hm.allVisors;
				return v.Length > 0 ? v[UnityEngine.Random.Range(0, v.Length)].ProdId : string.Empty;
			}
			catch (Exception ex)
			{
				ErrorReporter.Report(ex, HandlingId, "Dummies.RandomCosmo: picking cosmetic");
				return string.Empty;
			}
		}

		private static string PickName()
		{
			const string abc = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
			var c = new char[6];
			for(int i = 0; i < c.Length; i++)
				c[i] = abc[UnityEngine.Random.Range(0, abc.Length)];
			return new string(c);
		}

		private static void Restamp()
		{
			bool host = AmongUsClient.Instance != null && Utils.isHost;
			foreach(var kv in _bots)
			{
				Dummy d = kv.Value;
				if(d == null || d.Pc == null) continue;
				if(host)
				{
					try { d.Pc.RpcSetColor(d.Color); } catch { }
					SetName(d.Pc, d.Name);
					try { d.Pc.RpcSetHat(d.Hat); } catch { }
					try { d.Pc.RpcSetSkin(d.Skin); } catch { }
					try { d.Pc.RpcSetVisor(d.Visor); } catch { }
				}
				else
				{
					try
					{
						if(d.Pc.cosmetics != null && d.Pc.cosmetics.nameText != null)
							d.Pc.cosmetics.nameText.text = d.Name;
					}
					catch { }
				}
			}
		}

		[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CmdCheckMurder))]
		internal static class DummyKillPatch
		{
			public static bool Prefix(PlayerControl __instance, PlayerControl target)
			{
				try
				{
					if(target == null || target.Data == null || !IsDummy(target.PlayerId)) return true;
					if(AmongUsClient.Instance == null || !Utils.isHost) return true;
					if(target.Data.IsDead) return false;
					__instance.RpcMurderPlayer(target, true);
					return false;
				}
				catch (Exception ex)
				{
					ErrorReporter.Report(ex, HandlingId, "Dummies.DummyKillPatch.Prefix: killing dummy");
					return true;
				}
			}
		}

		[HarmonyPatch(typeof(LobbyBehaviour), nameof(LobbyBehaviour.Start))]
		internal static class DummyResetPatch
		{
			public static void Postfix() => Forget();
		}
	}
}
