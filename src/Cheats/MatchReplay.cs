using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace MalumMenu.Cheats
{
	// Ported from othermenu/Menu/HyperReplay.cs.
	//
	// othermenu drives this from a MonoBehaviour Update for path sampling plus a DrawGui for the
	// window. Here the sampling Tick comes from PlayerPhysics_LateUpdate (src's per-frame hook that
	// RadarPanel/OverheadChat/NeonOutline already use) and the window is a separate MonoBehaviour so
	// the replay map can be left open with the menu closed. The HUD's FPS/host line is a separate
	// later item, not part of this file.
	internal enum Rt
	{
		Kill = 0,
		Vent = 1,
		Report = 2,
		Shift = 3,
		Protect = 4,
		Sabotage = 5,
		Meeting = 6,
	}

	internal sealed class Ev
	{
		internal float T;
		internal Rt Type;
		internal byte Pid;
		internal int A;
		internal int B;
	}

	internal sealed class Pt
	{
		internal Vector2 P;
		internal float T;
	}

	public class MatchReplay : MonoBehaviour
	{
		private const int HandlingId = 20074;
		private const int MaxEv = 500;
		private const int MaxPts = 600;
		private const float PathGap = 0.15f;

		private static readonly List<Ev> Evs = new List<Ev>();
		private static readonly Dictionary<byte, List<Pt>> Paths = new Dictionary<byte, List<Pt>>();
		private static readonly Dictionary<byte, Color> Colors = new Dictionary<byte, Color>();

		private static float t0;
		private static bool running;
		private static float playT;
		private static bool playing;
		private static int focusPid = -1;
		private static float lastSampleAt;

		public static Rect windowRect;
		private static Vector2 scroll;

		private void Start()
		{
			windowRect = new Rect(Screen.width / 2f - 300f, 80f, 600f, 420f);
		}

		// ------------------------------------------------------------------ lifecycle

		internal static void Reset()
		{
			Evs.Clear();
			Paths.Clear();
			Colors.Clear();
			t0 = 0f;
			running = false;
			playing = false;
			playT = 0f;
			focusPid = -1;
		}

		internal static bool HasData => Evs.Count > 0 || Paths.Count > 0;

		internal static int EventCount => Evs.Count;

		internal static int TrackedCount => Paths.Count;

		// ------------------------------------------------------------------ recording

		private static float Now => Time.realtimeSinceStartup;

		private static void StartIfNeeded()
		{
			if (running)
			{
				return;
			}

			running = true;
			t0 = Now;
		}

		private static float Elapsed => Math.Max(0f, Now - t0);

		internal static void Rec(Rt type, byte pid, int a = 0, int b = 0)
		{
			try
			{
				StartIfNeeded();
				Evs.Add(new Ev { T = Elapsed, Type = type, Pid = pid, A = a, B = b });
				while (Evs.Count > MaxEv)
				{
					Evs.RemoveAt(0);
				}
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "MatchReplay.Rec"); }
		}

		internal static void Stamp()
		{
			float now = Now;
			if (now - lastSampleAt < PathGap)
			{
				return;
			}

			lastSampleAt = now;

			try
			{
				StartIfNeeded();
				float t = Elapsed;
				foreach (PlayerControl pc in PlayerControl.AllPlayerControls)
				{
					if (pc == null || pc.Data == null || pc.Data.Disconnected)
					{
						continue;
					}

					byte pid = pc.PlayerId;
					if (!Paths.TryGetValue(pid, out List<Pt> pts))
					{
						pts = new List<Pt>();
						Paths[pid] = pts;
					}

					pts.Add(new Pt { P = pc.GetTruePosition(), T = t });
					if (pts.Count > MaxPts)
					{
						pts.RemoveAt(0);
					}

					if (!Colors.ContainsKey(pid))
					{
						Colors[pid] = PlayerColor(pc);
					}
				}
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "MatchReplay.Stamp"); }
		}

		internal static void Tick()
		{
			Stamp();
			Playback();
		}

		private static void Playback()
		{
			if (!playing || !CheatToggles.replayPlayback)
			{
				return;
			}

			playT += Time.deltaTime * 2f;
			if (playT > 60f)
			{
				playT = 0f;
			}
		}

		// ------------------------------------------------------------------ clearing

		internal static void Clear()
		{
			Evs.Clear();
			playT = 0f;
			playing = false;
		}

		internal static void ClearPaths()
		{
			Paths.Clear();
			lastSampleAt = 0f;
		}

		internal static void ClearAfterMeeting()
		{
			Evs.Clear();
		}

		internal static void CycleFocus()
		{
			if (Paths.Count == 0)
			{
				focusPid = -1;
				return;
			}

			List<byte> keys = new List<byte>(Paths.Keys);
			keys.Sort();
			int idx = keys.IndexOf((byte)Mathf.Clamp(focusPid, 0, 255));
			focusPid = keys[(idx + 1) % keys.Count];
		}

		private static string NameById(byte pid)
		{
			foreach (PlayerControl pc in PlayerControl.AllPlayerControls)
			{
				if (pc != null && pc.PlayerId == pid && pc.Data != null)
				{
					return pc.Data.PlayerName;
				}
			}

			return "#" + pid;
		}

		private static Color PlayerColor(PlayerControl pc)
		{
			try
			{
				if (pc.Data != null && pc.Data.DefaultOutfit != null && Palette.PlayerColors != null)
				{
					int c = pc.Data.DefaultOutfit.ColorId;
					if (c >= 0 && c < Palette.PlayerColors.Length)
					{
						return Palette.PlayerColors[c];
					}
				}

				if (pc.CurrentOutfit != null && Palette.PlayerColors != null)
				{
					int c2 = pc.CurrentOutfit.ColorId;
					if (c2 >= 0 && c2 < Palette.PlayerColors.Length)
					{
						return Palette.PlayerColors[c2];
					}
				}
			}
			catch { }

			return Color.white;
		}

		private static Color EvColor(Ev e)
		{
			if (e.Pid != 255 && Colors.TryGetValue(e.Pid, out Color c))
			{
				return c;
			}

			return e.Type switch
			{
				Rt.Kill => new Color(1f, 0.3f, 0.3f),
				Rt.Vent => new Color(0.6f, 0.85f, 1f),
				Rt.Report => new Color(0.9f, 0.9f, 0.4f),
				Rt.Shift => new Color(0.75f, 0.6f, 1f),
				Rt.Protect => new Color(0.5f, 0.9f, 1f),
				Rt.Sabotage => new Color(1f, 0.6f, 0.2f),
				_ => new Color(1f, 0.85f, 0.3f),
			};
		}

		// ------------------------------------------------------------------ window

		private void OnGUI()
		{
			if (!CheatToggles.showReplay || MalumMenu.isPanicked)
			{
				return;
			}

			try
			{
				UIHelpers.ApplyUIColor();
				windowRect = GUI.Window((int)WindowId.ReplayUI, windowRect, (GUI.WindowFunction)DrawGui, "Match replay");
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "MatchReplay.OnGUI"); }
		}

		private static void DrawGui(int id)
		{
			GUILayout.BeginHorizontal();
			GUILayout.Label($"events {Evs.Count} · paths {Paths.Count}", GUILayout.Width(150f));
			GUILayout.FlexibleSpace();
			if (GUILayout.Button(focusPid < 0 ? "Focus: all" : $"Focus: {NameById((byte)focusPid)}", GUILayout.Width(150f)))
			{
				CycleFocus();
			}

			if (GUILayout.Button("Clear paths", GUILayout.Width(100f)))
			{
				ClearPaths();
			}

			if (GUILayout.Button("Clear all", GUILayout.Width(90f)))
			{
				Clear();
				ClearPaths();
			}

			GUILayout.EndHorizontal();

			scroll = GUILayout.BeginScrollView(scroll, false, false);
			DrawMap(new Rect(0f, 0f, 560f, 300f));
			GUILayout.EndScrollView();

			GUI.DragWindow();
		}

		private static void DrawMap(Rect area)
		{
			GUI.Box(area, "No path data yet");

			Bounds b = Bounds();
			if (b.size.sqrMagnitude <= 0.01f)
			{
				return;
			}

			// fit the union of all sampled points into the box, with a small pad
			float pad = 1.03f;
			float sx = area.width / (b.size.x * pad);
			float sy = area.height / (b.size.y * pad);
			float s = Mathf.Min(sx, sy);
			if (s <= 0f || float.IsInfinity(s) || float.IsNaN(s))
			{
				return;
			}

			float cx = b.center.x;
			float cy = b.center.y;

			Vector2 Map(Vector2 p) => new Vector2(area.x + area.width * 0.5f + (p.x - cx) * s, area.y + area.height * 0.5f - (p.y - cy) * s);

			// paths
			foreach (KeyValuePair<byte, List<Pt>> kv in Paths)
			{
				if (focusPid >= 0 && kv.Key != (byte)focusPid)
				{
					continue;
				}

				List<Pt> pts = kv.Value;
				if (pts == null || pts.Count < 2)
				{
					continue;
				}

				Colors.TryGetValue(kv.Key, out Color col);
				GUI.color = col;
				Vector2 prev = Map(pts[0].P);
				for (int i = 1; i < pts.Count; i++)
				{
					Vector2 cur = Map(pts[i].P);
					DrawLine(prev, cur, 1.5f);
					prev = cur;
				}

				GUI.color = Color.white;
			}

			// markers
			for (int i = 0; i < Evs.Count; i++)
			{
				Ev e = Evs[i];
				if (focusPid >= 0 && e.Pid != (byte)focusPid && e.Pid != 255)
				{
					continue;
				}

				Vector2 pos = PosAt(e);
				if (pos == Vector2.zero && e.Type != Rt.Meeting)
				{
					continue;
				}

				GUI.color = EvColor(e);
				float r = e.Type == Rt.Meeting ? 6f : 3.5f;
				GUI.DrawTexture(new Rect(Map(pos).x - r, Map(pos).y - r, r * 2f, r * 2f), Dot());
			}

			GUI.color = Color.white;
		}

		private static Vector2 PosAt(Ev e)
		{
			if (e.Pid != 255 && Paths.TryGetValue(e.Pid, out List<Pt> pts) && pts != null && pts.Count > 0)
			{
				float best = float.MaxValue;
				Vector2 bestPos = pts[0].P;
				for (int i = 0; i < pts.Count; i++)
				{
					float d = Mathf.Abs(pts[i].T - e.T);
					if (d < best)
					{
						best = d;
						bestPos = pts[i].P;
					}
				}

				return bestPos;
			}

			return Vector2.zero;
		}

		private static Bounds Bounds()
		{
			Bounds b = new Bounds();
			bool any = false;
			foreach (KeyValuePair<byte, List<Pt>> kv in Paths)
			{
				List<Pt> pts = kv.Value;
				if (pts == null)
				{
					continue;
				}

				for (int i = 0; i < pts.Count; i++)
				{
					if (!any)
					{
						b = new Bounds(pts[i].P, Vector2.one * 0.01f);
						any = true;
					}
					else
					{
						b.Encapsulate(pts[i].P);
					}
				}
			}

			return b;
		}

		private static Texture2D _dot;

		private static Texture2D Dot() => _dot ??= MakeDot();

		private static Texture2D MakeDot()
		{
			Texture2D t = new Texture2D(8, 8);
			for (int x = 0; x < 8; x++)
			{
				for (int y = 0; y < 8; y++)
				{
					float dx = (x - 3.5f) / 4f;
					float dy = (y - 3.5f) / 4f;
					t.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(1f - (dx * dx + dy * dy))));
				}
			}

			t.Apply();
			t.hideFlags = HideFlags.HideAndDontSave;
			return t;
		}

		private static void DrawLine(Vector2 a, Vector2 b, float w)
		{
			Color old = GUI.color;
			Vector2 d = b - a;
			float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
			float len = d.magnitude;
			GUI.matrix = Matrix4x4.TRS(a, Quaternion.Euler(0f, 0f, ang), Vector3.one) * Matrix4x4.Scale(new Vector3(len, w, 1f));
			GUI.DrawTexture(new Rect(0f, -w * 0.5f, 1f, 1f), Texture2D.whiteTexture);
			GUI.matrix = Matrix4x4.identity;
			GUI.color = old;
		}
	}

	// The eight event patchers, all also feeding the replay, ported from HyperEventNotify.
	internal static class ReplayRecorder
	{
		internal const int HandlingId = 20074;

		[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.MurderPlayer))]
		internal static class KillPatch
		{
			public static void Postfix(PlayerControl __instance, PlayerControl target, MurderResultFlags resultFlags)
			{
				try
				{
					if (!resultFlags.HasFlag(MurderResultFlags.Succeeded) || __instance == null)
					{
						return;
					}

					MatchReplay.Rec(Rt.Kill, target != null ? target.PlayerId : (byte)255, __instance.PlayerId);
				}
				catch { }
			}
		}

		[HarmonyPatch(typeof(Vent), nameof(Vent.EnterVent))]
		internal static class VentInPatch
		{
			public static void Postfix(Vent __instance, PlayerControl pc)
			{
				if (pc != null)
				{
					MatchReplay.Rec(Rt.Vent, pc.PlayerId, 1);
				}
			}
		}

		[HarmonyPatch(typeof(Vent), nameof(Vent.ExitVent))]
		internal static class VentOutPatch
		{
			public static void Postfix(Vent __instance, PlayerControl pc)
			{
				if (pc != null)
				{
					MatchReplay.Rec(Rt.Vent, pc.PlayerId, 0);
				}
			}
		}

		[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CmdReportDeadBody))]
		internal static class ReportPatch
		{
			public static void Postfix(PlayerControl __instance, [HarmonyArgument(0)] NetworkedPlayerInfo target)
			{
				if (target != null)
				{
					MatchReplay.Rec(Rt.Report, __instance != null ? __instance.PlayerId : (byte)255, target.PlayerId);
				}
			}
		}

		[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.Shapeshift))]
		internal static class ShiftPatch
		{
			public static void Postfix(PlayerControl __instance)
			{
				if (__instance != null)
				{
					MatchReplay.Rec(Rt.Shift, __instance.PlayerId);
				}
			}
		}

		[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.ProtectPlayer))]
		internal static class ProtectPatch
		{
			public static void Postfix(PlayerControl __instance)
			{
				if (__instance != null)
				{
					MatchReplay.Rec(Rt.Protect, __instance.PlayerId);
				}
			}
		}

		[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Start))]
		internal static class MeetingPatch
		{
			public static void Postfix()
			{
				MatchReplay.Rec(Rt.Meeting, 255);
			}
		}
	}

	[HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnGameJoined))]
	internal static class Replay_ResetPatch
	{
		public static void Postfix()
		{
			MatchReplay.Reset();
		}
	}

	[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Close))]
	internal static class Replay_MeetingClosePatch
	{
		public static void Postfix()
		{
			if (CheatToggles.replayClearAfterMeeting)
			{
				MatchReplay.ClearAfterMeeting();
			}
		}
	}
}
