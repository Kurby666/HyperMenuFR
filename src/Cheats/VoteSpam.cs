using System;
using Hazel;
using InnerNet;
using UnityEngine;

namespace MalumMenu.Cheats
{
	// Vote spam (host, in meeting): broadcast fake "voted" notes for every alive
	// player once per second. Pure troll — fills the meeting chat with vote notes.
	// Ported from othermenu Cheats/VoteSpam.cs.
	internal static class VoteSpam
	{
		private const int HandlingId = 20045;
		private const float Gap = 1f;

		private static float _next;

		internal static void Tick()
		{
			try
			{
				if(!CheatToggles.voteSpam) return;

				InnerNetClient net = AmongUsClient.Instance;
				if(net == null || !net.AmHost) return;

				PlayerControl me = PlayerControl.LocalPlayer;
				if(me == null || MeetingHud.Instance == null) return;

				float now = Time.unscaledTime;
				if(now < _next) return;
				_next = now + Gap;

				foreach(PlayerControl p in PlayerControl.AllPlayerControls)
				{
					if(p == null || p.Data == null || p.Data.Disconnected || p.Data.IsDead) continue;
					SendNote(p.PlayerId);
				}
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "VoteSpam.Tick: spamming vote notes"); }
		}

		private static void SendNote(byte forPlayer)
		{
			try
			{
				MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(
					PlayerControl.LocalPlayer.NetId,
					(byte)RpcCalls.SendChatNote,
					SendOption.Reliable,
					-1);
				writer.Write(forPlayer);
				writer.Write((byte)ChatNoteTypes.DidVote);
				AmongUsClient.Instance.FinishRpcImmediately(writer);
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "VoteSpam.SendNote: sending vote note"); }
		}
	}
}
