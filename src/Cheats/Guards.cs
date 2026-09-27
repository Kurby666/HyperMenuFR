using System;
using System.Collections.Generic;
using HarmonyLib;
using Hazel;
using BepInEx.Unity.IL2CPP;
using BepInEx.Unity.IL2CPP.Utils;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using Il2CppInterop.Runtime.Injection;
using InnerNet;
using UnityEngine;

namespace MalumMenu.Cheats
{
	// Ported from othermenu/Security/ForeignMods.cs. Detection only ever READS other clients'
	// incoming RPCs; it never sends anything. The user explicitly skipped othermenu's
	// "see other Nocturne users" handshake, so the toast is gated on its own toggle
	// (CheatToggles.modDetectToast) rather than on the handshake config.
	internal static class ForeignMods
	{
		internal const int HandlingId = 20075;
		private const byte VanillaTop = 90;

		private static readonly Dictionary<byte, string> Seen = new Dictionary<byte, string>();
		private static readonly Dictionary<int, int> Pending = new Dictionary<int, int>();
		private static readonly HashSet<byte> Toasted = new HashSet<byte>();

		internal static void Reset()
		{
			Seen.Clear();
			Pending.Clear();
			Toasted.Clear();
		}

		internal static string ModFor(byte pid) => Seen.TryGetValue(pid, out string m) ? m : null;

		private static bool Empty(MessageReader r)
		{
			try
			{
				return r == null || r.BytesRemaining == 0;
			}
			catch
			{
				return true;
			}
		}

		private static string KnownId(byte callId, MessageReader reader)
		{
			switch (callId)
			{
				case 164: return "SickoMenu";
				case 103: return "ASKIN";
				case 144:
				case 145: return "Gaff Menu";
				case 150: return "BetterAmongUs";
				case 169: return "Unknown";
				case 176: return "HostGuard";
				case 195:
				case 204: return "Polar Client";
				case 219:
				case 240: return "BanMod";
				case 250: return "KillNetwork";
				default: return null;
			}
		}

		private static bool LooksVersion(string v)
		{
			if (string.IsNullOrEmpty(v) || v.Length < 3 || v.Length > 16)
			{
				return false;
			}

			bool digit = false;
			bool dot = false;
			foreach (char ch in v)
			{
				if (ch >= '0' && ch <= '9')
				{
					digit = true;
				}
				else if (ch == '.')
				{
					dot = true;
				}
				else if (!char.IsLetter(ch) && ch != '-' && ch != '_')
				{
					return false;
				}
			}

			return digit && dot;
		}

		private static string Unknown(PlayerControl src, byte callId)
		{
			int key = (src.PlayerId << 8) | callId;
			Pending.TryGetValue(key, out int hits);
			hits++;
			Pending[key] = hits;
			return hits >= 2 ? "Unknown mod" : null;
		}

		private static void Mark(PlayerControl src, string mod)
		{
			Seen[src.PlayerId] = mod;
			EventLog.Add($"{EventLog.PName(src)} runs {mod}", "Foreign mod", EventCat.Join);
			if (CheatToggles.modDetectToast && Toasted.Add(src.PlayerId))
			{
				MalumMenu.notifications.Send("Foreign mod", $"{EventLog.PName(src)} runs {mod}", 4f);
			}
		}

		internal static void Inspect(PlayerControl src, byte callId, MessageReader reader)
		{
			if (src == null || src == PlayerControl.LocalPlayer || callId == 242)
			{
				return;
			}

			if (Seen.ContainsKey(src.PlayerId))
			{
				return;
			}

			MessageReader copy = null;
			try
			{
				string known = KnownId(callId, reader);
				if (known != null)
				{
					Mark(src, known);
					return;
				}

				if (callId <= VanillaTop)
				{
					return;
				}

				copy = MessageReader.Get(reader);

				if (callId == 212)
				{
					string s = copy.ReadString();
					copy.ReadBoolean();
					Mark(src, string.IsNullOrEmpty(s) || s.Length > 32 ? "BanMod" : s);
					return;
				}

				string sig = copy.ReadString();
				copy.ReadBoolean();
				string ver = copy.ReadString();

				string name = null;
				if (sig == "MMC")
				{
					name = "ModMenuCrew " + ver;
				}
				else if (LooksVersion(ver))
				{
					name = sig + " " + ver;
				}
				else
				{
					name = Unknown(src, callId);
				}

				if (name != null)
				{
					Mark(src, name);
				}
			}
			catch
			{
				// a malformed foreign payload is itself a reason to look twice
				try
				{
					string name = Unknown(src, callId);
					if (name != null)
					{
						Mark(src, name);
					}
				}
				catch { }
			}
			finally
			{
				try { copy?.Recycle(); } catch { }
			}
		}
	}

	// Ported from othermenu/Patches/RpcGuardPatches.cs.
	//
	// The host reads every incoming RPC and swallows the ones a vanilla client could never send.
	// Flagging is strike-based: two hits inside an 8s window escalate to AccessLists.Act, and a
	// per-client 6s cooldown keeps a single spammer from re-notifying every frame.
	internal static class RpcGuard
	{
		internal const int HandlingId = 20076;

		private const float StrikeWindow = 8f;
		private const int StrikeNeeded = 2;
		private const float NoteCooldown = 6f;

		private static readonly Dictionary<int, int> Strikes = new Dictionary<int, int>();
		private static readonly Dictionary<int, float> StrikeAt = new Dictionary<int, float>();
		private static readonly Dictionary<int, float> NotedAt = new Dictionary<int, float>();
		private static int shipId = -1;
		private static float shipAt;

		private static readonly SystemTypes[] SabSystems =
		{
			SystemTypes.Reactor,
			SystemTypes.LifeSupp,
			SystemTypes.Comms,
			SystemTypes.HeliSabotage,
			SystemTypes.Laboratory,
			SystemTypes.MushroomMixupSabotage,
		};

		internal static bool On()
		{
			return CheatToggles.rpcGuard && AmongUsClient.Instance is InnerNetClient net && net.AmHost;
		}

		internal static bool IsImp(PlayerControl pc)
		{
			try
			{
				return pc != null && pc.Data != null && pc.Data.Role != null && pc.Data.Role.IsImpostor;
			}
			catch
			{
				return false;
			}
		}

		internal static bool IsAlive(PlayerControl pc)
		{
			try
			{
				return pc != null && pc.Data != null && !pc.Data.IsDead;
			}
			catch
			{
				return false;
			}
		}

		internal static bool IsHS()
		{
			try
			{
				return GameManager.Instance != null && GameManager.Instance.IsHideAndSeek();
			}
			catch
			{
				return false;
			}
		}

		internal static bool RealVent(ShipStatus ship, int id)
		{
			try
			{
				if (ship?.AllVents == null)
				{
					return false;
				}

				var e = ship.AllVents.GetEnumerator();
				while (e.MoveNext())
				{
					Vent v = e.Current;
					if (v != null && v.Id == id)
					{
						return true;
					}
				}
			}
			catch { }

			return false;
		}

		internal static bool GraceOver()
		{
			ShipStatus ship = ShipStatus.Instance;
			if (ship == null)
			{
				return false;
			}

			int id = ship.GetInstanceID();
			if (id != shipId)
			{
				shipId = id;
				shipAt = Time.realtimeSinceStartup;
			}

			return Time.realtimeSinceStartup - shipAt > 3f;
		}

		internal static bool InSab(SystemTypes t)
		{
			for (int i = 0; i < SabSystems.Length; i++)
			{
				if (SabSystems[i] == t)
				{
					return true;
				}
			}

			return false;
		}

		internal static void Flag(PlayerControl actor, string reason)
		{
			EventLog.Fire("RPC guard", reason, CheatToggles.rpcGuardToast, EventCat.Other, 3.5f);
			if (actor == null)
			{
				return;
			}

			InnerNetClient net = AmongUsClient.Instance as InnerNetClient;
			if (net == null)
			{
				return;
			}

			int cid = actor.OwnerId;
			float now = Time.realtimeSinceStartup;
			if (NotedAt.TryGetValue(cid, out float last) && now - last < NoteCooldown)
			{
				return;
			}

			NotedAt[cid] = now;

			if (!StrikeAt.TryGetValue(cid, out float at) || now - at > StrikeWindow)
			{
				StrikeAt[cid] = now;
				Strikes[cid] = 1;
				return;
			}

			Strikes[cid] = Strikes.TryGetValue(cid, out int s) ? s + 1 : 1;
			if (Strikes[cid] >= StrikeNeeded)
			{
				Strikes[cid] = 0;
				AccessLists.Act(net, cid, AccessLists.RpcActionName(), AccessLists.ClientName(net, cid), reason);
			}
		}
	}

	// Ported from othermenu/Patches/NocturneAntiBan.cs: a client that boots a fake vent (op 2 with
	// 0 bytes and vent 0) and is then ejected within a second is running the vent-kick ban exploit.
	internal static class AntiBan
	{
		internal const int HandlingId = 20077;
		private const float Window = 1f;
		private static readonly Dictionary<int, float> Pending = new Dictionary<int, float>();

		// 0 = clean, 1 = saw the setup, 2 = saw the kill -> block
		internal static int Inspect(MessageReader reader, out PlayerControl actor)
		{
			actor = null;
			MessageReader copy = null;
			try
			{
				copy = MessageReader.Get(reader);
				if (copy.ReadByte() != (byte)SystemTypes.Ventilation)
				{
					return 0;
				}

				PlayerControl src = copy.ReadNetObject<PlayerControl>();
				if (src == null || src == PlayerControl.LocalPlayer || src.OwnerId < 0)
				{
					return 0;
				}

				actor = src;
				ushort num = copy.ReadUInt16();
				byte op = copy.ReadByte();
				byte vid = copy.ReadByte();

				if (op == 2 && num == 0 && vid == 0)
				{
					Pending[src.OwnerId] = Time.realtimeSinceStartup;
					return 1;
				}

				if (op != 5 || num != 1 || vid != 0)
				{
					return 0;
				}

				return Pending.TryGetValue(src.OwnerId, out float at) && Time.realtimeSinceStartup - at <= Window ? 2 : 0;
			}
			catch
			{
				return 0;
			}
			finally
			{
				try { copy?.Recycle(); } catch { }
			}
		}
	}

	// Ported from othermenu/Patches/NocturneVentTpProtect.cs + NocturneZiplineProtect.cs.
	// Blocks someone force-moving the local player: a raw vent boot, a forced ventilation update,
	// or a zipline ride the local player never asked for.
	//
	// DEVIATION: othermenu also granted a 1.5s grace from a RpcUseZipline prefix. src has no
	// reference to RpcUseZipline (the Fungle zipline rides through RpcCalls.UseZipline on
	// PlayerControl), so the grace is granted from CmdCheckUseZipline only — the console click,
	// which is the only legitimate way the local player starts their own ride.
	internal static class AntiForce
	{
		internal const int HandlingId = 20078;

		private static float ventNoteAt;
		private static float zipNoteAt;
		private static float zipOkUntil;

		internal static bool VentOn => CheatToggles.blockForcedVents;
		internal static bool ZipOn => CheatToggles.blockForcedZipline;

		internal static void NoteVent(string target)
		{
			float now = Time.realtimeSinceStartup;
			if (now - ventNoteAt < 30f)
			{
				return;
			}

			ventNoteAt = now;
			EventLog.Fire("Protection", $"a vent boot on {target} was blocked", true, EventCat.Vent, 3.5f);
		}

		internal static void NoteZip(string target)
		{
			float now = Time.realtimeSinceStartup;
			if (now - zipNoteAt < 2f)
			{
				return;
			}

			zipNoteAt = now;
			EventLog.Fire("Protection", $"a zipline ride on {target} was blocked", true, EventCat.Vent, 3.5f);
		}

		internal static void AllowZipline() => zipOkUntil = Time.realtimeSinceStartup + 1.5f;

		internal static bool ZiplineBlocked(PlayerControl target)
		{
			return ZipOn && target != null && target == PlayerControl.LocalPlayer && Time.realtimeSinceStartup > zipOkUntil;
		}
	}

	// Ported from othermenu/Security/NocturneAntiFakeMeeting.cs + NocturneSpawnFlood.cs.
	internal static class FloodGuard
	{
		internal const int HandlingId = 20079;

		private const int Cap = 200;
		private const float GraceSeconds = 5f;

		private static int lastFrame = -1;
		private static int count;
		private static float lastNote;
		private static float graceUntil;

		internal static void Grace() => graceUntil = Time.realtimeSinceStartup + GraceSeconds;

		internal static bool Allow()
		{
			if (Time.realtimeSinceStartup < graceUntil)
			{
				return true;
			}

			int frame = Time.frameCount;
			if (frame != lastFrame)
			{
				lastFrame = frame;
				count = 0;
			}

			count++;
			return count <= Cap;
		}

		internal static Il2CppSystem.Collections.IEnumerator Drop()
		{
			float now = Time.realtimeSinceStartup;
			if (now - lastNote > 1f)
			{
				lastNote = now;
				EventLog.Fire("Guard", "Trimmed a spawn flood", CheatToggles.securityNotify, EventCat.Other, 3.5f);
			}

			count = 0;
			// WrapToIl2Cpp ships in BepInEx.Unity.IL2CPP (namespace BepInEx.Unity.IL2CPP.Utils) and
			// extends IEnumerator, so an empty managed list's enumerator converts to an il2cpp
			// enumerator - a coroutine that yields nothing.
			return new List<object>().GetEnumerator().WrapToIl2Cpp();
		}
	}

	[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.HandleRpc))]
	[HarmonyPriority(Priority.First)]
	internal static class Guard_PlayerRpcPatch
	{
		public static bool Prefix(PlayerControl __instance, byte callId, MessageReader reader)
		{
			try
			{
				ForeignMods.Inspect(__instance, callId, reader);
			}
			catch { }

			string who = EventLog.PName(__instance);

			if (AntiForce.ZipOn && callId == (byte)RpcCalls.UseZipline && AntiForce.ZiplineBlocked(__instance))
			{
				AntiForce.NoteZip(who);
				return false;
			}

			if (!RpcGuard.On())
			{
				return true;
			}

			int pos = reader != null ? reader.Position : 0;
			bool block = false;

			try
			{
				if (callId == (byte)RpcCalls.SetScanner)
				{
					if (reader.ReadBoolean() && RpcGuard.IsImp(__instance))
					{
						RpcGuard.Flag(__instance, "scan by impostor");
						block = true;
					}
				}
				else if (callId == (byte)RpcCalls.PlayAnimation)
				{
					byte anim = reader.ReadByte();
					if (anim < 4 && RpcGuard.IsImp(__instance))
					{
						RpcGuard.Flag(__instance, "task anim by impostor");
						block = true;
					}
				}
				else if (callId == (byte)RpcCalls.ReportDeadBody && RpcGuard.IsHS())
				{
					RpcGuard.Flag(__instance, "report in H&S");
					block = true;
				}
			}
			catch
			{
				block = false;
			}
			finally
			{
				if (reader != null && pos >= 0)
				{
					try { reader.Position = pos; } catch { }
				}
			}

			if (block)
			{
				return false;
			}

			return true;
		}
	}

	[HarmonyPatch(typeof(PlayerPhysics), nameof(PlayerPhysics.HandleRpc))]
	[HarmonyPriority(Priority.First)]
	internal static class Guard_PlayerPhysicsRpcPatch
	{
		public static bool Prefix(byte callId, MessageReader reader)
		{
			if (AntiForce.VentOn && callId == (byte)RpcCalls.BootFromVent)
			{
				AntiForce.NoteVent("you");
				return false;
			}

			if (!RpcGuard.On())
			{
				return true;
			}

			PlayerControl actor = null;
			try
			{
				actor = PlayerControl.LocalPlayer;
			}
			catch { }

			int pos = reader != null ? reader.Position : 0;
			bool block = false;

			try
			{
				if (callId == (byte)RpcCalls.EnterVent || callId == (byte)RpcCalls.ExitVent)
				{
					int vid = reader.ReadPackedInt32();
					if (!RpcGuard.RealVent(ShipStatus.Instance, vid))
					{
						RpcGuard.Flag(actor, "fake vent id");
						block = true;
					}
					else if (RpcGuard.GraceOver() && RpcGuard.IsAlive(actor) && !CanVent(actor))
					{
						RpcGuard.Flag(actor, "vent without permission");
						block = true;
					}
				}
			}
			catch
			{
				block = false;
			}
			finally
			{
				if (reader != null && pos >= 0)
				{
					try { reader.Position = pos; } catch { }
				}
			}

			if (block)
			{
				return false;
			}

			return true;
		}

		private static bool CanVent(PlayerControl pc)
		{
			try
			{
				return pc != null && pc.Data != null && pc.Data.Role != null && pc.Data.Role.CanVent;
			}
			catch
			{
				return false;
			}
		}
	}

	[HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.HandleRpc))]
	[HarmonyPriority(Priority.First)]
	internal static class Guard_ShipRpcPatch
	{
		public static bool Prefix(ShipStatus __instance, byte callId, MessageReader reader)
		{
			if (callId == (byte)RpcCalls.CloseDoorsOfType)
			{
				if (RpcGuard.On() && RpcGuard.IsHS())
				{
					RpcGuard.Flag(null, "doors in H&S");
					return false;
				}

				return true;
			}

			if (callId != (byte)RpcCalls.UpdateSystem)
			{
				return true;
			}

			int pos = reader != null ? reader.Position : 0;
			bool block = false;
			PlayerControl actor = null;

			try
			{
				// anti-ban inspects the raw payload independently of the guard rules below
				if (AntiBan.Inspect(reader, out PlayerControl abActor) == 2)
				{
					InnerNetClient abNet = AmongUsClient.Instance as InnerNetClient;
					if (abNet != null && !abNet.AmHost)
					{
						EventLog.Fire("Anti-ban", "Vent-kick blocked.", true, EventCat.Vent, 3.5f);
						block = true;
					}
					else if (abNet != null && abNet.AmHost && CheatToggles.antiBanHost)
					{
						AccessLists.Act(abNet, abActor != null ? abActor.OwnerId : -1, AccessLists.RpcActionName(), AccessLists.ClientName(abNet, abActor != null ? abActor.OwnerId : -1), "vent-kick exploit");
						block = true;
					}
				}
			}
			catch { }

			if (!block && AntiForce.VentOn)
			{
				try
				{
					MessageReader peek = MessageReader.Get(reader);
					if (peek.ReadByte() == (byte)SystemTypes.Ventilation)
					{
						peek.ReadNetObject<PlayerControl>();
						peek.ReadUInt16();
						byte op = peek.ReadByte();
						if (op == 1 || op == 2)
						{
							AntiForce.NoteVent("you");
							block = true;
						}
					}

					peek.Recycle();
				}
				catch { }
			}

			if (!block && RpcGuard.On())
			{
				try
				{
					byte sys = reader.ReadByte();
					actor = reader.ReadNetObject<PlayerControl>();
					var st = (SystemTypes)sys;

					if (st == SystemTypes.Ventilation)
					{
						reader.ReadUInt16();
						byte op = reader.ReadByte();
						byte vid = reader.ReadByte();
						if (op == 2)
						{
							RpcGuard.Flag(actor, !RpcGuard.RealVent(__instance, vid) ? "fake vent id" : "vent for another");
							block = true;
						}
					}
					else if (RpcGuard.InSab(st))
					{
						if (RpcGuard.IsHS() && st != SystemTypes.MushroomMixupSabotage)
						{
							RpcGuard.Flag(actor, "sabotage in H&S");
							block = true;
						}
						else
						{
							byte amount = reader.ReadByte();
							bool known = __instance != null && __instance.Systems != null && __instance.Systems.ContainsKey(st);
							if (!known)
							{
								RpcGuard.Flag(actor, "sabotage off-map");
								block = true;
							}
							else if (RpcGuard.IsAlive(actor) && !RpcGuard.IsImp(actor)
								&& (st == SystemTypes.MushroomMixupSabotage || (amount & 0x80) != 0))
							{
								RpcGuard.Flag(actor, "sabotage by crew");
								block = true;
							}
						}
					}
				}
				catch
				{
					block = false;
				}
			}

			if (reader != null && pos >= 0)
			{
				try { reader.Position = pos; } catch { }
			}

			if (block)
			{
				return false;
			}

			return true;
		}
	}

	[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CmdCheckUseZipline))]
	internal static class AntiForce_ZipOkPatch
	{
		public static void Prefix()
		{
			AntiForce.AllowZipline();
		}
	}

	[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Start))]
	internal static class Guard_FakeMeetingPatch
	{
		public static bool Prefix(MeetingHud __instance)
		{
			try
			{
				if (!CheatToggles.blockFakeMeetings)
				{
					return true;
				}

				// a meeting in the lobby, or with no ship, is not a real one
				if (ShipStatus.Instance != null && LobbyBehaviour.Instance == null)
				{
					return true;
				}

				if (__instance != null)
				{
					UnityEngine.Object.Destroy(__instance.gameObject);
				}

				EventLog.Fire("Guard", "Blocked a fake lobby meeting", CheatToggles.securityNotify, EventCat.Meeting, 3.5f);
				return false;
			}
			catch
			{
				return true;
			}
		}
	}

	[HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.CoHandleSpawn))]
	internal static class Guard_SpawnFloodPatch
	{
		public static bool Prefix(ref Il2CppSystem.Collections.IEnumerator __result)
		{
			try
			{
				if (!CheatToggles.blockSpawnFloods || FloodGuard.Allow())
				{
					return true;
				}

				__result = FloodGuard.Drop();
				return false;
			}
			catch
			{
				return true;
			}
		}
	}

	[HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnGameJoined))]
	internal static class Guard_GraceJoinPatch
	{
		public static void Prefix()
		{
			FloodGuard.Grace();
		}
	}

	[HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnGameJoined))]
	internal static class Guard_ModResetPatch
	{
		public static void Postfix()
		{
			ForeignMods.Reset();
		}
	}

	[HarmonyPatch(typeof(LobbyBehaviour), nameof(LobbyBehaviour.Start))]
	internal static class Guard_GraceLobbyPatch
	{
		public static void Prefix()
		{
			FloodGuard.Grace();
		}
	}

	[HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.Start))]
	internal static class Guard_GraceShipPatch
	{
		public static void Prefix()
		{
			FloodGuard.Grace();
		}
	}
}
