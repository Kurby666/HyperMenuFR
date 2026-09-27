using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace MalumMenu.Cheats
{
	// Overhead chat: show chat messages as speech bubbles above players.
	// Ported from othermenu Cheats/NocturneOverheadChat.cs.
	public class OverheadChat : MonoBehaviour
	{
		private const int HandlingId = 20041;
		private const int MaxLen = 60;

		private sealed class Bubble
		{
			internal string Text;
			internal float At;
		}

		private static readonly Dictionary<byte, Bubble> _bubbles = new Dictionary<byte, Bubble>();
		private static readonly List<byte> _drop = new List<byte>(8);

		private GUIStyle _box;
		private GUIStyle _text;

		internal static void Feed(PlayerControl src, string msg)
		{
			try
			{
				if(!CheatToggles.overheadChat) return;
				if(src == null || src.Data == null || string.IsNullOrWhiteSpace(msg)) return;
				if(msg.TrimStart().StartsWith("/")) return;

				string clean = StripTags(msg).Replace('\n', ' ').Replace('\r', ' ').Trim();
				if(clean.Length == 0) return;
				if(clean.Length > MaxLen) clean = clean.Substring(0, MaxLen - 1) + "…";

				_bubbles[src.PlayerId] = new Bubble { Text = clean, At = Time.unscaledTime };
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "OverheadChat.Feed: feeding chat bubble"); }
		}

		internal static void Clear()
		{
			try { _bubbles.Clear(); }
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "OverheadChat.Clear: clearing chat bubbles"); }
		}

		private static string StripTags(string s)
		{
			if(string.IsNullOrEmpty(s) || s.IndexOf('<') < 0) return s;
			StringBuilder sb = new StringBuilder(s.Length);
			bool inTag = false;
			foreach(char c in s)
			{
				if(c == '<') { inTag = true; continue; }
				if(c == '>')
				{
					if(inTag) { inTag = false; continue; }
				}
				if(!inTag) sb.Append(c);
			}
			return sb.ToString();
		}

		public void OnGUI()
		{
			try
			{
				if(Event.current.type != EventType.Repaint) return;
				if(!CheatToggles.overheadChat || _bubbles.Count == 0) return;
				if(MalumMenu.isPanicked) return;
				if(PlayerControl.LocalPlayer == null || PlayerControl.LocalPlayer.Data == null) return;
				if(MeetingHud.Instance != null || ExileController.Instance != null) return;
				if(HudManager.Instance != null && HudManager.Instance.Chat != null && HudManager.Instance.Chat.IsOpenOrOpening)
					return;

				bool inLobby = LobbyBehaviour.Instance != null;
				bool inMatch = !inLobby && ShipStatus.Instance != null;
				if(!inLobby && !inMatch) return;

				int where = Mathf.Clamp(CheatToggles.overheadChatWhere, 0, 2);
				if((where == 1 && !inMatch) || (where == 2 && !inLobby)) return;

				Camera cam = Camera.main;
				if(cam == null) return;
				if(_box == null) Build();

				float now = Time.unscaledTime;
				float life = Mathf.Clamp(CheatToggles.overheadChatTime, 2f, 15f);
				_drop.Clear();

				foreach(PlayerControl p in PlayerControl.AllPlayerControls)
				{
					if(p == null || p.Data == null || p.Data.Disconnected) continue;
					if(!_bubbles.TryGetValue(p.PlayerId, out Bubble b)) continue;

					float age = now - b.At;
					if(age >= life)
					{
						_drop.Add(p.PlayerId);
						continue;
					}

					Vector2 world;
					try { world = p.GetTruePosition(); }
					catch { world = new Vector2(p.transform.position.x, p.transform.position.y); }

					Vector3 raw = cam.WorldToScreenPoint(new Vector3(world.x, world.y, 0f));
					float sx = raw.x, sy = Screen.height - raw.y;
					if(sx < -200f || sx > Screen.width + 200f || sy < -120f || sy > Screen.height + 120f) continue;

					float fade = age > life - 0.6f ? Mathf.Clamp01((life - age) / 0.6f) : 1f;
					float rise = Mathf.Min(1f, age / 0.25f);
					DrawBubble(sx, sy - 65f - (1f - rise) * 10f, b.Text, fade, BodyColor(p));
				}

				for(int i = 0; i < _drop.Count; i++)
					_bubbles.Remove(_drop[i]);
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "OverheadChat.OnGUI: drawing chat bubbles"); }
		}

		private void DrawBubble(float sx, float sy, string msg, float fade, Color tint)
		{
			try
			{
				float w = Mathf.Clamp(msg.Length * 9.2f + 24f, 96f, 380f);
				float h = 30f;

				Rect box = new Rect(sx - w * 0.5f, sy - h, w, h);
				Color prev = GUI.color;
				GUI.color = new Color(1f, 1f, 1f, fade);
				GUI.Box(box, "", _box);

				_text.normal.textColor = new Color(0.96f, 0.97f, 1f, fade);
				Rect inner = new Rect(box.x + 12f, box.y + 4f, box.width - 24f, box.height - 8f);
				GUI.Label(inner, msg, _text);

				// Tinted edge bar under the bubble.
				Rect edge = new Rect(box.x + 8f, box.y + box.height - 4f, box.width - 16f, 2f);
				Color prevBg = GUI.backgroundColor;
				GUI.backgroundColor = new Color(tint.r, tint.g, tint.b, 0.85f * fade);
				GUI.Box(edge, "", GUIStyle.none);
				GUI.backgroundColor = prevBg;

				GUI.color = prev;
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "OverheadChat.DrawBubble: drawing bubble"); }
		}

		private static Color BodyColor(PlayerControl p)
		{
			try
			{
				int id = p.CurrentOutfit.ColorId;
				if(id >= 0 && id < Palette.PlayerColors.Length) return Palette.PlayerColors[id];
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "OverheadChat.BodyColor: resolving body color"); }
			return Color.white;
		}

		private void Build()
		{
			_box = new GUIStyle(GUI.skin.box)
			{
				normal = { background = Texture2D.whiteTexture },
			};
			_box.normal.background = MakeTex(new Color(0.05f, 0.06f, 0.09f, 0.93f));
			_text = new GUIStyle(GUI.skin.label)
			{
				fontSize = 15,
				fontStyle = FontStyle.Bold,
				alignment = TextAnchor.MiddleCenter,
				wordWrap = true,
				richText = false,
			};
		}

		private static Texture2D MakeTex(Color c)
		{
			Texture2D t = new Texture2D(1, 1);
			t.SetPixel(0, 0, c);
			t.Apply();
			return t;
		}

		[HarmonyPatch(typeof(ChatController), nameof(ChatController.AddChat))]
		internal static class OverheadChatFeed
		{
			static void Postfix(PlayerControl sourcePlayer, string chatText)
			{
				try { Feed(sourcePlayer, chatText); }
				catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "OverheadChatFeed.Postfix: feeding chat bubble"); }
			}
		}

		[HarmonyPatch(typeof(LobbyBehaviour), nameof(LobbyBehaviour.Start))]
		internal static class OverheadChatReset
		{
			static void Postfix()
			{
				try { Clear(); }
				catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "OverheadChatReset.Postfix: clearing chat bubbles"); }
			}
		}
	}
}
