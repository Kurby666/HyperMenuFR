using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using InnerNet;
using UnityEngine;

namespace MalumMenu.Cheats
{
	internal sealed class RecentEntry
	{
		internal string Name;
		internal string Fc;
		internal string Puid;
		internal string Platform;
		internal string Raw;
		internal int Level;
		internal bool Left;
		internal string Title;
		internal string Info;
		internal string Key;
	}

	internal sealed class RecentRow
	{
		internal string Name;
		internal string Code;
		internal string Puid;
	}

	// Ported from othermenu/Security/HyperJoinDetector.cs + HyperRecent.cs +
	// HyperRecentPlayers.cs.
	//
	// src substitutions: the detector's MonoBehaviour is dropped in favour of a static Tick driven
	// from RoutineManager.Update (src convention, the same call othermenu's per-frame Update made);
	// toasts go through MalumMenu.notifications; KnownBefore on NameHistory is made internal by
	// the wiring pass so the "seen this friend code before" line is real; the recently-played-with
	// poll reads FriendsListManager directly.
	internal static class JoinIntel
	{
		internal const int HandlingId = 20072;

		private const float ScanInterval = 0.35f;
		private const float JoinSettle = 0.70f;
		private const float JoinRetry = 0.45f;
		private const float JoinMaxWait = 7f;

		private static float nextScanAt;

		private static readonly HashSet<string> Known = new HashSet<string>();
		private static readonly Dictionary<int, PendingRec> Pending = new Dictionary<int, PendingRec>();
		private static readonly Dictionary<int, float> Recheck = new Dictionary<int, float>();
		private static readonly HashSet<int> Live = new HashSet<int>();
		private static readonly HashSet<int> Stale = new HashSet<int>();
		private static readonly Dictionary<int, string> Names = new Dictionary<int, string>();
		private static readonly HashSet<int> Seen = new HashSet<int>();

		private sealed class PendingRec
		{
			internal float At;
			internal int Tries;
		}

		private const int RecentMax = 30;
		private static readonly List<RecentEntry> Recent = new List<RecentEntry>();
		private static readonly List<RecentRow> Played = new List<RecentRow>();

		// the same token list othermenu flags on a raw/nick
		private static readonly string[] SusTokens =
		{
			"test", "admin", "dev", "staff", "mod", "cheat", "aim", "hack", "bot", "lobby",
			"host", "owner", "gm", "debug", "menu", "nitro", "pro", "vip", "god",
		};

		// ------------------------------------------------------------------ recent

		private static string Key(string fc, string puid) => !string.IsNullOrEmpty(fc) ? "fc:" + fc.ToLowerInvariant() : "puid:" + puid;

		internal static List<RecentEntry> RecentEntries => Recent;

		internal static List<RecentRow> PlayedEntries => Played;

		internal static void ForgetRecent(string key)
		{
			for (int i = Recent.Count - 1; i >= 0; i--)
			{
				if (Recent[i].Key == key)
				{
					Recent.RemoveAt(i);
				}
			}
		}

		private static void Seal(RecentEntry e)
		{
			e.Name = e.Name ?? "?";
			e.Fc = e.Fc ?? string.Empty;
			e.Puid = e.Puid ?? string.Empty;
			e.Platform = e.Platform ?? string.Empty;
			e.Raw = e.Raw ?? string.Empty;
			e.Info = e.Info ?? string.Empty;
			e.Title = string.IsNullOrEmpty(e.Title) ? e.Name : e.Title;
		}

		private static void InsertRecent(RecentEntry e)
		{
			Seal(e);
			for (int i = 0; i < Recent.Count; i++)
			{
				if (Recent[i].Key == e.Key)
				{
					Recent.RemoveAt(i);
					break;
				}
			}

			Recent.Insert(0, e);
			while (Recent.Count > RecentMax)
			{
				Recent.RemoveAt(Recent.Count - 1);
			}
		}

		// ------------------------------------------------------------------ scan

		internal static void Tick()
		{
			float now = Time.realtimeSinceStartup;
			if (now < nextScanAt)
			{
				return;
			}

			nextScanAt = now + ScanInterval;

			PruneDeparted();
			Scan();
			PollPlayed();
		}

		private static void PruneDeparted()
		{
			InnerNetClient net = AmongUsClient.Instance as InnerNetClient;
			if (net == null || net.allClients == null)
			{
				return;
			}

			HashSet<int> present = new HashSet<int>();
			try
			{
				var e = net.allClients.GetEnumerator();
				while (e.MoveNext())
				{
					ClientData c = e.Current;
					if (c != null)
					{
						present.Add(c.Id);
					}
				}
			}
			catch { }

			foreach (int id in Stale)
			{
				if (present.Contains(id))
				{
					continue;
				}

				AnnounceLeave(id);
			}

			Stale.Clear();
			foreach (int id in present)
			{
				if (!Live.Contains(id))
				{
					Stale.Add(id);
				}
			}
		}

		private static void Scan()
		{
			InnerNetClient net = AmongUsClient.Instance as InnerNetClient;
			if (net == null || net.allClients == null)
			{
				return;
			}

			try
			{
				var e = net.allClients.GetEnumerator();
				while (e.MoveNext())
				{
					ClientData c = e.Current;
					if (c == null || c.Id < 0)
					{
						continue;
					}

					Live.Add(c.Id);
					Stale.Remove(c.Id);

					string name = AccessLists.SafeName(c);
					Names[c.Id] = name;

					string fc = AccessLists.SafeFc(c);
					string key = Key(fc, AccessLists.SafePuid(c));

					if (!Pending.ContainsKey(c.Id) && !Seen.Contains(c.Id))
					{
						Pending[c.Id] = new PendingRec { At = Time.realtimeSinceStartup };
						continue;
					}

					if (!Pending.ContainsKey(c.Id))
					{
						continue;
					}

					PendingRec p = Pending[c.Id];
					if (!JoinLevels.Ready(c))
					{
						if (Time.realtimeSinceStartup - p.At > JoinMaxWait)
						{
							Pending.Remove(c.Id);
						}

						continue;
					}

					if (Time.realtimeSinceStartup - p.At < JoinSettle)
					{
						continue;
					}

					if (Time.realtimeSinceStartup - p.At > JoinMaxWait)
					{
						Pending.Remove(c.Id);
						continue;
					}

					// still re-checking (level changed on us) — retry a couple of times
					p.Tries++;
					if (p.Tries < 3 && Time.realtimeSinceStartup - p.At < JoinMaxWait)
					{
						Recheck[c.Id] = Time.realtimeSinceStartup + JoinRetry;
						continue;
					}

					Pending.Remove(c.Id);
					Recheck.Remove(c.Id);
					Remember(net, c, name, key, fc);
				}
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "JoinIntel.Scan"); }
		}

		private static void Remember(InnerNetClient net, ClientData c, string name, string key, string fc)
		{
			string puid = AccessLists.SafePuid(c);
			string platform = AccessLists.PlatformLabel(c);
			string raw = AccessLists.SafePlatform(c);
			string lvl = JoinLevels.Display(c);

			Seen.Add(c.Id);
			if (!Known.Contains(key))
			{
				Known.Add(key);
			}

			bool isSelf = net.ClientId == c.Id;
			string info = $"lvl {lvl} · {platform}";

			if (CheatToggles.notifyKnown && !isSelf && NameHistory.KnownBefore((fc ?? string.Empty).ToLowerInvariant(), out string since, out string knownNick))
			{
				info += $" · seen {since} as {knownNick}";
			}

			InsertRecent(new RecentEntry
			{
				Name = name,
				Fc = fc,
				Puid = puid,
				Platform = platform,
				Raw = raw,
				Level = int.TryParse(lvl, out int l) ? l : 0,
				Title = name,
				Info = info,
				Key = key,
			});

			if (isSelf || !CheatToggles.joinIntel)
			{
				return;
			}

			AnnounceJoin(c, name, lvl, platform, info);
		}

		private static void AnnounceJoin(ClientData c, string name, string lvl, string platform, string info)
		{
			string title = "Join";
			string body = $"{name} · {info}";

			if (IsSuspicious(name, AccessLists.SafePlatform(c)))
			{
				title = "Join ⚠";
			}

			EventLog.Fire(title, body, CheatToggles.joinIntelToasts, EventCat.Join, 4f);
		}

		private static void AnnounceLeave(int clientId)
		{
			Names.TryGetValue(clientId, out string name);
			name = string.IsNullOrEmpty(name) ? "#" + clientId : name;

			Live.Remove(clientId);
			Seen.Remove(clientId);
			Pending.Remove(clientId);
			Recheck.Remove(clientId);
			Names.Remove(clientId);

			for (int i = 0; i < Recent.Count; i++)
			{
				if (Recent[i].Title == name)
				{
					Recent[i].Left = true;
					Recent[i].Info += " · left";
				}
			}

			EventLog.Fire("Leave", $"{name} left", CheatToggles.joinIntelToasts, EventCat.Join, 3f);
		}

		private static string ResolveName(int clientId)
		{
			return Names.TryGetValue(clientId, out string n) ? n : "#" + clientId;
		}

		// ------------------------------------------------------------------ suspicion

		private static string CleanRaw(string raw)
		{
			string s = ColorTools.StripTags(raw ?? string.Empty);
			StringBuilder sb = new StringBuilder(s.Length);
			foreach (char ch in s)
			{
				if (char.IsLetterOrDigit(ch) || ch == '_' || ch == '-' || ch == '.')
				{
					sb.Append(char.ToLowerInvariant(ch));
				}
			}

			return sb.ToString();
		}

		private static bool IsSuspicious(string name, string raw)
		{
			string a = CleanRaw(name);
			string b = CleanRaw(raw);
			if (a.Length == 0 && b.Length == 0)
			{
				return false;
			}

			for (int i = 0; i < SusTokens.Length; i++)
			{
				string t = SusTokens[i];
				if ((a.Length > 0 && a.Contains(t)) || (b.Length > 0 && b.Contains(t)))
				{
					return true;
				}
			}

			return false;
		}

		// ------------------------------------------------------------------ recently played with

		// othermenu polls FriendsListManager.RecentlyPlayedWith for this list. That collection is
		// not reachable from src (FriendCodeData has no src reference), so Placed is derived from the
		// lobby history this class already keeps — same data, one less dependency.
		private static void PollPlayed()
		{
			Played.Clear();
			for (int i = 0; i < Recent.Count; i++)
			{
				RecentEntry e = Recent[i];
				if (!string.IsNullOrEmpty(e.Fc) || !string.IsNullOrEmpty(e.Puid))
				{
					Played.Add(new RecentRow { Name = e.Name, Code = e.Fc, Puid = e.Puid });
				}
			}
		}

		internal static void BanPlayed(RecentRow r)
		{
			if (r == null)
			{
				return;
			}

			AccessLists.AddBan(r.Name, r.Code, r.Puid);
			MalumMenu.notifications.Send("Ban list", r.Name, 2.5f);
		}

		internal static void WhitePlayed(RecentRow r)
		{
			if (r == null)
			{
				return;
			}

			AccessLists.AddWhite(r.Name, r.Code, r.Puid);
			MalumMenu.notifications.Send("Whitelist", r.Name, 2.5f);
		}

		internal static void ClearPlayed()
		{
			Played.Clear();
		}

		internal static void Forget(string key) => ForgetRecent(key);

		// A new game invalidates the per-client join state but deliberately KEEPS Known (so the
		// "seen this friend code before" line still fires on a rejoin) and Recent (the whole point
		// of the card is the cross-lobby history).
		internal static void ResetGame()
		{
			Pending.Clear();
			Recheck.Clear();
			Live.Clear();
			Stale.Clear();
			Names.Clear();
			Seen.Clear();
		}
	}

	[HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnGameJoined))]
	internal static class JoinIntel_ResetPatch
	{
		public static void Postfix()
		{
			try
			{
				JoinIntel.ResetGame();
			}
			catch { }
		}
	}
}
