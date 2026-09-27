using System;
using System.Collections.Generic;
using HarmonyLib;
using InnerNet;
using UnityEngine;

namespace MalumMenu.Cheats
{
	// Votekick guard: defensive auto-rejoin escape (non-host), vote tally expose,
	// host punish-voter (Null/Warn/Kick/Ban), and self-kick block.
	// Ported from othermenu NocturneAntiVotekick.cs + NocturneVoteKickPatch (AccessGuardPatches.cs).
	internal static class VotekickGuard
	{
		private const int HandlingId = 20050;
		private const int EscapeVotes = 2;
		private const float RejoinDelay = 2f;

		private static readonly Dictionary<int, List<int>> _tally = new Dictionary<int, List<int>>();
		private static int _pendingCode;
		private static float _rejoinAt;

		internal static int VotesOnMe
		{
			get
			{
				try
				{
					InnerNetClient net = AmongUsClient.Instance;
					if(net == null) return 0;
					if(_tally.TryGetValue(net.ClientId, out List<int> voters))
						return voters.Count;
					return 0;
				}
				catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "VotekickGuard.VotesOnMe: counting votes on self"); return 0; }
			}
		}

		internal static string PunishName(int idx)
		{
			switch(idx)
			{
				case 0: return "Null";
				case 1: return "Warn";
				case 3: return "Ban";
				default: return "Kick";
			}
		}

		internal static void NoteVote(int srcClient, int targetClient)
		{
			try
			{
				if(!_tally.TryGetValue(targetClient, out List<int> voters))
				{
					voters = new List<int>();
					_tally[targetClient] = voters;
				}
				if(!voters.Contains(srcClient))
					voters.Add(srcClient);

				if(!CheatToggles.votekickProtect) return;
				if(Utils.isHost) return;
				if(VotekickTools.Armed) return;

				InnerNetClient net = AmongUsClient.Instance;
				if(net == null || targetClient != net.ClientId) return;
				if(voters.Count < EscapeVotes) return;

				Escape();
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "VotekickGuard.NoteVote: recording vote"); }
		}

		internal static void PunishVoter(int srcClient)
		{
			try
			{
				InnerNetClient net = AmongUsClient.Instance;
				if(net == null || srcClient == net.ClientId) return;

				int mode = Mathf.Clamp(CheatToggles.votekickPunishIdx, 0, 3);
				string name = ClientName(srcClient);
				if(mode == 1)
					MalumMenu.notifications.Send("Votekick Guard", $"{name} tried to votekick and was warned.", 5);
				else if(mode == 2)
				{
					net.KickPlayer(srcClient, false);
					MalumMenu.notifications.Send("Votekick Guard", $"{name} tried to votekick and was kicked.", 5);
				}
				else if(mode == 3)
				{
					net.KickPlayer(srcClient, true);
					MalumMenu.notifications.Send("Votekick Guard", $"{name} tried to votekick and was banned.", 5);
				}
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "VotekickGuard.PunishVoter: punishing voter"); }
		}

		internal static void Tick()
		{
			try
			{
				if(_pendingCode == 0) return;
				if(Time.unscaledTime < _rejoinAt) return;
				if(LobbyBehaviour.Instance != null || ShipStatus.Instance != null)
				{
					_pendingCode = 0;
					return;
				}
				int code = _pendingCode;
				_pendingCode = 0;
				VotekickTools.Rejoin(code);
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "VotekickGuard.Tick: rejoining after escape"); }
		}

		private static void Escape()
		{
			try
			{
				AmongUsClient net = AmongUsClient.Instance;
				if(net == null) return;
				int code = net.GameId;
				if(code == 0) return;

				GUIUtility.systemCopyBuffer = GameCode.IntToGameName(code);
				MalumMenu.notifications.Send("Votekick Guard", $"2+ votekick votes on you — left and rejoining {GameCode.IntToGameName(code)}.", 10);
				net.ExitGame(DisconnectReasons.ExitGame);
				_pendingCode = code;
				_rejoinAt = Time.unscaledTime + RejoinDelay;
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "VotekickGuard.Escape: leaving and rejoining"); }
		}

		private static string ClientName(int srcClient)
		{
			try
			{
				foreach(PlayerControl p in PlayerControl.AllPlayerControls)
				{
					if(p != null && p.Data != null && !p.Data.Disconnected && p.OwnerId == srcClient)
						return p.Data.PlayerName;
				}
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "VotekickGuard.ClientName: resolving voter name"); }
			return $"Client {srcClient}";
		}

		[HarmonyPatch(typeof(VoteBanSystem), nameof(VoteBanSystem.AddVote))]
		internal static class GuardPrefix
		{
			static bool Prefix(int srcClient, int clientId)
			{
				try
				{
					NoteVote(srcClient, clientId);

					if(!CheatToggles.votekickProtect) return true;

					InnerNetClient net = AmongUsClient.Instance;
					if(net != null && clientId == net.ClientId) return false;

					if(Utils.isHost && (net == null || srcClient != net.ClientId))
					{
						PunishVoter(srcClient);
						return false;
					}
					return true;
				}
				catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "GuardPrefix.Prefix: guarding votekick"); return true; }
			}
		}

		[HarmonyPatch(typeof(LobbyBehaviour), nameof(LobbyBehaviour.Start))]
		internal static class TallyReset
		{
			static void Postfix()
			{
				try
				{
					_tally.Clear();
					_pendingCode = 0;
				}
				catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "TallyReset.Postfix: clearing vote tally"); }
			}
		}
	}
}
