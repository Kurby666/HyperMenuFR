using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using HarmonyLib;
using Hazel;
using InnerNet;
using UnityEngine;

namespace MalumMenu.Cheats
{
	// Ported from othermenu/Security/HyperAccess.cs + othermenu/Patches/JoinLevelPatches.cs
	// (HyperJoinLevels) + the two enforcement hooks in othermenu/Patches/AccessGuardPatches.cs
	// (OnPlayerJoined postfix and the HyperAccessGuard Update loop).
	//
	// Deliberately NOT ported from AccessGuardPatches.cs:
	//   * HyperVoteKickPatch  -> already covered by Cheats/VotekickGuard.cs (host punish-voter
	//     prefix on VoteBanSystem.AddVote with Null/Warn/Kick/Ban).
	//   * HyperKickSelfGuardPatch -> already covered by the self-kick block in VotekickGuard.
	// Porting either again would put two prefixes on the same method.
	//
	// Deliberately NOT ported: othermenu/Security/HyperGate.cs. Despite the name it is not a
	// join gate at all - it hashes the local PUID and hard-disables the mod when the hash is on a
	// hardcoded blocklist. That is the othermenu author's piracy/licensing check, not a menu
	// feature, so it has no place in HyperMenu.
	//
	// Note: min-level also overlaps src/features/Host.cs BlockLowLevels (Host-Only 2 tab). That one
	// is left untouched; this gate adds the max-level half plus a per-action choice, and both
	// honour the same "only if the toggle is on" contract so enabling both just kicks twice.
	internal sealed class AccessEntry
	{
		internal string Name;
		internal string Code;
		internal string Puid;
	}

	internal static class AccessLists
	{
		internal const int HandlingId = 20071;

		private static readonly List<AccessEntry> Bans = new List<AccessEntry>();
		private static readonly List<AccessEntry> Whites = new List<AccessEntry>();
		private static readonly List<string> NickBans = new List<string>();
		private static readonly List<string> PlatformBans = new List<string>();
		private static readonly Dictionary<int, float> ActedAt = new Dictionary<int, float>();
		private const float ActCooldown = 6f;
		private static float nextScanAt;

		private static string Dir => Path.Combine(BepInEx.Paths.GameRootPath, "HyperMenu");
		private static string BanTxt => Path.Combine(Dir, "BanList.txt");
		private static string WhiteTxt => Path.Combine(Dir, "WhiteList.txt");
		private static string NickTxt => Path.Combine(Dir, "NickBanList.txt");
		private static string PlatformTxt => Path.Combine(Dir, "PlatformBanList.txt");

		private const string EntryFormat = "# HyperMenu — format: Name | FriendCode | PUID";
		private const string NickFormat = "# HyperMenu — one nick per line";
		private const string PlatformFormat = "# HyperMenu — one platform name per line";

		// ---------------------------------------------------------------- file format helpers

		private static string Esc(string s) => string.IsNullOrEmpty(s) ? string.Empty : s.Replace(';', ' ').Replace('|', ' ').Trim();

		private static void ParsePlatforms(List<string> into)
		{
			into.Clear();
			foreach (string item in ReadLines(PlatformTxt))
			{
				string n = ColorTools.StripTags(item).Trim();
				if (n.Length > 0 && !HasPlatform(into, n))
				{
					into.Add(n);
				}
			}
		}

		private static void ParseNicks(List<string> into)
		{
			into.Clear();
			foreach (string item in ReadLines(NickTxt))
			{
				string n = ColorTools.StripTags(item).Trim();
				if (n.Length > 0 && !HasNick(into, n))
				{
					into.Add(n);
				}
			}
		}

		private static void Parse(List<AccessEntry> into)
		{
			into.Clear();
			foreach (string item in ReadLines(BanTxt))
			{
				string[] p = item.Split('|');
				string name = p.Length >= 1 ? p[0].Trim() : string.Empty;
				string code = p.Length >= 2 ? p[1].Trim() : string.Empty;
				string puid = p.Length >= 3 ? p[2].Trim() : string.Empty;
				if ((code.Length > 0 || puid.Length > 0) && !Has(into, code, puid))
				{
					into.Add(new AccessEntry { Name = name, Code = code, Puid = puid });
				}
			}
		}

		private static void ParseWhite(List<AccessEntry> into)
		{
			into.Clear();
			foreach (string item in ReadLines(WhiteTxt))
			{
				string[] p = item.Split('|');
				string name = p.Length >= 1 ? p[0].Trim() : string.Empty;
				string code = p.Length >= 2 ? p[1].Trim() : string.Empty;
				string puid = p.Length >= 3 ? p[2].Trim() : string.Empty;
				if ((code.Length > 0 || puid.Length > 0) && !Has(into, code, puid))
				{
					into.Add(new AccessEntry { Name = name, Code = code, Puid = puid });
				}
			}
		}

		// othermenu kept these lists in string-valued config entries (';' separated) and mirrored
		// them to TXT. src has no string config entries, so the TXT file is the single source of
		// truth and is re-read on every change. Same on-disk format, same '#' header convention.
		private static IEnumerable<string> ReadLines(string path)
		{
			List<string> outLines = new List<string>();
			try
			{
				if (!File.Exists(path))
				{
					return outLines;
				}

				foreach (string raw in File.ReadAllLines(path))
				{
					string line = raw.Trim();
					if (line.Length == 0 || line[0] == '#')
					{
						continue;
					}

					outLines.Add(line);
				}
			}
			catch { }

			return outLines;
		}

		private static void WriteEntries(string path, List<AccessEntry> list)
		{
			try
			{
				Directory.CreateDirectory(Dir);
				StringBuilder sb = new StringBuilder();
				sb.AppendLine(EntryFormat);
				for (int i = 0; i < list.Count; i++)
				{
					sb.Append(list[i].Name).Append(" | ").Append(list[i].Code).Append(" | ").Append(list[i].Puid).Append('\n');
				}

				File.WriteAllText(path, sb.ToString());
			}
			catch { }
		}

		private static void WriteLines(string path, List<string> list, string header)
		{
			try
			{
				Directory.CreateDirectory(Dir);
				StringBuilder sb = new StringBuilder();
				sb.AppendLine(header);
				for (int i = 0; i < list.Count; i++)
				{
					sb.Append(list[i]).Append('\n');
				}

				File.WriteAllText(path, sb.ToString());
			}
			catch { }
		}

		private static bool Has(List<AccessEntry> list, string code, string puid)
		{
			for (int i = 0; i < list.Count; i++)
			{
				AccessEntry e = list[i];
				if (!string.IsNullOrEmpty(code) && string.Equals(e.Code, code, StringComparison.OrdinalIgnoreCase))
				{
					return true;
				}

				if (!string.IsNullOrEmpty(puid) && string.Equals(e.Puid, puid, StringComparison.OrdinalIgnoreCase))
				{
					return true;
				}
			}

			return false;
		}

		private static string NickKey(string name)
		{
			string s = ColorTools.StripTags(name);
			if (string.IsNullOrEmpty(s))
			{
				return string.Empty;
			}

			StringBuilder sb = new StringBuilder(s.Length);
			foreach (char ch in s.ToLowerInvariant())
			{
				if (char.IsLetterOrDigit(ch))
				{
					sb.Append(ch);
				}
			}

			return sb.ToString();
		}

		private static bool HasNick(List<string> list, string name)
		{
			string key = NickKey(name);
			if (key.Length == 0)
			{
				return false;
			}

			for (int i = 0; i < list.Count; i++)
			{
				if (NickKey(list[i]) == key)
				{
					return true;
				}
			}

			return false;
		}

		private static string PlatformKey(string raw)
		{
			string s = ColorTools.StripTags(raw);
			return string.IsNullOrEmpty(s) ? string.Empty : s.Trim().ToLowerInvariant();
		}

		private static bool HasPlatform(List<string> list, string raw)
		{
			string key = PlatformKey(raw);
			if (key.Length == 0)
			{
				return false;
			}

			for (int i = 0; i < list.Count; i++)
			{
				if (PlatformKey(list[i]) == key)
				{
					return true;
				}
			}

			return false;
		}

		// ---------------------------------------------------------------- public surface

		internal static List<AccessEntry> BanEntries
		{
			get
			{
				Parse(Bans);
				return Bans;
			}
		}

		internal static List<AccessEntry> WhiteEntries
		{
			get
			{
				ParseWhite(Whites);
				return Whites;
			}
		}

		internal static List<string> NickBanEntries
		{
			get
			{
				ParseNicks(NickBans);
				return NickBans;
			}
		}

		internal static List<string> PlatformBanEntries
		{
			get
			{
				ParsePlatforms(PlatformBans);
				return PlatformBans;
			}
		}

		internal static bool IsBanned(string fc, string puid) => Has(BanEntries, fc, puid);
		internal static bool IsWhite(string fc, string puid) => Has(WhiteEntries, fc, puid);
		internal static bool IsNickBanned(string name) => HasNick(NickBanEntries, name);
		internal static bool IsPlatformBanned(string raw) => HasPlatform(PlatformBanEntries, raw);

		internal static bool IsBotPlatform(string raw) => !string.IsNullOrEmpty(raw) && raw.IndexOf("bot", StringComparison.OrdinalIgnoreCase) >= 0;

		internal static void AddBan(string name, string fc, string puid)
		{
			List<AccessEntry> list = BanEntries;
			if ((string.IsNullOrWhiteSpace(fc) && string.IsNullOrWhiteSpace(puid)) || Has(list, fc, puid))
			{
				return;
			}

			list.Add(new AccessEntry { Name = name ?? string.Empty, Code = (fc ?? string.Empty).Trim(), Puid = (puid ?? string.Empty).Trim() });
			WriteEntries(BanTxt, list);
		}

		internal static void AddWhite(string name, string fc, string puid)
		{
			List<AccessEntry> list = WhiteEntries;
			if ((string.IsNullOrWhiteSpace(fc) && string.IsNullOrWhiteSpace(puid)) || Has(list, fc, puid))
			{
				return;
			}

			list.Add(new AccessEntry { Name = name ?? string.Empty, Code = (fc ?? string.Empty).Trim(), Puid = (puid ?? string.Empty).Trim() });
			WriteEntries(WhiteTxt, list);
		}

		internal static void RemoveByKey(List<AccessEntry> list, string key, string path)
		{
			if (string.IsNullOrEmpty(key))
			{
				return;
			}

			for (int i = list.Count - 1; i >= 0; i--)
			{
				if (string.Equals(list[i].Code, key, StringComparison.OrdinalIgnoreCase) || string.Equals(list[i].Puid, key, StringComparison.OrdinalIgnoreCase))
				{
					list.RemoveAt(i);
					WriteEntries(path, list);
					return;
				}
			}
		}

		internal static void RemoveBan(string key) => RemoveByKey(BanEntries, key, BanTxt);
		internal static void RemoveWhite(string key) => RemoveByKey(WhiteEntries, key, WhiteTxt);

		internal static void ClearBans()
		{
			Bans.Clear();
			WriteEntries(BanTxt, Bans);
		}

		internal static void ClearWhites()
		{
			Whites.Clear();
			WriteEntries(WhiteTxt, Whites);
		}

		internal static void AddNickBan(string name)
		{
			List<string> list = NickBanEntries;
			string clean = ColorTools.StripTags(name).Trim();
			if (clean.Length == 0 || HasNick(list, clean))
			{
				return;
			}

			list.Add(clean);
			WriteLines(NickTxt, list, NickFormat);
		}

		internal static void RemoveNickBan(string name)
		{
			List<string> list = NickBanEntries;
			string key = NickKey(name);
			for (int i = list.Count - 1; i >= 0; i--)
			{
				if (NickKey(list[i]) == key)
				{
					list.RemoveAt(i);
					WriteLines(NickTxt, list, NickFormat);
					return;
				}
			}
		}

		internal static void ClearNickBans()
		{
			NickBans.Clear();
			WriteLines(NickTxt, NickBans, NickFormat);
		}

		internal static void AddPlatformBan(string raw)
		{
			List<string> list = PlatformBanEntries;
			string clean = ColorTools.StripTags(raw).Trim();
			if (clean.Length == 0 || HasPlatform(list, clean))
			{
				return;
			}

			list.Add(clean);
			WriteLines(PlatformTxt, list, PlatformFormat);
		}

		internal static void RemovePlatformBan(string raw)
		{
			List<string> list = PlatformBanEntries;
			string key = PlatformKey(raw);
			for (int i = list.Count - 1; i >= 0; i--)
			{
				if (PlatformKey(list[i]) == key)
				{
					list.RemoveAt(i);
					WriteLines(PlatformTxt, list, PlatformFormat);
					return;
				}
			}
		}

		internal static void ClearPlatformBans()
		{
			PlatformBans.Clear();
			WriteLines(PlatformTxt, PlatformBans, PlatformFormat);
		}

		// ---------------------------------------------------------------- host actions

		internal static void Kick(InnerNetClient net, int clientId, bool ban)
		{
			try
			{
				if (net == null || !net.AmHost)
				{
					return;
				}

				if (clientId < 0 || clientId == net.ClientId || clientId == net.HostId)
				{
					return;
				}

				net.KickPlayer(clientId, ban);
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "AccessLists.Kick"); }
		}

		internal static void Act(InnerNetClient net, int clientId, string action, string who, string reason)
		{
			switch ((action ?? "Null").Trim().ToLowerInvariant())
			{
				case "warn":
					MalumMenu.notifications.Send("Protection", $"{who}: {reason}", 3f);
					break;
				case "kick":
					Kick(net, clientId, false);
					MalumMenu.notifications.Send("Kick", $"{who}: {reason}", 3f);
					break;
				case "ban":
					ClientData c = FindClient(net, clientId);
					if (c != null)
					{
						AddBan(SafeName(c), SafeFc(c), SafePuid(c));
					}

					Kick(net, clientId, true);
					MalumMenu.notifications.Send("Ban", $"{who}: {reason}", 3f);
					break;
			}
		}

		internal static void BanClient(InnerNetClient net, ClientData c)
		{
			if (c == null)
			{
				return;
			}

			AddBan(SafeName(c), SafeFc(c), SafePuid(c));
			Kick(net, c.Id, true);
			MalumMenu.notifications.Send("Ban list", SafeName(c), 2.5f);
		}

		internal static void WhiteClient(ClientData c)
		{
			if (c == null)
			{
				return;
			}

			AddWhite(SafeName(c), SafeFc(c), SafePuid(c));
			MalumMenu.notifications.Send("Whitelist", SafeName(c), 2.5f);
		}

		internal static void NickBanClient(InnerNetClient net, ClientData c)
		{
			if (c == null)
			{
				return;
			}

			AddNickBan(SafeName(c));
			Kick(net, c.Id, true);
			MalumMenu.notifications.Send("Nick ban", SafeName(c), 2.5f);
		}

		// ---------------------------------------------------------------- join gate

		internal static void Enforce(InnerNetClient net, ClientData client)
		{
			try
			{
				if (net == null || !net.AmHost || client == null)
				{
					return;
				}

				if (client.Id < 0 || client.Id == net.ClientId || client.Id == net.HostId)
				{
					return;
				}

				string fc = SafeFc(client);
				string puid = SafePuid(client);
				if (IsWhite(fc, puid))
				{
					return;
				}

				if (OnCooldown(client.Id))
				{
					return;
				}

				if (CheatToggles.accessBanEnabled && IsBanned(fc, puid))
				{
					Touch(client.Id);
					Kick(net, client.Id, true);
					MalumMenu.notifications.Send("Ban list", SafeName(client), 3f);
					return;
				}

				if (CheatToggles.accessNickBanEnabled && IsNickBanned(SafeName(client)))
				{
					Touch(client.Id);
					Kick(net, client.Id, true);
					MalumMenu.notifications.Send("Nick ban", SafeName(client), 3f);
					return;
				}

				if (CheatToggles.accessPlatformBanEnabled && IsPlatformBanned(SafePlatform(client)))
				{
					Touch(client.Id);
					Kick(net, client.Id, true);
					MalumMenu.notifications.Send("Platform ban", SafeName(client), 3f);
					return;
				}

				if (IsBotPlatform(SafePlatform(client)))
				{
					Touch(client.Id);
					Kick(net, client.Id, true);
					MalumMenu.notifications.Send("Bot", SafeName(client), 3f);
					return;
				}

				if (CheatToggles.accessWhitelistOnly && WhiteEntries.Count > 0 && !IsWhite(fc, puid))
				{
					Touch(client.Id);
					Kick(net, client.Id, false);
					MalumMenu.notifications.Send("Not whitelisted", SafeName(client), 3f);
					return;
				}

				if (JoinLevels.TryLevel(client.Id, client.Character, out int lvl))
				{
					if (CheatToggles.minLevelEnabled && lvl < CheatToggles.minLevel)
					{
						Touch(client.Id);
						Act(net, client.Id, LevelActionName(), SafeName(client), $"lvl {lvl} < {CheatToggles.minLevel}");
						return;
					}

					if (CheatToggles.maxLevelEnabled && lvl > CheatToggles.maxLevel)
					{
						Touch(client.Id);
						Act(net, client.Id, LevelActionName(), SafeName(client), $"lvl {lvl} > {CheatToggles.maxLevel}");
					}
				}
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "AccessLists.Enforce"); }
		}

		private static readonly string[] LevelActions = { "Null", "Warn", "Kick", "Ban" };

		internal static string LevelActionName() => LevelActions[Mathf.Clamp(CheatToggles.levelActionIdx, 0, LevelActions.Length - 1)];

		internal static string LevelActionLabel() => LevelActionName() switch
		{
			"Null" => "Do nothing",
			"Warn" => "Warn only",
			"Kick" => "Kick",
			_ => "Ban",
		};

		internal static void CycleLevelAction()
		{
			CheatToggles.levelActionIdx = (CheatToggles.levelActionIdx + 1) % LevelActions.Length;
		}

		internal static readonly string[] Actions = { "Null", "Warn", "Kick", "Ban" };

		internal static string RpcActionName() => Actions[Mathf.Clamp(CheatToggles.rpcGuardActionIdx, 0, Actions.Length - 1)];

		internal static string RpcActionLabel() => RpcActionName() switch
		{
			"Null" => "Do nothing",
			"Warn" => "Warn only",
			"Kick" => "Kick",
			_ => "Ban",
		};

		internal static void CycleRpcAction()
		{
			CheatToggles.rpcGuardActionIdx = (CheatToggles.rpcGuardActionIdx + 1) % Actions.Length;
		}

		private static bool OnCooldown(int id) => ActedAt.TryGetValue(id, out float t) && Time.realtimeSinceStartup - t < ActCooldown;

		private static void Touch(int id) => ActedAt[id] = Time.realtimeSinceStartup;

		// ---------------------------------------------------------------- ClientData helpers

		internal static ClientData FindClient(InnerNetClient net, int clientId)
		{
			try
			{
				if (net == null || net.allClients == null)
				{
					return null;
				}

				var e = net.allClients.GetEnumerator();
				while (e.MoveNext())
				{
					ClientData c = e.Current;
					if (c != null && c.Id == clientId)
					{
						return c;
					}
				}
			}
			catch { }

			return null;
		}

		internal static string ClientName(InnerNetClient net, int clientId)
		{
			ClientData c = FindClient(net, clientId);
			return c != null ? SafeName(c) : "#" + clientId;
		}

		internal static string SafeName(ClientData c)
		{
			try
			{
				if (c != null && !string.IsNullOrWhiteSpace(c.PlayerName))
				{
					return c.PlayerName.Trim();
				}
			}
			catch { }

			return c != null ? "#" + c.Id : "?";
		}

		internal static string SafeFc(ClientData c)
		{
			try
			{
				return c.FriendCode ?? string.Empty;
			}
			catch
			{
				return string.Empty;
			}
		}

		internal static string SafePuid(ClientData c)
		{
			try
			{
				return c.ProductUserId ?? string.Empty;
			}
			catch
			{
				return string.Empty;
			}
		}

		internal static string SafePlatform(ClientData c)
		{
			return c != null && c.PlatformData != null ? (c.PlatformData.PlatformName ?? string.Empty).Trim() : string.Empty;
		}

		internal static string PlatformLabel(ClientData c)
		{
			string raw = SafePlatform(c);
			if (raw.Length == 0)
			{
				return "?";
			}

			try
			{
				return c.PlatformData != null ? Utils.PlatformTypeToString(c.PlatformData.Platform) : raw;
			}
			catch
			{
				return raw;
			}
		}

		// ---------------------------------------------------------------- periodic enforcement

		// Port of HyperAccessGuard.Update. Colour reservations are NOT re-applied from here:
		// Cheats/ColorTools.cs ReservationTick already runs a 1s idempotent host poll that applies
		// them on join, so calling both would just do the same work twice.
		internal static void Tick()
		{
			float now = Time.realtimeSinceStartup;
			if (now < nextScanAt)
			{
				return;
			}

			nextScanAt = now + 0.6f;

			if (AmongUsClient.Instance == null || LobbyBehaviour.Instance == null)
			{
				return;
			}

			InnerNetClient net = AmongUsClient.Instance as InnerNetClient;
			if (net == null || !net.AmHost || net.allClients == null)
			{
				return;
			}

			bool fg = CheatToggles.kickFortegreen;
			if (!fg
				&& !CheatToggles.accessBanEnabled
				&& !CheatToggles.accessWhitelistOnly
				&& !CheatToggles.accessNickBanEnabled
				&& !CheatToggles.accessPlatformBanEnabled
				&& !CheatToggles.minLevelEnabled
				&& !CheatToggles.maxLevelEnabled)
			{
				return;
			}

			try
			{
				var e = net.allClients.GetEnumerator();
				while (e.MoveNext())
				{
					ClientData c = e.Current;
					if (c == null)
					{
						continue;
					}

					if (fg && c.Id != net.ClientId && c.Id != net.HostId && IsFortegreen(c))
					{
						Kick(net, c.Id, false);
						MalumMenu.notifications.Send("Fortegreen", SafeName(c), 2.5f);
						continue;
					}

					Enforce(net, c);
				}
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "AccessLists.Tick"); }
		}

		private static bool IsFortegreen(ClientData c)
		{
			try
			{
				return c.Character != null && c.Character.CurrentOutfit != null && c.Character.CurrentOutfit.ColorId == 18;
			}
			catch
			{
				return false;
			}
		}
	}

	// Ported verbatim from othermenu/Patches/JoinLevelPatches.cs (HyperJoinLevels).
	// A joining client sends its level before its character exists, so the raw value is cached
	// off both the player id and the InnerNet client id; every later read prefers the live value
	// and falls back to the cache.
	internal static class JoinLevels
	{
		private const uint MaxRaw = 9999u;
		private const byte TempId = 100;
		private const float SettleGap = 0.75f;

		private static readonly Dictionary<byte, uint> ByPlayerId = new Dictionary<byte, uint>();
		private static readonly Dictionary<int, uint> ByClientId = new Dictionary<int, uint>();
		private static readonly Dictionary<int, float> LoadedAt = new Dictionary<int, float>();

		private static bool ValidRaw(uint raw) => raw != uint.MaxValue && raw <= MaxRaw;

		internal static void Reset()
		{
			ByPlayerId.Clear();
			ByClientId.Clear();
			LoadedAt.Clear();
		}

		private static int OwnerOf(PlayerControl player) => player != null ? player.OwnerId : -1;

		private static bool BlankName(string name)
		{
			string v = (name ?? string.Empty).Trim();
			return v.Length == 0 || v == "??" || v == "???" || v.Equals("Player", StringComparison.OrdinalIgnoreCase);
		}

		private static bool OutfitReady(PlayerControl pc)
		{
			try
			{
				if (pc.Data.DefaultOutfit == null)
				{
					return false;
				}

				int col = pc.Data.DefaultOutfit.ColorId;
				if (col < 0)
				{
					return false;
				}

				return Palette.PlayerColors == null || col < Palette.PlayerColors.Length;
			}
			catch
			{
				return false;
			}
		}

		private static bool Loaded(PlayerControl pc)
		{
			return pc != null && pc.Data != null && !pc.Data.Disconnected
				&& pc.PlayerId < TempId && !pc.Data.IsIncomplete
				&& !BlankName(pc.Data.PlayerName) && OutfitReady(pc);
		}

		private static bool Settled(int clientId, PlayerControl pc)
		{
			if (clientId < 0)
			{
				return false;
			}

			if (!Loaded(pc))
			{
				LoadedAt.Remove(clientId);
				return false;
			}

			float now = Time.unscaledTime;
			if (!LoadedAt.TryGetValue(clientId, out float at))
			{
				LoadedAt[clientId] = now;
				return false;
			}

			return now - at >= SettleGap;
		}

		internal static bool Ready(ClientData c) => c != null && c.Id >= 0 && Settled(c.Id, c.Character);

		internal static void Remember(PlayerControl player, uint raw)
		{
			if (player == null || raw == 0u || !ValidRaw(raw))
			{
				return;
			}

			ByPlayerId[player.PlayerId] = raw;
			try
			{
				ClientData c = AmongUsClient.Instance != null ? AmongUsClient.Instance.GetClientFromCharacter(player) : null;
				if (c != null && c.Id >= 0)
				{
					ByClientId[c.Id] = raw;
					PushToClient(c.Id, raw);
				}
			}
			catch { }
		}

		internal static void RememberRpc(PlayerControl player, uint raw) => Remember(player, raw);

		internal static void RememberClient(int clientId, uint raw)
		{
			if (clientId < 0 || raw == 0u || !ValidRaw(raw))
			{
				return;
			}

			ByClientId[clientId] = raw;
			PushToClient(clientId, raw);
		}

		private static void PushToClient(int clientId, uint raw)
		{
			if (clientId < 0 || raw == 0u || !ValidRaw(raw))
			{
				return;
			}

			try
			{
				ClientData c = AmongUsClient.Instance != null ? AmongUsClient.Instance.FindClientById(clientId) : null;
				if (c != null && c.PlayerLevel != raw)
				{
					c.PlayerLevel = raw;
				}
			}
			catch { }
		}

		internal static void RememberCurrent(PlayerControl player)
		{
			try
			{
				if (player != null && player.Data != null && !player.Data.IsIncomplete && ValidRaw(player.Data.PlayerLevel))
				{
					Remember(player, player.Data.PlayerLevel);
				}
			}
			catch { }
		}

		internal static bool TryGet(int clientId, out uint raw)
		{
			raw = 0u;
			return clientId >= 0 && ByClientId.TryGetValue(clientId, out raw);
		}

		private static bool TryCache(PlayerControl player, out uint raw)
		{
			raw = 0u;
			if (player == null)
			{
				return false;
			}

			try
			{
				if (ByPlayerId.TryGetValue(player.PlayerId, out raw))
				{
					return true;
				}
			}
			catch { }

			try
			{
				ClientData c = AmongUsClient.Instance != null ? AmongUsClient.Instance.GetClientFromCharacter(player) : null;
				if (c != null && ByClientId.TryGetValue(c.Id, out raw))
				{
					return true;
				}
			}
			catch { }

			return false;
		}

		internal static bool TryRaw(int clientId, PlayerControl pc, out uint raw)
		{
			uint live = 0u;
			bool haveLive = false;
			try
			{
				if (pc != null && pc.Data != null && !pc.Data.IsIncomplete && ValidRaw(pc.Data.PlayerLevel))
				{
					live = pc.Data.PlayerLevel;
					haveLive = true;
				}
			}
			catch { }

			if (haveLive && live > 0u)
			{
				raw = live;
				Remember(pc, raw);
				return true;
			}

			if (TryCache(pc, out raw) && raw > 0u)
			{
				return true;
			}

			if (clientId >= 0 && ByClientId.TryGetValue(clientId, out raw) && raw > 0u)
			{
				return true;
			}

			int owner = clientId >= 0 ? clientId : OwnerOf(pc);
			if (haveLive && Settled(owner, pc))
			{
				raw = live;
				return true;
			}

			raw = 0u;
			return false;
		}

		internal static string Display(int clientId, PlayerControl pc) => TryRaw(clientId, pc, out uint raw) ? (raw + 1u).ToString() : "?";

		internal static string Display(PlayerControl pc)
		{
			int id = -1;
			if (pc != null)
			{
				id = pc.OwnerId;
			}

			return Display(id, pc);
		}

		internal static string Display(ClientData c)
		{
			if (c == null)
			{
				return "?";
			}

			if (c.Character != null)
			{
				string viaChar = Display(c.Id, c.Character);
				if (viaChar != "?")
				{
					return viaChar;
				}
			}

			if (TryGet(c.Id, out uint raw) && raw > 0u)
			{
				return (raw + 1u).ToString();
			}

			try
			{
				if (!ValidRaw(c.PlayerLevel))
				{
					return "?";
				}

				if (c.PlayerLevel > 0u)
				{
					RememberClient(c.Id, c.PlayerLevel);
					return (c.PlayerLevel + 1u).ToString();
				}

				if (Settled(c.Id, c.Character))
				{
					return "1";
				}
			}
			catch { }

			return "?";
		}

		internal static bool TryLevel(int clientId, PlayerControl pc, out int level)
		{
			if (TryRaw(clientId, pc, out uint raw))
			{
				level = (int)(raw + 1u);
				return true;
			}

			level = 0;
			return false;
		}
	}

	[HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnPlayerJoined))]
	internal static class AccessLists_JoinPatch
	{
		public static void Postfix(AmongUsClient __instance, ClientData data)
		{
			try
			{
				AccessLists.Enforce(__instance as InnerNetClient, data);
			}
			catch (Exception ex) { ErrorReporter.Report(ex, AccessLists.HandlingId, "AccessLists_JoinPatch.Postfix"); }
		}
	}

	[HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnGameJoined))]
	internal static class JoinLevels_ResetPatch
	{
		public static void Prefix()
		{
			try
			{
				JoinLevels.Reset();
			}
			catch (Exception ex) { ErrorReporter.Report(ex, AccessLists.HandlingId, "JoinLevels_ResetPatch.Prefix"); }
		}
	}

	[HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnPlayerJoined))]
	internal static class JoinLevels_JoinPushPatch
	{
		public static void Postfix(ClientData data)
		{
			if (data == null || data.Id < 0 || data.PlayerLevel > 0u)
			{
				return;
			}

			try
			{
				if (JoinLevels.TryGet(data.Id, out uint raw) && raw > 0u)
				{
					data.PlayerLevel = raw;
				}
			}
			catch (Exception ex) { ErrorReporter.Report(ex, AccessLists.HandlingId, "JoinLevels_JoinPushPatch.Postfix"); }
		}
	}

	[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.HandleRpc))]
	internal static class JoinLevels_RpcPatch
	{
		public static void Postfix(PlayerControl __instance, byte callId, MessageReader reader)
		{
			if (callId != 38 || __instance == null || reader == null)
			{
				return;
			}

			MessageReader copy = null;
			try
			{
				copy = MessageReader.Get(reader);
				uint raw = copy.ReadPackedUInt32();
				JoinLevels.RememberRpc(__instance, raw);
			}
			catch { }
			finally
			{
				try { copy?.Recycle(); } catch { }
			}
		}
	}

	[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.SetLevel))]
	internal static class JoinLevels_SetPatch
	{
		public static void Postfix(PlayerControl __instance, uint level)
		{
			if (__instance == null || level == 0u)
			{
				return;
			}

			try
			{
				JoinLevels.Remember(__instance, level);
			}
			catch (Exception ex) { ErrorReporter.Report(ex, AccessLists.HandlingId, "JoinLevels_SetPatch.Postfix"); }
		}
	}

	[HarmonyPatch(typeof(NetworkedPlayerInfo), nameof(NetworkedPlayerInfo.Deserialize))]
	internal static class JoinLevels_InfoPatch
	{
		public static void Postfix(NetworkedPlayerInfo __instance)
		{
			if (__instance == null || __instance.IsIncomplete)
			{
				return;
			}

			uint raw = __instance.PlayerLevel;
			if (raw == 0u)
			{
				return;
			}

			try
			{
				JoinLevels.RememberClient(__instance.ClientId, raw);
			}
			catch (Exception ex) { ErrorReporter.Report(ex, AccessLists.HandlingId, "JoinLevels_InfoPatch.Postfix"); }
		}
	}
}
