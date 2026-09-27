using System;
using System.Collections.Generic;
using System.Text;
using BepInEx.Configuration;
using UnityEngine;

namespace MalumMenu.Cheats
{
	// Ported from othermenu/Menu/HyperSearchIndex.cs (the feature-title index) and the
	// column/opacity handling from othermenu/Menu/HyperMenu.cs + HyperStyle.cs.
	//
	// INDEX SOURCE DIFFERENCE, and it is deliberate: othermenu calls
	// `HyperSearchIndex.Note(tab, group, title)` from inside its `Card(...)` layout helper, so the
	// index fills itself as cards are drawn. HyperMenu's 19 tab bodies each build their own
	// GUILayout layout inline across ~15 separate files, so there is no single choke point to hook.
	// Instead the index is derived automatically from `CheatToggles.ToggleFields` — the same
	// reflection dictionary the profile save/load already uses — which means every cheat toggle
	// HyperMenu has ever added is searchable the moment it is declared, with zero call-site edits
	// and no way for the index to drift out of date.
	internal sealed class SearchHit
	{
		internal string Title;
		internal string Field;
		internal System.Reflection.FieldInfo Info;
		internal bool IsBool;
		internal bool Value;

		internal void Flip()
		{
			if (Info == null)
			{
				return;
			}

			try
			{
				if (IsBool)
				{
					Info.SetValue(null, !(Info.GetValue(null) is bool b && b));
				}
			}
			catch { }
		}

		internal void Set(bool v)
		{
			if (Info == null || !IsBool)
			{
				return;
			}

			try
			{
				Info.SetValue(null, v);
			}
			catch { }
		}
	}

	internal static class MenuSearch
	{
		internal const string FileHeader = "# HyperMenu feature index (tab\ttitle)";

		// The tab names, in MenuUI's registration order. Kept here as plain strings so the search
		// box can match on tab name too ("host", "sabotage", "protection", ...).
		internal static readonly string[] Tabs =
		{
			"Movement", "Self", "ESP", "Roles", "Players", "Ship", "Sabotage", "Chat",
			"Animations", "Console", "Host-Only", "Host-Only 2", "Passive", "Troll",
			"Protections", "Anticheat", "Modes", "Config", "Settings",
		};

		private static readonly List<SearchHit> Index = new List<SearchHit>();
		private static readonly List<SearchHit> Hits = new List<SearchHit>();
		private static bool built;

		internal static string Path => System.IO.Path.Combine(
			System.IO.Path.Combine(BepInEx.Paths.GameRootPath, "HyperMenu"), "searchindex.dat");

		/// <summary>Humanise a camelCase / snake_case field name into something a human would type.</summary>
		internal static string Humanise(string name)
		{
			if (string.IsNullOrEmpty(name))
			{
				return string.Empty;
			}

			StringBuilder sb = new StringBuilder(name.Length + 8);
			for (int i = 0; i < name.Length; i++)
			{
				char c = name[i];
				if (c == '_' || c == '-')
				{
					if (sb.Length > 0 && sb[sb.Length - 1] != ' ')
					{
						sb.Append(' ');
					}

					continue;
				}

				if (i > 0 && char.IsUpper(c))
				{
					char prev = name[i - 1];
					bool boundary = char.IsLower(prev) || char.IsDigit(prev)
						|| (char.IsUpper(prev) && i + 1 < name.Length && char.IsLower(name[i + 1]));
					if (boundary && sb.Length > 0 && sb[sb.Length - 1] != ' ')
					{
						sb.Append(' ');
					}
				}

				sb.Append(c);
			}

			return sb.ToString().Trim();
		}

		private static void EnsureBuilt()
		{
			if (built)
			{
				return;
			}

			built = true;

			// The tab titles themselves, so a query like "lobby" or "protection" finds the tab even
			// when no single toggle carries that word.
			for (int i = 0; i < Tabs.Length; i++)
			{
				Index.Add(new SearchHit { Title = Tabs[i] + " tab", Field = null, Info = null, IsBool = false });
			}

			// Every declared toggle, both bool and the int/float settings, plus a couple of
			// hand-written cards whose label lives in a tab body rather than a field name.
			try
			{
				foreach (KeyValuePair<string, System.Reflection.FieldInfo> kv in CheatToggles.ToggleFields)
				{
					Index.Add(new SearchHit
					{
						Title = Humanise(kv.Key),
						Field = kv.Key,
						Info = kv.Value,
						IsBool = kv.Value.FieldType == typeof(bool),
					});
				}
			}
			catch { }

			string[] extra =
			{
				"Teleport to location", "Per-map sabotage buttons", "Per-door close buttons",
				"Tasks menu", "Doors menu", "Protect menu", "Event log", "Match replay",
				"Player picker", "Lobby settings editor", "Lobby presets", "Dummies",
				"Lobby clones", "Networked clones", "Lobby pranks", "Auto host", "Lobby browser",
				"Guard access lists", "Join intel", "Recent players", "Quick menu",
				"Player detail", "Murder", "Eject", "Frame player", "Morph into player",
				"Whisper", "Mute", "Nick ban", "Ban list", "Whitelist", "Colour reservations",
				"Outfit favourites", "Friend code spoof", "Version spoof", "Platform spoof",
				"Advanced chat", "Body mode", "Gradient stamp", "Hotkeys", "Reactive host rules",
			};

			for (int i = 0; i < extra.Length; i++)
			{
				Index.Add(new SearchHit { Title = extra[i], Field = null, Info = null, IsBool = false });
			}

			Flush();
		}

		internal static int Count
		{
			get
			{
				EnsureBuilt();
				return Index.Count;
			}
		}

		/// <summary>othermenu's Collect: substring match on the lowercased title.</summary>
		internal static List<SearchHit> Collect(string query)
		{
			EnsureBuilt();
			Hits.Clear();

			if (string.IsNullOrWhiteSpace(query))
			{
				return Hits;
			}

			string q = query.Trim().ToLowerInvariant();
			for (int i = 0; i < Index.Count; i++)
			{
				SearchHit h = Index[i];
				if (h.Title != null && h.Title.ToLowerInvariant().Contains(q))
				{
					if (h.IsBool && h.Info != null)
					{
						h.Value = h.Info.GetValue(null) is bool b && b;
					}

					Hits.Add(h);
				}
			}

			return Hits;
		}

		// othermenu writes tab\tgroup\ttitle lines. src's index is derived rather than collected, so
		// the file is a plain, human-readable dump of what is searchable — useful for anyone who
		// wants to see or extend the list without recompiling.
		// Best single toggle match for a free-text cheat name, used by the Settings tab's per-cheat
		// keybind editor. Tries an exact field-name hit first, then an exact humanised-title hit,
		// then the first substring match - so "speed" finds PlayerSpeedModifier and "no clip"
		// finds NoClip. Returns null when nothing matches or the query is blank.
		internal static SearchHit FindCheat(string query)
		{
			if (string.IsNullOrWhiteSpace(query))
			{
				return null;
			}

			EnsureBuilt();

			string q = query.Trim();
			string ql = q.ToLowerInvariant();

			for (int i = 0; i < Index.Count; i++)
			{
				SearchHit h = Index[i];
				if (h?.Field != null && string.Equals(h.Field, q, System.StringComparison.OrdinalIgnoreCase))
				{
					return h;
				}
			}

			SearchHit loose = null;
			for (int i = 0; i < Index.Count; i++)
			{
				SearchHit h = Index[i];
				if (h?.Field == null || h.Title == null)
				{
					continue;
				}

				if (h.Title.ToLowerInvariant() == ql)
				{
					return h;
				}

				if (loose == null && h.Title.ToLowerInvariant().Contains(ql))
				{
					loose = h;
				}
			}

			return loose;
		}

		internal static void Flush()
		{
			try
			{
				System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));
				StringBuilder sb = new StringBuilder();
				sb.AppendLine(FileHeader);
				for (int i = 0; i < Index.Count; i++)
				{
					sb.Append(Index[i].Title);
					if (Index[i].Field != null)
					{
						sb.Append('\t').Append(Index[i].Field);
					}

					sb.Append('\n');
				}

				System.IO.File.WriteAllText(Path, sb.ToString());
			}
			catch { }
		}
	}

	// Window opacity + result columns.
	//
	// othermenu's `VanillaStyle` and `LiteMenu` are deliberately NOT ported. VanillaStyle's own
	// description is "disables lobby and main menu repaint, art and custom Start button" — all
	// of which are othermenu-specific effects HyperMenu never had (the dark lobby theme, main-menu
	// picture and custom start button were all declined), so the switch would control nothing.
	// LiteMenu strips HyperStyle's rounded-rect/glow drawing, and HyperMenu draws with plain
	// GUILayout, so there are no custom effects to strip either.
	internal static class MenuTheme
	{
		internal static ConfigEntry<float> Opacity;
		internal static ConfigEntry<int> Columns;

		private const string Section = "HyperMenu.Theme";

		private static Texture2D _original;
		private static Texture2D _tinted;
		private static float _appliedFor = -1f;

		private const float LayGap = 8f;
		private const float LayMinCol = 236f;

		internal static void Bind()
		{
			if (Opacity != null)
			{
				return;
			}

			Opacity = MalumMenu.Plugin.Config.Bind(Section, "Opacity", 1f, "Menu window opacity (0.45-1).");
			Opacity.Value = Mathf.Clamp(Opacity.Value, 0.45f, 1f);

			Columns = MalumMenu.Plugin.Config.Bind(Section, "Columns", 0, "Result columns: 0 auto, 1-3 fixed.");
			Columns.Value = Mathf.Clamp(Columns.Value, 0, 3);
		}

		internal static float OpacityValue
		{
			get
			{
				Bind();
				return Opacity.Value;
			}
		}

		internal static int ColumnCount => Mathf.Clamp(Columns != null ? Columns.Value : 0, 0, 3);

		internal static void CycleColumns()
		{
			Bind();
			Columns.Value = (Columns.Value + 1) % 4;
		}

		internal static string ColumnLabel()
		{
			int c = ColumnCount;
			return c == 0 ? "Auto" : c.ToString();
		}

		/// <summary>othermenu's ColsFor: an explicit 1-3 wins, otherwise fit as many as will fit.</summary>
		internal static int ColsFor(float w)
		{
			int forced = ColumnCount;
			if (forced > 0)
			{
				return Mathf.Clamp(forced, 1, 3);
			}

			return Mathf.Clamp(Mathf.FloorToInt((w + LayGap) / (LayMinCol + LayGap)), 1, 3);
		}

		internal static float Gap => LayGap;

		/// <summary>
		/// Alpha-only tint of the skin's window background, so the game shows through. Called from
		/// MenuUI.OnGUI because GUI.skin is only valid during a GUI pass. Restores the untouched
		/// texture whenever opacity returns to 1 so the window is byte-identical to before.
		/// </summary>
		internal static void Apply()
		{
			Bind();

			Texture2D bg = GUI.skin != null && GUI.skin.window != null ? GUI.skin.window.normal.background : null;
			if (bg == null)
			{
				return;
			}

			float want = Mathf.Clamp(Opacity.Value, 0.45f, 1f);

			if (bg != _original)
			{
				// skin changed (or first pass) — drop the stale tint and re-capture
				_original = bg;
				_tinted = null;
				_appliedFor = -1f;
			}

			if (Mathf.Approximately(want, 1f))
			{
				GUI.skin.window.normal.background = _original;
				return;
			}

			if (_tinted == null || !Mathf.Approximately(want, _appliedFor))
			{
				_tinted = MakeFaded(_original, want);
				_appliedFor = want;
			}

			GUI.skin.window.normal.background = _tinted;
		}

		private static Texture2D MakeFaded(Texture2D src, float alpha)
		{
			try
			{
				Texture2D t = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false)
				{
					hideFlags = HideFlags.HideAndDontSave,
					wrapMode = src.wrapMode,
					filterMode = src.filterMode,
				};

				Color32[] px = src.GetPixels32();
				for (int i = 0; i < px.Length; i++)
				{
					px[i].a = (byte)Mathf.Clamp(px[i].a * alpha, 0f, 255f);
				}

				t.SetPixels32(px);
				t.Apply();
				return t;
			}
			catch
			{
				return null;
			}
		}

		/// <summary>Restore the pristine skin on plugin unload.</summary>
		internal static void Reset()
		{
			if (GUI.skin != null && GUI.skin.window != null && _original != null)
			{
				GUI.skin.window.normal.background = _original;
			}

			_tinted = null;
			_appliedFor = -1f;
		}
	}
}
