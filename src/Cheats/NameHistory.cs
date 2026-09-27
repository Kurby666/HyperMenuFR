using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using HarmonyLib;
using InnerNet;
using UnityEngine;

namespace MalumMenu.Cheats
{
	// Nick history by FriendCode + known-player notify. Ported from othermenu
	// Player/NocturneNameHistory.cs (431 lines). Adaptations: tracked from
	// PlayerControl.AllPlayerControls (level via Data.PlayerLevel like PlayersTab,
	// PUID/platform via allClients FriendCode match) instead of ClientData polling;
	// level/platform/PUID stored in the same block file format for forward
	// compatibility; greet/nick-change toasts via MalumMenu.notifications instead of
	// NocturneToast/event-log (neither exists in src); Tick driven from
	// RoutineManager.Update instead of a MonoBehaviour; English-only strings.
	internal static class NameHistory
	{
		private const int HandlingId = 20053;

		private sealed class Rec
		{
			internal string Fc;
			internal string Nick;
			internal List<string> Nicks;
			internal string Level = "?";
			internal string Puid = "";
			internal string Platform = "?";
			internal string Raw = "";
			internal string First;
			internal string Last;
		}

		private const string Sep = "══════════════════════════════════════";
		private const float NickWriteCd = 3f;
		private const float TickGap = 0.6f;

		private static readonly Dictionary<string, Rec> Cache = new Dictionary<string, Rec>(StringComparer.OrdinalIgnoreCase);
		private static readonly HashSet<string> _priorFcs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		private static readonly HashSet<string> _greeted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		private static readonly Dictionary<string, float> _lastFlip = new Dictionary<string, float>();
		private static bool _loaded;
		private static float _next;

		private static string HistoryTxt => Path.Combine(BepInEx.Paths.GameRootPath, "HyperMenu", "NameHistory.txt");

		internal static void Tick()
		{
			try
			{
				if(!CheatToggles.nameHistory) return;
				if(Time.unscaledTime < _next) return;
				_next = Time.unscaledTime + TickGap;
				EnsureLoaded();
				if(PlayerControl.AllPlayerControls == null) return;

				PlayerControl me = PlayerControl.LocalPlayer;
				foreach(PlayerControl p in PlayerControl.AllPlayerControls)
				{
					if(p == null || p.Data == null || p.Data.Disconnected) continue;
					string prev = RecordFromPlayer(p);
					string fc = (p.Data.FriendCode ?? "").Trim().ToLowerInvariant();
					if(fc.Length == 0) continue;
					bool self = p == me;
					if(prev != null && CheatToggles.notifyKnown)
						MalumMenu.notifications.Send("Nick change", prev + " -> " + CurrentNick(p));
					if(!self && CheatToggles.notifyKnown && _greeted.Add(fc) && KnownBefore(fc, out string since, out string knownNick))
						Greet(p, since, knownNick);
				}
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "NameHistory.Tick: tracking nicks"); }
		}

		private static void Greet(PlayerControl p, string since, string oldNick)
		{
			try
			{
				string nick = CurrentNick(p);
				if(nick.Length == 0) return;
				string detail = oldNick.Length > 0 && !string.Equals(oldNick, nick, StringComparison.Ordinal)
					? nick + " (was " + oldNick + ") · since " + since
					: nick + " · since " + since;
				MalumMenu.notifications.Send("Known player", detail);
			}
			catch { }
		}

		private static string CurrentNick(PlayerControl p)
		{
			try { return ChatTools.MuteList.Strip(p.Data.PlayerName ?? "").Trim(); }
			catch { return ""; }
		}

		// Returns previous nick when a change was recorded, else null.
		private static string RecordFromPlayer(PlayerControl p)
		{
			try
			{
				string nick = CurrentNick(p);
				if(nick.Length == 0 || nick == "???") return null;
				string fc = (p.Data.FriendCode ?? "").Trim();
				if(fc.Length == 0) return null;

				EnsureLoaded();
				string key = fc.ToLowerInvariant();
				string now = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
				string level = (p.Data.PlayerLevel + 1).ToString();
				ResolveExtra(fc, out string puid, out string platform, out string raw);

				if(Cache.TryGetValue(key, out Rec r))
				{
					bool changed = false;
					string prev = null;
					if(!string.Equals(r.Nick, nick, StringComparison.Ordinal))
					{
						float tnow = Time.realtimeSinceStartup;
						if(_lastFlip.TryGetValue(key, out float lf) && tnow - lf < NickWriteCd)
						{
							_lastFlip[key] = tnow;
							return null;
						}
						_lastFlip[key] = tnow;
						prev = r.Nick;
						if(!r.Nicks.Contains(nick)) r.Nicks.Insert(0, nick);
						r.Nick = nick;
						r.Last = now;
						changed = true;
					}
					if(level != "?" && level != r.Level) { r.Level = level; changed = true; }
					if(puid.Length > 0 && puid != r.Puid) { r.Puid = puid; changed = true; }
					if(platform != "?" && platform != r.Platform) { r.Platform = platform; changed = true; }
					if(raw.Length > 0 && raw != r.Raw) { r.Raw = raw; changed = true; }
					if(changed) Save();
					return prev;
				}

				Cache[key] = new Rec { Fc = fc, Nick = nick, Nicks = new List<string> { nick }, Level = level, Puid = puid, Platform = platform, Raw = raw, First = now, Last = now };
				Save();
				return null;
			}
			catch { return null; }
		}

		private static void ResolveExtra(string fc, out string puid, out string platform, out string raw)
		{
			puid = "";
			platform = "?";
			raw = "";
			try
			{
				InnerNetClient net = AmongUsClient.Instance;
				if(net == null || net.allClients == null) return;
				var e = net.allClients.GetEnumerator();
				while(e.MoveNext())
				{
					ClientData c = e.Current;
					if(c == null) continue;
					string cfc = "";
					try { cfc = (c.FriendCode ?? "").Trim(); } catch { }
					if(cfc.Length == 0 || !string.Equals(cfc, fc, StringComparison.OrdinalIgnoreCase)) continue;
					try { puid = (c.ProductUserId ?? "").Trim(); } catch { }
					try
					{
						if(c.PlatformData != null)
						{
							string pname = c.PlatformData.PlatformName;
							if(!string.IsNullOrWhiteSpace(pname))
							{
								pname = pname.Replace("\r", " ").Replace("\n", " ").Trim();
								raw = pname.Length <= 40 ? pname : pname.Substring(0, 40);
								platform = raw;
							}
						}
					}
					catch { }
					return;
				}
			}
			catch { }
		}

		internal static bool KnownBefore(string fcLower, out string since, out string knownNick)
		{
			since = "";
			knownNick = "";
			try
			{
				if(!_priorFcs.Contains(fcLower)) return false;
				if(!Cache.TryGetValue(fcLower, out Rec r)) return false;
				since = ShortDate(r.First);
				knownNick = r.Nick ?? "";
				return true;
			}
			catch { return false; }
		}

		internal static List<string> OwnNicks()
		{
			try
			{
				EnsureLoaded();
				PlayerControl me = PlayerControl.LocalPlayer;
				if(me == null || me.Data == null) return new List<string>();
				string fc = (me.Data.FriendCode ?? "").Trim().ToLowerInvariant();
				if(fc.Length == 0) return new List<string>();
				if(Cache.TryGetValue(fc, out Rec r) && r.Nicks != null)
					return new List<string>(r.Nicks);
			}
			catch { }
			return new List<string>();
		}

		internal static int NickCount(PlayerControl p)
		{
			try
			{
				if(p == null || p.Data == null) return 0;
				string fc = (p.Data.FriendCode ?? "").Trim();
				if(fc.Length == 0) return 0;
				EnsureLoaded();
				return Cache.TryGetValue(fc.ToLowerInvariant(), out Rec r) ? r.Nicks.Count : 0;
			}
			catch { return 0; }
		}

		private static string ShortDate(string iso)
		{
			if(string.IsNullOrEmpty(iso) || iso.Length < 10) return "?";
			string d = iso.Substring(0, 10);
			return d.Length == 10 ? d.Substring(8, 2) + "." + d.Substring(5, 2) + "." + d.Substring(0, 4) : d;
		}

		private static void EnsureLoaded()
		{
			if(_loaded) return;
			_loaded = true;
			Load();
			foreach(string k in Cache.Keys) _priorFcs.Add(k);
		}

		private static void Load()
		{
			Cache.Clear();
			try
			{
				if(!File.Exists(HistoryTxt)) return;
				string[] lines = File.ReadAllLines(HistoryTxt, Encoding.UTF8);
				bool block = false;
				for(int i = 0; i < lines.Length; i++)
					if(lines[i].StartsWith("Name: ", StringComparison.Ordinal)) { block = true; break; }
				if(block) LoadBlocks(lines);
				else LoadLegacy(lines);
			}
			catch { }
		}

		private static void LoadBlocks(string[] lines)
		{
			Rec cur = null;
			foreach(string rraw in lines)
			{
				string line = rraw.Replace("\r", "");
				if(line.StartsWith("Name: ", StringComparison.Ordinal))
				{
					Commit(cur);
					cur = new Rec { Nick = line.Substring(6).Trim(), Nicks = new List<string>() };
					if(cur.Nick.Length > 0) cur.Nicks.Add(cur.Nick);
					continue;
				}
				if(cur == null) continue;
				if(line.StartsWith("Aliases: ", StringComparison.Ordinal))
				{
					string a = line.Substring(9).Trim();
					if(a.Length > 0 && a != "—")
						foreach(string part in a.Split('·'))
						{
							string n = part.Trim();
							if(n.Length > 0 && !cur.Nicks.Contains(n)) cur.Nicks.Add(n);
						}
				}
				else if(line.StartsWith("Level: ", StringComparison.Ordinal)) cur.Level = line.Substring(7).Trim();
				else if(line.StartsWith("FriendCode: ", StringComparison.Ordinal)) cur.Fc = line.Substring(12).Trim();
				else if(line.StartsWith("PUID: ", StringComparison.Ordinal))
				{
					string v = line.Substring(6).Trim();
					cur.Puid = v == "—" ? "" : v;
				}
				else if(line.StartsWith("Platform: ", StringComparison.Ordinal))
				{
					string v = line.Substring(10).Trim();
					int idx = v.IndexOf(" · ", StringComparison.Ordinal);
					if(idx >= 0) { cur.Platform = v.Substring(0, idx).Trim(); cur.Raw = v.Substring(idx + 3).Trim(); }
					else cur.Platform = v;
				}
				else if(line.StartsWith("First seen: ", StringComparison.Ordinal)) cur.First = line.Substring(12).Trim();
				else if(line.StartsWith("Last seen: ", StringComparison.Ordinal)) cur.Last = line.Substring(11).Trim();
			}
			Commit(cur);
		}

		private static void Commit(Rec r)
		{
			if(r == null) return;
			string fc = (r.Fc ?? "").Trim();
			if(fc.Length == 0) return;
			if(r.Nicks == null) r.Nicks = new List<string>();
			if(r.Nicks.Count == 0 && !string.IsNullOrEmpty(r.Nick)) r.Nicks.Add(r.Nick);
			r.Fc = fc;
			Cache[fc.ToLowerInvariant()] = r;
		}

		private static void LoadLegacy(string[] lines)
		{
			foreach(string rraw in lines)
			{
				string line = rraw.Trim();
				if(line.Length == 0 || line[0] == '#') continue;
				string[] p = line.Split('|');
				if(p.Length < 5) continue;
				string fc = p[0].Trim();
				if(fc.Length == 0) continue;
				List<string> nicks = new List<string>();
				foreach(string part in p[2].Split(';'))
				{
					string n = part.Trim();
					if(n.Length > 0) nicks.Add(n);
				}
				if(nicks.Count == 0 && p[1].Trim().Length > 0) nicks.Add(p[1].Trim());
				Cache[fc.ToLowerInvariant()] = new Rec { Fc = fc, Nick = p[1].Trim(), Nicks = nicks, First = p[3].Trim(), Last = p[4].Trim() };
			}
		}

		private static void Save()
		{
			try
			{
				Directory.CreateDirectory(Path.GetDirectoryName(HistoryTxt));
				StringBuilder sb = new StringBuilder();
				sb.Append("# HyperMenu player history\n");
				List<Rec> ordered = new List<Rec>(Cache.Values);
				ordered.Sort((a, b) => string.CompareOrdinal(b.Last ?? "", a.Last ?? ""));
				foreach(Rec r in ordered)
				{
					sb.Append(Sep).Append('\n');
					sb.Append("Name: ").Append(r.Nick).Append('\n');
					sb.Append("Aliases: ").Append(r.Nicks.Count > 1 ? string.Join(" · ", r.Nicks.GetRange(1, r.Nicks.Count - 1)) : "—").Append('\n');
					sb.Append("Level: ").Append(r.Level).Append('\n');
					sb.Append("FriendCode: ").Append(r.Fc).Append('\n');
					sb.Append("PUID: ").Append(r.Puid.Length > 0 ? r.Puid : "—").Append('\n');
					string plat = r.Platform;
					if(r.Raw.Length > 0) plat += " · " + r.Raw;
					sb.Append("Platform: ").Append(plat).Append('\n');
					sb.Append("First seen: ").Append(r.First).Append('\n');
					sb.Append("Last seen: ").Append(r.Last).Append('\n');
				}
				sb.Append(Sep).Append('\n');
				File.WriteAllText(HistoryTxt, sb.ToString(), Encoding.UTF8);
			}
			catch { }
		}

		[HarmonyPatch(typeof(LobbyBehaviour), nameof(LobbyBehaviour.Start))]
		private static class ResetPatch
		{
			public static void Postfix() => _greeted.Clear();
		}
	}
}
