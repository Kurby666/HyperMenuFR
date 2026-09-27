using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime.InteropTypes;
using Object = UnityEngine.Object;

namespace MalumMenu.Cheats
{
	// Send players down/up the Fungle zipline. Ported from othermenu Cheats/Zipline.cs.
	internal static class ZiplineRide
	{
		private const int HandlingId = 20028;

		private static ShipStatus _ship;
		private static ZiplineBehaviour _line;

		internal static bool OnMap => Line() != null;

		private static ZiplineBehaviour Line()
		{
			try
			{
				ShipStatus ship = ShipStatus.Instance;
				if(ship == null)
				{
					_ship = null;
					_line = null;
					return null;
				}
				if(_ship == ship) return _line;

				_ship = ship;
				FungleShipStatus fungle = ((Il2CppObjectBase)ship).TryCast<FungleShipStatus>();
				if(fungle != null && fungle.Zipline != null)
					_line = fungle.Zipline;
				else
					_line = Object.FindObjectOfType<ZiplineBehaviour>();
				return _line;
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "ZiplineRide.Line: finding zipline"); return null; }
		}

		private static bool Send(PlayerControl target, ZiplineBehaviour line, bool fromTop)
		{
			try
			{
				target.RpcUseZipline(target, line, fromTop);
			}
			catch (Exception ex)
			{
				ErrorReporter.Report(ex, HandlingId, "ZiplineRide.Send: sending player on zipline");
				return false;
			}
			return true;
		}

		internal static string Ride(PlayerControl target, bool fromTop)
		{
			if(target == null || target.Data == null || target.Data.Disconnected)
				return "No target.";
			if(ShipStatus.Instance == null) return "In match only.";

			ZiplineBehaviour line = Line();
			if(line == null)
				return "No zipline here — Fungle only.";

			if(!Send(target, line, fromTop))
				return "Failed.";
			return (fromTop ? "Down: " : "Up: ") + target.Data.PlayerName;
		}

		internal static string RideSelected(bool fromTop)
		{
			if(ShipStatus.Instance == null) return "In match only.";

			ZiplineBehaviour line = Line();
			if(line == null) return "No zipline here — Fungle only.";

			List<PlayerControl> targets = RideTargets.Players();
			if(targets.Count == 0) return "Nobody selected.";

			int n = 0;
			for(int i = 0; i < targets.Count; i++)
				if(Send(targets[i], line, fromTop)) n++;

			return (fromTop ? "Down: " : "Up: ") + n + "/" + targets.Count;
		}
	}
}
