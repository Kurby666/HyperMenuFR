using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using InnerNet;
using TMPro;
using UnityEngine;

namespace MalumMenu.Cheats
{
	// Ported from othermenu/Menu/NocturneHud.cs.
	//
	// SCOPE: display + hotkey dispatch only. othermenu's NocturneHud.Update is its master tick
	// dispatcher (Invisible, LobbyPhantom, Corpses, VentTp, Shield, VentKick, Jail, ColorAll,
	// Platform, TaskDrain, Pet, MeetingTools, VoteSpam, SmokeSpam, LobbyHistory, Dleks + ~40 key
	// actions). In src every one of those ticks is already wired elsewhere (ShipStatus_FixedUpdate,
	// RoutineManager.Update, PlayerPhysics patches) and every one of those key actions already
	// lives in Cheats/Hotkeys.Tick(), so only the HUD rendering is ported here.
	//
	// INTEGRATION: src already patches PingTracker.Update with a Postfix (Patches/OtherPatches.cs,
	// PingTracker_Update) that writes the author credit + coloured ping. othermenu instead uses a
	// Prefix that returns false. A Harmony Prefix returning false skips the original but Postfixes
	// on the same method still run, so adding a competing Prefix here would just get overwritten.
	// Instead the existing Postfix is gated: when CheatToggles.gradientStamp is on it delegates to
	// StatusHud.RenderTracker, and when it is off the original HyperMenu text is unchanged.
	public class StatusHud : MonoBehaviour
	{
		internal const int HandlingId = 20094;

		private const float LobbyLifetime = 10f * 60f;

		internal static int CurrentFps = 60;

		public void Update()
		{
			CurrentFps = Utils.GetFps();
		}

		// ------------------------------------------------------------------ stamp

		private const int GradSteps = 32;
		private const float GradSpeed = 12f;

		private const string Brand = "HyperMenu";

		private static readonly string[] _hot = new string[GradSteps];
		private static readonly string[] _dim = new string[GradSteps];
		private static readonly string[] _brand = new string[GradSteps];
		private static readonly string[] _ver = new string[GradSteps];
		private static readonly string[] _by = new string[GradSteps];
		private static readonly string[] _ping = new string[GradSteps];
		private static readonly string[] _ms = new string[GradSteps];
		private static readonly string[] _fps = new string[GradSteps];
		private static readonly string[] _lob = new string[GradSteps];
		private static readonly string[] _hostLbl = new string[GradSteps];
		private static readonly List<string> _segs = new List<string>(5);
		private static bool _gradReady;

		private static int Wrap(int i) => (i % GradSteps + GradSteps) % GradSteps;

		internal static string StampLine()
		{
			Gradient();
			int i = Wrap((int)(Time.unscaledTime * GradSpeed));
			return "<b>" + _brand[i] + "</b> " + _ver[i] + " " + _by[i];
		}

		private static string Tint(StringBuilder sb, string text, int p, int at, bool hot)
		{
			string[] src = hot ? _hot : _dim;
			sb.Clear();
			for (int c = 0; c < text.Length; c++)
			{
				sb.Append("<color=#").Append(src[Wrap(p - (at + c))]).Append('>').Append(text[c]).Append("</color>");
			}

			return sb.ToString();
		}

		private static string Hex(Color c)
		{
			Color32 c32 = c;
			return c32.r.ToString("X2") + c32.g.ToString("X2") + c32.b.ToString("X2");
		}

		// src's live accent source. othermenu reads NocturneStyle.Current.Accent; HyperMenu keeps its
		// hue in MenuUI.hue (the same value the RGB mode cycles), so the gradient is rebuilt
		// whenever that hue moves.
		private static void Gradient()
		{
			Color a = Color.HSVToRGB(MenuUI.hue, 0.85f, 1f);
			if (_gradReady && Approximately(a, _gradKey))
			{
				return;
			}

			_gradKey = a;
			_gradReady = true;
			Color.RGBToHSV(a, out float h, out float s, out float v);
			s = Mathf.Max(s, 0.72f);
			v = Mathf.Max(v, 0.88f);

			for (int i = 0; i < GradSteps; i++)
			{
				float t = i / (float)GradSteps;
				float w = 0.5f + 0.5f * Mathf.Sin(t * Mathf.PI * 2f);
				float spark = w * w * w * w * w * w;
				float deep = Mathf.Pow(1f - w, 3f);

				float hh = Mathf.Repeat(h + Mathf.Lerp(-0.06f, 0.06f, w), 1f);
				float ss = Mathf.Clamp01(Mathf.Lerp(Mathf.Lerp(s, 1f, deep * 0.35f), s * 0.5f, spark));
				float vv = Mathf.Clamp01(Mathf.Lerp(Mathf.Lerp(v, v * 0.78f, deep), 1f, spark));

				_hot[i] = Hex(Color.HSVToRGB(hh, ss, vv));
				_dim[i] = Hex(Color.HSVToRGB(hh, Mathf.Clamp01(ss * 0.94f), Mathf.Clamp01(vv * 0.58f)));
			}

			StringBuilder sb = new StringBuilder(64);
			for (int p = 0; p < GradSteps; p++)
			{
				_brand[p] = Tint(sb, Brand, p, 0, true);
				_ver[p] = Tint(sb, "v" + MalumMenu.hyperVersion, p, 13, false);
				_by[p] = Tint(sb, "by Simon", p, 20, false);
				_ping[p] = Tint(sb, "PING", p, 35, false);
				_ms[p] = Tint(sb, "ms", p, 43, false);
				_fps[p] = Tint(sb, "FPS", p, 49, false);
				_lob[p] = Tint(sb, "Lobby:", p, 58, false);
				_hostLbl[p] = Tint(sb, "Host:", p, 68, false);
			}
		}

		private static Color _gradKey;

		private static bool Approximately(Color a, Color b)
		{
			return Mathf.Abs(a.r - b.r) < 0.002f && Mathf.Abs(a.g - b.g) < 0.002f && Mathf.Abs(a.b - b.b) < 0.002f;
		}

		private static string BuildLine(int ping)
		{
			const string Div = "  <color=#FFFFFF>•</color>  ";
			Gradient();

			int p = (int)(Time.unscaledTime * GradSpeed);
			_segs.Clear();

			int i = Wrap(p);

			_segs.Add($"<b>{_brand[i]}</b> {_ver[i]} {_by[i]}");
			_segs.Add($"{_ping[i]} <mspace=0.56em><b><color=#FFFFFF>{ping,3}</color></b></mspace> {_ms[i]}");

			if (CheatToggles.showFps)
			{
				_segs.Add($"{_fps[i]} <mspace=0.56em><b><color=#FFFFFF>{CurrentFps,3}</color></b></mspace>");
			}

			if (CheatToggles.showLobbyTimer && TryLobbyTimer(out int remaining))
			{
				string value = $"{remaining / 60}:{remaining % 60:00}";
				_segs.Add($"{_lob[i]} <mspace=0.56em><b><color=#FFFFFF>{value}</color></b></mspace>");
			}

			if (CheatToggles.showHostLine && ShipStatus.Instance != null && LobbyBehaviour.Instance == null)
			{
				string host = HostName();
				if (host.Length > 0)
				{
					_segs.Add($"{_hostLbl[i]} <color=#{_hot[Wrap(p - 63)]}>{host}</color>");
				}
			}

			return "<size=82%>" + string.Join(Div, _segs) + "</size>";
		}

		// ------------------------------------------------------------------ ping tracker

		private static string _lastTracker;
		private static int _apId;
		private static Vector3 _apEdge;

		private static void LowerTracker(PingTracker tracker)
		{
			try
			{
				AspectPosition ap = tracker.aspectPosition;
				if (ap == null)
				{
					return;
				}

				int id = ap.GetInstanceID();
				if (id != _apId)
				{
					_apId = id;
					_apEdge = ap.DistanceFromEdge;
				}

				Vector3 want = _apEdge;
				want.y = _apEdge.y * 0.75f;
				want.x = _apEdge.x + 0.13f;
				if ((ap.DistanceFromEdge - want).sqrMagnitude > 1e-6f)
				{
					ap.DistanceFromEdge = want;
					ap.AdjustPosition();
				}
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "StatusHud.LowerTracker"); }
		}

		internal static void RenderTracker(PingTracker tracker)
		{
			if (tracker == null || tracker.text == null)
			{
				return;
			}

			LowerTracker(tracker);

			int ping = 0;
			if (AmongUsClient.Instance != null)
			{
				ping = Utils.GetPing();
			}

			string text = BuildLine(ping);
			if (text == _lastTracker)
			{
				return;
			}

			_lastTracker = text;

			TMP_Text t = tracker.text;
			t.richText = true;
			t.enableWordWrapping = false;
			t.alignment = TextAlignmentOptions.Center;
			t.lineSpacing = -6f;
			t.overflowMode = TextOverflowModes.Overflow;
			t.text = text;
		}

		// ------------------------------------------------------------------ helpers

		private static string _hostC = string.Empty;
		private static float _hostAt = -99f;

		internal static string HostName()
		{
			float now = Time.unscaledTime;
			if (now - _hostAt < 1f)
			{
				return _hostC;
			}

			_hostAt = now;

			try
			{
				ClientData host = AmongUsClient.Instance != null ? AmongUsClient.Instance.GetHost() : null;
				if (host == null)
				{
					return _hostC = string.Empty;
				}

				string raw = host.PlayerName;
				if (string.IsNullOrWhiteSpace(raw) && host.Character != null && host.Character.Data != null)
				{
					raw = host.Character.Data.PlayerName;
				}

				if (string.IsNullOrWhiteSpace(raw))
				{
					return _hostC = string.Empty;
				}

				string clean = ColorTools.StripTags(raw).Trim();
				if (clean.Length > 16)
				{
					clean = clean.Substring(0, 15) + "…";
				}

				return _hostC = AmongUsClient.Instance.AmHost ? clean + " (You)" : clean;
			}
			catch
			{
				return _hostC = string.Empty;
			}
		}

		private static int _lobbyGameId = -1;
		private static float _lobbyStart = -1f;

		internal static bool TryLobbyTimer(out int remaining)
		{
			remaining = 0;
			if (LobbyBehaviour.Instance == null || AmongUsClient.Instance == null)
			{
				_lobbyStart = -1f;
				_lobbyGameId = -1;
				return false;
			}

			int gid = AmongUsClient.Instance.GameId;
			if (_lobbyStart < 0f || gid != _lobbyGameId)
			{
				_lobbyGameId = gid;
				float seed = 0f;
				try
				{
					float elapsed = LobbyBehaviour.Instance.optionsTimer;
					if (elapsed > 0f && elapsed < LobbyLifetime)
					{
						seed = elapsed;
					}
				}
				catch
				{
					seed = 0f;
				}

				_lobbyStart = Time.realtimeSinceStartup - seed;
			}

			remaining = Mathf.Max(0, Mathf.CeilToInt(LobbyLifetime - (Time.realtimeSinceStartup - _lobbyStart)));
			return true;
		}

		internal static void ResetLobbyClock()
		{
			_lobbyStart = -1f;
			_lobbyGameId = -1;
		}

		internal static void InvalidateTracker()
		{
			_lastTracker = null;
		}
	}
}
