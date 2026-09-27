using System.Collections.Generic;

namespace MalumMenu.Cheats
{
	// Selection set for ride cheats (zipline). Ported from othermenu Cheats/RideTargets.cs.
	internal static class RideTargets
	{
		private static readonly HashSet<byte> _set = new HashSet<byte>();

		internal static int Count => _set.Count;
		internal static bool Has(byte pid) => _set.Contains(pid);

		internal static void Toggle(byte pid)
		{
			if(!_set.Remove(pid))
				_set.Add(pid);
		}

		internal static void Clear()
		{
			_set.Clear();
			Prune();
		}

		internal static void All()
		{
			if(PlayerControl.AllPlayerControls == null) return;
			foreach(PlayerControl pc in PlayerControl.AllPlayerControls)
			{
				if(pc == null || pc.Data == null || pc.Data.Disconnected) continue;
				if(pc == PlayerControl.LocalPlayer) continue;
				_set.Add(pc.PlayerId);
			}
		}

		internal static List<PlayerControl> Players()
		{
			var list = new List<PlayerControl>();
			Prune();
			if(_set.Count == 0 || PlayerControl.AllPlayerControls == null) return list;

			foreach(PlayerControl pc in PlayerControl.AllPlayerControls)
			{
				if(pc == null || pc.Data == null || pc.Data.Disconnected) continue;
				if(pc == PlayerControl.LocalPlayer) continue;
				if(_set.Contains(pc.PlayerId))
					list.Add(pc);
			}
			return list;
		}

		private static void Prune()
		{
			if(PlayerControl.AllPlayerControls == null) { _set.Clear(); return; }
			var live = new HashSet<byte>();
			foreach(PlayerControl pc in PlayerControl.AllPlayerControls)
			{
				if(pc == null || pc.Data == null || pc.Data.Disconnected) continue;
				live.Add(pc.PlayerId);
			}
			_set.RemoveWhere(id => !live.Contains(id));
		}
	}
}
