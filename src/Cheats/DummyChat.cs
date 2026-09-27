using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace MalumMenu.Cheats
{
	// Dummy senses, body reports, meeting chatter and voting. Ported from
	// othermenu Host/NocturneDummyChat.cs. Adaptations: MeetingHud.CastVote
	// does not exist in src's game refs and VoteBanSystem.AddVote takes lobby
	// client ids (swallowed by the host instant-kick prefix), so dummy votes
	// use the src-native meeting path (playerStates[].SetVote + SetDirtyBit +
	// CheckForEndVoting, as in PlayersTab); RoomAt reuses
	// Utils.GetRoomFromPosition; English-only lines; errors via ErrorReporter.
	internal static class DummyChat
	{
		private const int HandlingId = 20062;
		private const float SpotRange = 1.6f;
		private const float SightRange = 5.5f;
		private const byte Skip = 253;

		private sealed class Seen
		{
			public byte Id; public float T; public bool Killer;
		}
		private static readonly Dictionary<byte, List<Seen>> Mem = new Dictionary<byte, List<Seen>>();
		private static float _nextScan;

		private static float _reportUntil;
		private static byte _reporter = byte.MaxValue;
		private static byte _suspect = byte.MaxValue;
		private static readonly List<byte> _near = new List<byte>();
		private static string _room = "";

		private static bool _pending;
		private static float _nextLine;
		private static int _step;
		private static readonly List<byte> _talkers = new List<byte>();
		private static int _reply;
		private static bool _voted;
		private static float _voteAt;
		private static readonly List<byte> _queue = new List<byte>();
		private static byte _target = Skip;
		private static float _nextVote;
		private static bool _voting;

		private static bool On => CheatToggles.dummyReportBodies;

		internal static void Reset()
		{
			Mem.Clear();
			_reportUntil = 0f;
			_reporter = byte.MaxValue;
			_suspect = byte.MaxValue;
			_near.Clear();
			_room = "";
			_pending = false;
			_step = 0;
			_talkers.Clear();
			_reply = 0;
			_voted = false;
			_queue.Clear();
			_target = Skip;
			_nextVote = 0f;
			_voting = false;
		}

		internal static void OnMurder(PlayerControl killer, PlayerControl victim)
		{
			try
			{
				if(!On || killer == null) return;
				Vector2 at = victim != null ? (Vector2)victim.GetTruePosition() : (Vector2)killer.GetTruePosition();
				float now = Time.time;
				foreach(PlayerControl d in Dummies.Live())
				{
					if(d == null || d.Data == null || d.Data.IsDead) continue;
					if(((Vector2)d.GetTruePosition() - at).magnitude > SightRange + 1.5f) continue;
					List<Seen> mem = MemOf(d.PlayerId);
					bool found = false;
					for(int i = 0; i < mem.Count; i++)
						if(mem[i].Id == killer.PlayerId)
						{
							mem[i].T = now;
							mem[i].Killer = true;
							found = true;
							break;
						}
					if(!found)
						mem.Add(new Seen { Id = killer.PlayerId, T = now, Killer = true });
				}
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "DummyChat.OnMurder: recording witness"); }
		}

		private static List<Seen> MemOf(byte id)
		{
			if(!Mem.TryGetValue(id, out List<Seen> m))
			{
				m = new List<Seen>();
				Mem[id] = m;
			}
			return m;
		}

		private static DeadBody[] _bodies;
		private static float _bodiesAt = -99f;

		private static DeadBody[] Bodies()
		{
			if(_bodies == null || Time.time - _bodiesAt > 0.4f)
			{
				_bodiesAt = Time.time;
				try { _bodies = UnityEngine.Object.FindObjectsOfType<DeadBody>(); }
				catch (Exception ex)
				{
					ErrorReporter.Report(ex, HandlingId, "DummyChat.Bodies: finding bodies");
					_bodies = null;
				}
			}
			return _bodies;
		}

		internal static bool TryReport(PlayerControl d)
		{
			try
			{
				if(!On || d == null || d.Data == null || d.Data.IsDead) return false;
				if(Time.time < _reportUntil || MeetingHud.Instance != null) return false;

				Vector2 me = d.GetTruePosition();
				DeadBody[] bodies = Bodies();
				if(bodies == null) return false;

				for(int i = 0; i < bodies.Length; i++)
				{
					DeadBody b = bodies[i];
					if(b == null) continue;
					Vector2 bp = b.TruePosition;
					if((bp - me).magnitude > SpotRange) continue;

					Capture(d, b, bp);
					_reportUntil = Time.time + 30f;
					NetworkedPlayerInfo victim = GameData.Instance.GetPlayerById(b.ParentId);
					try { d.CmdReportDeadBody(victim); }
					catch (Exception ex)
					{
						ErrorReporter.Report(ex, HandlingId, "DummyChat.TryReport: reporting body");
						return false;
					}

					_reporter = d.PlayerId;
					_pending = true;
					_voted = false;
					_step = 0;
					_reply = 0;
					_voteAt = 0f;
					_queue.Clear();
					_nextVote = 0f;
					_voting = false;
					_nextLine = Time.time + 2.5f;
					return true;
				}
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "DummyChat.TryReport: trying report"); }
			return false;
		}

		private static void Capture(PlayerControl reporter, DeadBody body, Vector2 bp)
		{
			_near.Clear();
			_suspect = byte.MaxValue;
			_room = RoomAt(bp);
			byte victimId = body.ParentId;

			try
			{
				byte witness = byte.MaxValue;
				if(Mem.TryGetValue(reporter.PlayerId, out List<Seen> mem))
				{
					float now = Time.time;
					for(int i = 0; i < mem.Count; i++)
					{
						Seen s = mem[i];
						if(s.Id == victimId) continue;
						if(s.Killer && now - s.T <= 60f && !Dummies.IsDummy(s.Id))
						{
							witness = s.Id;
							if(!_near.Contains(s.Id))
								_near.Add(s.Id);
						}
						else if(now - s.T <= 12f && !_near.Contains(s.Id))
							_near.Add(s.Id);
					}
				}

				float best = float.MaxValue;
				byte nearest = byte.MaxValue;
				foreach(PlayerControl p in PlayerControl.AllPlayerControls)
				{
					if(p == null || p.Data == null || p.Data.IsDead) continue;
					if(p.PlayerId == victimId || Dummies.IsDummy(p.PlayerId)) continue;
					float dd = ((Vector2)p.GetTruePosition() - bp).magnitude;
					if(dd <= 7f)
					{
						if(!_near.Contains(p.PlayerId))
							_near.Add(p.PlayerId);
						if(dd < best)
						{
							best = dd;
							nearest = p.PlayerId;
						}
					}
				}

				if(witness != byte.MaxValue)
					_suspect = witness;
				else if(nearest != byte.MaxValue)
					_suspect = nearest;
				else
					for(int i = _near.Count - 1; i >= 0; i--)
						if(!Dummies.IsDummy(_near[i]))
						{
							_suspect = _near[i];
							break;
						}
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "DummyChat.Capture: capturing suspects"); }
		}

		internal static void TickVision()
		{
			try
			{
				if(!On || MeetingHud.Instance != null || Time.time < _nextScan) return;
				_nextScan = Time.time + 0.5f;
				float now = Time.time;
				foreach(PlayerControl d in Dummies.Live())
				{
					if(d == null || d.Data == null || d.Data.IsDead) continue;
					Vector2 dp = d.GetTruePosition();
					List<Seen> mem = MemOf(d.PlayerId);
					foreach(PlayerControl p in PlayerControl.AllPlayerControls)
					{
						if(p == null || p.Data == null || p.Data.IsDead || p.PlayerId == d.PlayerId) continue;
						if(((Vector2)p.GetTruePosition() - dp).magnitude <= SightRange)
							Bump(mem, p.PlayerId, now);
					}
					mem.RemoveAll((Predicate<Seen>)(s => now - s.T > 20f));
				}
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "DummyChat.TickVision: scanning vision"); }
		}

		private static void Bump(List<Seen> mem, byte id, float now)
		{
			for(int i = 0; i < mem.Count; i++)
				if(mem[i].Id == id)
				{
					mem[i].T = now;
					return;
				}
			mem.Add(new Seen { Id = id, T = now });
		}

		internal static void TickMeeting()
		{
			try
			{
				if(!On) return;
				MeetingHud hud = MeetingHud.Instance;
				if(hud == null)
				{
					_pending = false;
					return;
				}
				if(!_pending) return;

				float now = Time.time;
				if(now >= _nextLine && _step >= 0) NextLine();

				if(!_voted && _voteAt > 0f && now >= _voteAt)
				{
					if(!_voting)
					{
						_voting = true;
						BeginVotes();
					}
					else
						PumpVotes();
				}
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "DummyChat.TickMeeting: ticking meeting"); }
		}

		private static void NextLine()
		{
			PlayerControl rep = ById(_reporter);
			switch(_step)
			{
				case 0:
				{
					string w = string.IsNullOrEmpty(_room) ? "somewhere" : _room;
					string line = _near.Count == 0
						? $"Body in {w}, no one around. Skip?"
						: $"Found a body in {w}! Nearby: {Names(_near)}.";
					Say(rep, line);
					_step = 1;
					_nextLine = Time.time + 2.2f;
					break;
				}
				case 1:
				{
					if(_suspect != byte.MaxValue)
						Say(rep, $"I suspect {Name(_suspect)}, was closest.");
					PickTalkers();
					_reply = 0;
					_step = 2;
					_nextLine = Time.time + 2f;
					break;
				}
				default:
				{
					if(_reply < _talkers.Count)
					{
						Say(ById(_talkers[_reply]), Reply(_reply));
						_reply++;
						_nextLine = Time.time + UnityEngine.Random.Range(1.6f, 2.4f);
					}
					else
					{
						_step = -1;
						_voteAt = Time.time + 2f;
					}
					break;
				}
			}
		}

		private static readonly string[] En =
		{
			"Was on tasks, not me.", "Sus...", "Not enough info, skip.", "Where were you?",
			"Agree, let's vote.", "Saw no one.", "Saw them by the body!", "Not me, swear.",
			"Makes sense, vote them.", "No random votes.", "I was fixing sabotage.", "Skip to be safe.",
			"Who followed who?", "I believe you.", "Accusing too fast.", "Vote or skip?",
		};

		private static string Reply(int salt) => En[(UnityEngine.Random.Range(0, En.Length) + salt) % En.Length];

		private static void PickTalkers()
		{
			_talkers.Clear();
			try
			{
				foreach(PlayerControl pc in Dummies.Live())
				{
					if(pc == null || pc.Data == null || pc.Data.IsDead || pc.PlayerId == _reporter) continue;
					_talkers.Add(pc.PlayerId);
					if(_talkers.Count >= 6) break;
				}
				Shuffle(_talkers);
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "DummyChat.PickTalkers: picking talkers"); }
		}

		private static void Shuffle(List<byte> list)
		{
			for(int i = list.Count - 1; i > 0; i--)
			{
				int j = UnityEngine.Random.Range(0, i + 1);
				(list[i], list[j]) = (list[j], list[i]);
			}
		}

		private static void BeginVotes()
		{
			_queue.Clear();
			_target = _suspect != byte.MaxValue ? _suspect : Skip;
			try
			{
				foreach(PlayerControl pc in Dummies.Live())
					if(pc != null && pc.Data != null && !pc.Data.IsDead)
						_queue.Add(pc.PlayerId);
				Shuffle(_queue);
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "DummyChat.BeginVotes: queueing votes"); }
			_nextVote = Time.time + UnityEngine.Random.Range(0.5f, 1.2f);
		}

		private static void PumpVotes()
		{
			try
			{
				if(Time.time < _nextVote) return;
				MeetingHud hud = MeetingHud.Instance;
				if(hud == null)
				{
					_voted = true;
					_pending = false;
					return;
				}
				while(_queue.Count > 0)
				{
					byte id = _queue[0];
					_queue.RemoveAt(0);
					PlayerControl pc = ById(id);
					if(pc == null || pc.Data == null || pc.Data.IsDead) continue;
					try
					{
						byte vote = _target == Skip ? PlayerVoteArea.SkippedVote : _target;
						hud.playerStates[id].SetVote(vote);
						hud.SetDirtyBit(1u);
						hud.CheckForEndVoting();
					}
					catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "DummyChat.PumpVotes: casting dummy vote"); }
					_nextVote = Time.time + UnityEngine.Random.Range(1f, 2f);
					return;
				}
				_voted = true;
				_pending = false;
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "DummyChat.PumpVotes: pumping votes"); }
		}

		private static void Say(PlayerControl pc, string text)
		{
			if(pc == null || string.IsNullOrEmpty(text)) return;
			try { pc.RpcSendChat(text); }
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "DummyChat.Say: sending chat"); }
		}

		private static PlayerControl ById(byte id)
		{
			try
			{
				NetworkedPlayerInfo info = GameData.Instance.GetPlayerById(id);
				return info != null ? info.Object : null;
			}
			catch (Exception ex)
			{
				ErrorReporter.Report(ex, HandlingId, "DummyChat.ById: finding player");
				return null;
			}
		}

		private static string Name(byte id)
		{
			try
			{
				NetworkedPlayerInfo d = GameData.Instance.GetPlayerById(id);
				return d != null ? ChatTools.MuteList.Strip(d.PlayerName) : "?";
			}
			catch { return "?"; }
		}

		private static string Names(List<byte> ids)
		{
			if(ids == null || ids.Count == 0) return "no one";
			var parts = new List<string>();
			for(int i = 0; i < ids.Count && i < 4; i++)
				parts.Add(Name(ids[i]));
			return string.Join(", ", parts.ToArray());
		}

		private static string RoomAt(Vector2 pos)
		{
			try
			{
				PlainShipRoom room = Utils.GetRoomFromPosition(pos);
				if(room == null) return "";
				return RoomName((int)room.RoomId);
			}
			catch (Exception ex)
			{
				ErrorReporter.Report(ex, HandlingId, "DummyChat.RoomAt: naming room");
				return "";
			}
		}

		private static string RoomName(int room)
		{
			switch(room)
			{
				case 1: return "Admin";
				case 2: return "Comms";
				case 3: return "Reactor";
				case 4: return "Electrical";
				case 5: return "Navigation";
				case 6: return "O2";
				case 7: return "Shields";
				case 8: return "Cafeteria";
				case 9: return "Storage";
				case 10: return "MedBay";
				case 11: return "Security";
				case 12: return "Weapons";
				case 13: return "Lower Engine";
				case 14: return "Comms";
				case 16: return "Launchpad";
				case 17: return "Upper Engine";
				case 19: return "Engine Room";
				case 21: return "Reactor";
				case 24: return "Cockpit";
				case 25: return "Records";
				default: return "hallway";
			}
		}

		[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.MurderPlayer))]
		internal static class DummyWitnessPatch
		{
			public static void Postfix(PlayerControl __instance, PlayerControl target, MurderResultFlags resultFlags)
			{
				try { OnMurder(__instance, target); }
				catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "DummyChat.DummyWitnessPatch.Postfix: witnessing murder"); }
			}
		}
	}
}
