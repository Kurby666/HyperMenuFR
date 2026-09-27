using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MalumMenu.Cheats
{
	// Named lobby-settings presets (host, lobby only), stored as name|payload
	// lines in HyperMenu/LobbyPresets.txt. Ported from othermenu
	// Host/NocturneLobbyPresets.cs (BepInEx-config storage replaced with the
	// same flat-file pattern as NameHistory/ColorReservations).
	internal static class LobbyPresets
	{
		private const int HandlingId = 20057;

		internal static string PresetName = "";

		private static string PresetsTxt => Path.Combine(BepInEx.Paths.GameRootPath, "HyperMenu", "LobbyPresets.txt");

		private static readonly List<string> _names = new List<string>();
		private static readonly Dictionary<string, string> _data = new Dictionary<string, string>();
		private static bool _loaded;

		private static void EnsureLoaded()
		{
			try
			{
				if(_loaded) return;
				_loaded = true;
				if(!File.Exists(PresetsTxt)) return;
				foreach(string line in File.ReadAllLines(PresetsTxt))
				{
					if(string.IsNullOrEmpty(line)) continue;
					int bar = line.IndexOf('|');
					if(bar <= 0) continue;
					string pname = line.Substring(0, bar);
					if(_data.ContainsKey(pname)) continue;
					_names.Add(pname);
					_data[pname] = line.Substring(bar + 1);
				}
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "LobbyPresets.EnsureLoaded: loading presets"); }
		}

		private static void Flush()
		{
			try
			{
				Directory.CreateDirectory(Path.GetDirectoryName(PresetsTxt));
				StringBuilder sb = new StringBuilder();
				foreach(string n in _names)
				{
					if(sb.Length > 0) sb.Append('\n');
					sb.Append(n).Append('|').Append(_data[n]);
				}
				File.WriteAllText(PresetsTxt, sb.ToString());
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "LobbyPresets.Flush: saving presets"); }
		}

		internal static List<string> Names()
		{
			EnsureLoaded();
			return _names;
		}

		internal static string Save(string pname)
		{
			try
			{
				EnsureLoaded();
				pname = Clean(pname);
				if(pname.Length == 0) return "Empty name.";
				if(!LobbySettings.Ready()) return "Host in lobby only.";
				if(!_data.ContainsKey(pname)) _names.Add(pname);
				_data[pname] = LobbySettings.Capture();
				while(_names.Count > 12)
				{
					string drop = _names[0];
					_names.RemoveAt(0);
					_data.Remove(drop);
				}
				Flush();
				return "Preset \"" + pname + "\" saved.";
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "LobbyPresets.Save: saving preset"); return "Failed."; }
		}

		internal static string Apply(string pname)
		{
			try
			{
				EnsureLoaded();
				if(!_data.TryGetValue(pname, out string payload)) return "No such preset.";
				if(!LobbySettings.Ready()) return "Host in lobby only.";
				return LobbySettings.ApplyState(payload) ? "Applied \"" + pname + "\"." : "Corrupt preset.";
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "LobbyPresets.Apply: applying preset"); return "Failed."; }
		}

		internal static void Delete(string pname)
		{
			try
			{
				EnsureLoaded();
				if(!_data.ContainsKey(pname)) return;
				_names.Remove(pname);
				_data.Remove(pname);
				Flush();
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "LobbyPresets.Delete: deleting preset"); }
		}

		private static string Clean(string s)
		{
			if(string.IsNullOrEmpty(s)) return "";
			s = s.Replace("|", "").Replace("\n", "").Replace("\r", "").Trim();
			if(s.Length > 20) s = s.Substring(0, 20);
			return s;
		}
	}
}
