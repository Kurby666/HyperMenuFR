using System;
using Hazel;
using InnerNet;

namespace MalumMenu.Cheats
{
	// Frame sabotage: fakes the actor in a system RPC so a foreign guard
	// flags an innocent player (Sabotage flags a non-impostor at any value).
	// Ported from othermenu Cheats/NocturneFrameSabotage.cs.
	internal static class FrameSabotage
	{
		private const int HandlingId = 20031;

		internal static readonly SystemTypes[] Systems = { SystemTypes.Sabotage, SystemTypes.Reactor, SystemTypes.Electrical };
		internal static readonly string[] SystemNames = { "Sabotage (reliable)", "Reactor (16 or 128)", "Electrical (>5)" };

		internal static string SystemName(int idx) => SystemNames[idx % SystemNames.Length];

		internal static string Send(PlayerControl frameAs, SystemTypes system, byte value)
		{
			if(frameAs == null || frameAs.Data == null || frameAs.Data.Disconnected)
				return "no target";
			if(AmongUsClient.Instance == null || ShipStatus.Instance == null)
				return "in-match only";

			try
			{
				Network.BatchedMessage batch = AmongUsClient.Instance.AmHost
					? new Network.BatchedMessage()
					: new Network.BatchedMessage(AmongUsClient.Instance.HostId);

				MessageWriter body = MessageWriter.Get(SendOption.Reliable);
				body.Write(value);

				batch.QueueUpdateSystem(frameAs, system, body);
				body.Recycle();
				batch.FinishBatch();
				return "sent: " + frameAs.Data.PlayerName;
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "FrameSabotage.Send: sending framed system update"); return "failed"; }
		}
	}
}
