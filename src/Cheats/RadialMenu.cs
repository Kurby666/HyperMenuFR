using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace MalumMenu.Cheats
{
	// Ported from othermenu/Menu/NocturneQuick.cs (the 48-item quick palette) and
	// othermenu/Menu/NocturneRadial.cs (the hold-to-open ring).
	//
	// othermenu gives every quick item its own ConfigEntry<bool>. HyperMenu has no such thing —
	// every cheat toggle is a plain `public static bool` on CheatToggles, already reachable by name
	// through the existing `CheatToggles.ToggleFields` reflection dictionary. So the ids below map
	// to FIELD NAMES instead of config entries, and clicking a ring item flips the same field the
	// menu checkbox flips. That keeps one source of truth for each cheat.
	internal sealed class QuickItem
	{
		internal string Id;
		internal string Label;
		internal string Field;
		internal FieldInfo Info;

		internal bool On
		{
			get
			{
				if (Info == null)
				{
					return false;
				}

				try
				{
					return Info.GetValue(null) is bool b && b;
				}
				catch
				{
					return false;
				}
			}
			set
			{
				if (Info == null)
				{
					return;
				}

				try
				{
					Info.SetValue(null, value);
				}
				catch { }
			}
		}
	}

	internal static class QuickMenu
	{
		internal const int MaxFavs = 10;

		private static readonly List<QuickItem> Items = new List<QuickItem>();
		private static readonly Dictionary<string, QuickItem> ByIdMap = new Dictionary<string, QuickItem>();
		private static readonly List<QuickItem> Favs = new List<QuickItem>();

		internal static string FavRaw = string.Empty;

		private static bool built;

		// othermenu's ids, remapped onto HyperMenu field names. Ids whose othermenu target has no
		// HyperMenu equivalent are simply not listed; the count is therefore lower than 48.
		private static readonly (string id, string label, string field)[] Defs =
		{
			("god", "Immortal", "immortality"),
			("nukegame", "Meeting Spam", "spamMeetings"),
			("invisible", "Invisible", "invisible"),
			("mirage", "Mirage", "mirage"),
			("speed", "Speed Hack", "speedHack"),
			("invert", "Invert Controls", "invertControls"),
			("noclip", "NoClip", "noClip"),
			("freecam", "Free Cam", "freecam"),
			("zoom", "Zoom Out", "zoomOut"),
			("esp", "ESP", "seePlayerInfo"),
			("neon", "Neon Outline", "neonOutline"),
			("radar", "Radar", "showRadar"),
			("tracers", "Tracers", "tracers"),
			("taskarrows", "Task Arrows", "taskArrows"),
			("killcd", "No Kill Cooldown", "killAnyone"),
			("seevents", "See Roles", "seeRoles"),
			("seeghosts", "See Ghosts", "seeGhosts"),
			("wallhack", "Wallhack", "seeInVents"),
			("roles", "Roles ESP", "seeRoles"),
			("votes", "Reveal Votes", "revealVotes"),
			("infonames", "Player Info", "seePlayerInfo"),
			("skipshhh", "Skip Shhh", "skipShhhAnimation"),
			("ghoststart", "Ghost After Start", "ghostAfterStart"),
			("mouseteleport", "RMB Teleport", "teleportCursor"),
			("mouseselect", "Mouse Select", "mouseSelect"),
			("cosmetic", "Free Cosmetics", "freeCosmetics"),
			("namecolor", "Colored Name", "nameColor"),
			("autohost", "Auto Host", "autoHost"),
			("dummies", "Dummies", "enableDummies"),
			("doors", "Auto Doors", "autoOpenDoors"),
			("sablights", "Keep Lights Off", "keepLightsOff"),
			("sabspam", "Sabotage Spam", "spamMainSab"),
			("nowin", "No Win Conditions", "noGameEnd"),
			("spoofplatform", "Spoof Platform", "spoofLevel"),
			("telemetry", "No Telemetry", "noTelemetry"),
			("rpcguard", "RPC Guard", "rpcGuard"),
			("vkprotect", "Vote Kick Guard", "votekickProtect"),
			("betterchat", "Better Chat", "lowerRateLimits"),
			("ghostchat", "Ghost Chat", "showGhostMessages"),
			("chatlog", "Log Chat", "logChat"),
			("fullbright", "Fullbright", "fullbright"),
			("nobodies", "No Shadows", "noShadows"),
			("nocamjam", "Streamer Mode", "streamerMode"),
			("ventkick", "Vent Kick", "ventKickActive"),
			("votekick", "Votekick", "votekickAutoRejoin"),
			("meetfree", "Meeting Roam", "meetingRoam"),
			("worldtilt", "World Tilt", "worldTilt"),
			("dispatch", "Dispatch", "showEventLog"),
		};

		internal static List<QuickItem> All
		{
			get
			{
				EnsureBuilt();
				return Items;
			}
		}

		internal static QuickItem ById(string id)
		{
			EnsureBuilt();
			return id != null && ByIdMap.TryGetValue(id, out QuickItem it) ? it : null;
		}

		internal static List<QuickItem> FavList
		{
			get
			{
				EnsureBuilt();
				return Favs;
			}
		}

		private static void EnsureBuilt()
		{
			if (built)
			{
				return;
			}

			built = true;

			// ToggleFields is CheatToggles' own name->FieldInfo map, so the ring and the menu
			// checkboxes always agree.
			IReadOnlyDictionary<string, FieldInfo> map = null;
			try
			{
				map = CheatToggles.ToggleFields;
			}
			catch { }

			for (int i = 0; i < Defs.Length; i++)
			{
				(string id, string label, string field) = Defs[i];
				FieldInfo info = null;
				if (map != null)
				{
					map.TryGetValue(field, out info);
				}

				QuickItem it = new QuickItem { Id = id, Label = label, Field = field, Info = info };
				Items.Add(it);
				ByIdMap[id] = it;
			}

			ParseFavs();
		}

		private static void ParseFavs()
		{
			Favs.Clear();
			if (string.IsNullOrWhiteSpace(FavRaw))
			{
				return;
			}

			string[] parts = FavRaw.Split(',');
			for (int i = 0; i < parts.Length && Favs.Count < MaxFavs; i++)
			{
				QuickItem it = ByIdMap.TryGetValue(parts[i].Trim(), out QuickItem found) ? found : null;
				if (it != null && !Favs.Contains(it))
				{
					Favs.Add(it);
				}
			}
		}

		private static void SaveFavs()
		{
			List<string> ids = new List<string>();
			for (int i = 0; i < Favs.Count; i++)
			{
				ids.Add(Favs[i].Id);
			}

			FavRaw = string.Join(",", ids);
		}

		internal static bool IsFav(string id) => FavList.Contains(ById(id));

		internal static bool ToggleFav(string id)
		{
			QuickItem it = ById(id);
			if (it == null)
			{
				return false;
			}

			if (Favs.Remove(it))
			{
				SaveFavs();
				return false;
			}

			if (Favs.Count >= MaxFavs)
			{
				return false;
			}

			Favs.Add(it);
			SaveFavs();
			return true;
		}

		internal static void ClearFavs()
		{
			Favs.Clear();
			FavRaw = string.Empty;
		}
	}

	// The ring itself. Renders in its own OnGUI so it works with the main menu closed.
	public sealed class RadialMenu : MonoBehaviour
	{
		internal const int HandlingId = 20095;

		private static readonly float OpenEaseTime = 0.16f;
		private static readonly float MinRadius = 150f;
		private static readonly float MaxRadius = 235f;
		private static readonly int Sparkles = 12;

		private GUIStyle _label;
		private GUIStyle _center;
		private GUIStyle _sub;
		private GUIStyle _hint;
		private GUIStyle _texStyle;

		private float _openAt = -99f;
		private Vector2[] _pos = new Vector2[0];
		private float[] _siz = new float[0];
		private float[] _pop = new float[0];

		private Texture2D _disc;
		private Texture2D _glow;

		private static readonly float[] SparkSeed = BuildSparkSeed();

		private static float[] BuildSparkSeed()
		{
			float[] a = new float[Sparkles];
			for (int i = 0; i < Sparkles; i++)
			{
				// golden-ratio spacing, same trick othermenu uses
				a[i] = i * 0.61803399f;
			}

			return a;
		}

		private void Init()
		{
			if (_label != null)
			{
				return;
			}

			_label = new GUIStyle(GUI.skin.label)
			{
				alignment = TextAnchor.MiddleCenter,
				fontSize = 12,
				richText = true,
			};

			_center = new GUIStyle(GUI.skin.label)
			{
				alignment = TextAnchor.MiddleCenter,
				fontSize = 15,
				fontStyle = FontStyle.Bold,
				richText = true,
			};

			_sub = new GUIStyle(GUI.skin.label)
			{
				alignment = TextAnchor.MiddleCenter,
				fontSize = 13,
				richText = true,
			};

			_hint = new GUIStyle(GUI.skin.label)
			{
				alignment = TextAnchor.UpperCenter,
				fontSize = 12,
				wordWrap = true,
				richText = true,
			};

			_texStyle = new GUIStyle(GUI.skin.box);

			_disc = BuildDisc(128);
			_glow = BuildDisc(160);
		}

		// othermenu's BuildDisc, verbatim: a soft-edged circle in a generated texture.
		private static Texture2D BuildDisc(int size)
		{
			try
			{
				Texture2D t = new Texture2D(size, size, TextureFormat.RGBA32, false)
				{
					hideFlags = HideFlags.HideAndDontSave,
					wrapMode = TextureWrapMode.Clamp,
					filterMode = FilterMode.Bilinear,
				};

				Color32[] px = new Color32[size * size];
				float cc = (size - 1) * 0.5f;
				float rad = cc - 0.5f;

				for (int y = 0; y < size; y++)
				{
					for (int x = 0; x < size; x++)
					{
						float dx = x - cc;
						float dy = y - cc;
						float a = Mathf.Clamp01(rad - Mathf.Sqrt(dx * dx + dy * dy) + 0.75f);
						px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
					}
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

		private static Color A(Color c, float a) => new Color(c.r, c.g, c.b, a);

		private static float Frac(float v) => v - Mathf.Floor(v);

		private static Color Accent => Color.HSVToRGB(MenuUI.hue, 0.85f, 1f);

		private void OnGUI()
		{
			if (MalumMenu.isPanicked || !CheatToggles.radialMenu)
			{
				return;
			}

			Init();

			bool open = Cheats.Hotkeys.Held(Cheats.Hotkeys.Get("Radial"));
			float now = Time.unscaledTime;

			if (open)
			{
				if (_openAt < 0f)
				{
					_openAt = now;
				}
			}
			else
			{
				_openAt = -99f;
			}

			float elapsed = now - _openAt;
			if (elapsed < 0f || elapsed > 0.9f)
			{
				return;
			}

			DrawRing(elapsed, now);
		}

		private void DrawRing(float elapsed, float now)
		{
			List<QuickItem> favs = Cheats.QuickMenu.FavList;
			float ease = 1f - (1f - Mathf.Clamp01(elapsed / OpenEaseTime));
			ease = ease * ease * ease;
			float pulse = 0.5f + 0.5f * Mathf.Sin(now * 3.2f);
			float alpha = elapsed < OpenEaseTime ? ease : Mathf.Clamp01((0.9f - elapsed) / 0.25f);

			Color accent = Accent;
			GUI.color = Color.white;
			GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.blackTexture, ScaleMode.StretchToFill, true, 0f, A(Color.black, 0.55f * alpha), 0f, 0f);

			Vector2 hub = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
			float radius = Mathf.Clamp(94f + favs.Count * 11f, MinRadius, MaxRadius);

			// orbiting sparkle discs
			for (int i = 0; i < Sparkles; i++)
			{
				float ang = Frac(SparkSeed[i] + now * 0.05f) * Mathf.PI * 2f;
				float rr = radius * (0.55f + 0.45f * Frac(SparkSeed[i] * 3.7f + now * 0.11f));
				Vector2 p = hub + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * rr;
				float s = (3f + pulse * 3f) * ease;
				Tex(_glow, p, s, A(accent, 0.10f * alpha));
			}

			if (favs.Count == 0)
			{
				GUI.color = Color.white;
				GUI.Box(new Rect(hub.x - 150f, hub.y - 60f, 300f, 120f), GUIContent.none);
				GUI.Label(new Rect(hub.x - 150f, hub.y - 34f, 300f, 30f), "<b>Quick menu</b>", _center);
				GUI.Label(new Rect(hub.x - 140f, hub.y + 2f, 280f, 52f),
					"No favourites yet.\nRight-click a menu entry to star it, then hold the Quick key.",
					_hint);
				return;
			}

			EnsureSlots(favs.Count);

			Vector2 m = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
			int hovered = -1;

			float step = 360f / favs.Count;
			for (int i = 0; i < favs.Count; i++)
			{
				QuickItem it = favs[i];
				float ang = (-90f + step * i) * Mathf.Deg2Rad;
				Vector2 p = hub + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * radius;

				float pu = Mathf.Clamp01((elapsed - (0.02f + 0.02f * i)) / 0.22f);
				_pop[i] = 1f + 2.2f * (pu - 1f) * (pu - 1f) * (pu - 1f) + 1.2f * (pu - 1f) * (pu - 1f);
				float siz = Mathf.Lerp(30f, 46f, pu);
				_siz[i] = siz;
				_pos[i] = p;

				if (Vector2.Distance(m, p) <= siz)
				{
					hovered = i;
				}
			}

			// spokes
			for (int i = 0; i < favs.Count; i++)
			{
				float thick = i == hovered ? 3f : 1.5f;
				Line(hub, _pos[i], A(Color.white, (i == hovered ? 0.35f : 0.15f) * alpha), thick);
			}

			// items
			for (int i = 0; i < favs.Count; i++)
			{
				QuickItem it = favs[i];
				bool on = it.On;
				Color tint = on ? A(accent, 0.85f) : A(new Color(0.25f, 0.25f, 0.28f), 0.85f);
				Rect r = new Rect(_pos[i].x - _siz[i] * _pop[i] * 0.5f, _pos[i].y - _siz[i] * _pop[i] * 0.5f,
					_siz[i] * _pop[i], _siz[i] * _pop[i]);
				Tex(_disc, _pos[i], _siz[i] * _pop[i] * 1.15f, A(tint, alpha));
				GUI.Label(r, WrapLabel(it.Label), _label);

				if (i == hovered)
				{
					MalumMenu.notifications.Send("Quick menu", it.Label, 1.2f);
				}
			}

			// hub
			Rect hubRect = new Rect(hub.x - 110f, hub.y - 34f, 220f, 68f);
			GUI.Box(hubRect, GUIContent.none);
			if (hovered < 0)
			{
				GUI.Label(new Rect(hub.x - 110f, hub.y - 24f, 220f, 24f), "<b>HyperMenu</b>", _center);
				GUI.Label(new Rect(hub.x - 110f, hub.y - 2f, 220f, 20f), "quick menu", _sub);
			}
			else
			{
				QuickItem it = favs[hovered];
				GUI.Label(new Rect(hub.x - 110f, hub.y - 26f, 220f, 24f), "<b>" + it.Label + "</b>", _center);
				string pill = it.On ? "<color=#7CFC7C>ON</color>" : "<color=#AAAAAA>OFF</color>";
				GUI.Label(new Rect(hub.x - 110f, hub.y - 2f, 220f, 20f), pill, _sub);
			}

			if (hovered >= 0 && Input.GetMouseButtonDown(0))
			{
				QuickItem it = favs[hovered];
				it.On = !it.On;
			}
		}

		private void EnsureSlots(int count)
		{
			if (_pos.Length >= count)
			{
				return;
			}

			_pos = new Vector2[count];
			_siz = new float[count];
			_pop = new float[count];
		}

		private static string WrapLabel(string s)
		{
			s = s ?? string.Empty;
			if (s.Length <= 12)
			{
				return s;
			}

			int mid = s.Length / 2;
			if (mid < 1 || mid >= s.Length)
			{
				return s;
			}

			return s.Substring(0, mid) + "\n" + s.Substring(mid);
		}

		private void Tex(Texture2D t, Vector2 c, float size, Color col)
		{
			if (t == null)
			{
				return;
			}

			Rect r = new Rect(c.x - size * 0.5f, c.y - size * 0.5f, size, size);
			Color prev = GUI.color;
			GUI.color = col;
			GUI.DrawTexture(r, t, ScaleMode.StretchToFill, true);
			GUI.color = prev;
		}

		private void Line(Vector2 a, Vector2 b, Color col, float thick)
		{
			float ang = Mathf.Atan2(b.y - a.y, b.x - a.x);
			float len = Vector2.Distance(a, b);
			if (len < 0.5f)
			{
				return;
			}

			Matrix4x4 old = GUI.matrix;
			GUIUtility.RotateAroundPivot(ang * Mathf.Rad2Deg, a);
			Color prev = GUI.color;
			GUI.color = col;
			GUI.DrawTexture(new Rect(a.x, a.y - thick * 0.5f, len, thick), Texture2D.whiteTexture, ScaleMode.StretchToFill, true);
			GUI.color = prev;
			GUI.matrix = old;
		}
	}
}
