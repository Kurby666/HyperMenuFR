using System;
using System.Collections.Generic;
using UnityEngine;

namespace MalumMenu.Cheats
{
	// In-match radar overlay: IMGUI panel showing nearby players as colored dots
	// relative to your position, drawn on top of a floorplan of the current map.
	// Display-only (no click/teleport).
	public class RadarPanel : MonoBehaviour
	{
		private const int HandlingId = 20040;

		private static readonly float[] Ranges = { 15f, 30f, 60f };

		private DeadBody[] _bodies = Array.Empty<DeadBody>();
		private float _nextBodyScan;

		// World-space room rectangles for the current map, cached. Refreshed on a timer rather
		// than per frame: this is a per-frame OnGUI path, and walking the room list every frame
		// is exactly the shape that made PlatformRide abort the CLR.
		private static readonly List<Rect> _rooms = new List<Rect>(32);
		private static float _nextMapScan;

		public void OnGUI()
		{
			try
			{
				if(!CheatToggles.showRadar) return;
				if(MalumMenu.isPanicked) return;
				if(Event.current.type != EventType.Repaint) return;

				PlayerControl me = PlayerControl.LocalPlayer;
				if(me == null || me.Data == null) return;
				if(ShipStatus.Instance == null) return;
				if(MeetingHud.Instance != null || ExileController.Instance != null) return;

				float size = Mathf.Clamp(CheatToggles.radarSize, 120f, 400f);
				float range = Ranges[Mathf.Clamp(CheatToggles.radarRangeIdx, 0, Ranges.Length - 1)];
				float opacity = Mathf.Clamp01(CheatToggles.radarOpacity);
				var origin = new Rect(CheatToggles.radarX, CheatToggles.radarY, size, size);

				Color prev = GUI.color;
				GUI.color = new Color(0.03f, 0.04f, 0.06f, 0.85f * opacity);
				GUI.Box(origin, GUIContent.none);
				GUI.color = prev;

				Vector2 center = origin.center;
				float scale = (size * 0.5f - 8f) / range;
				Vector2 myPos = me.GetTruePosition();

				RefreshRooms();
				DrawRooms(origin, center, myPos, scale, opacity);

				// Cross + range rings (static guides, drawn as labels to avoid texture work)
				GUI.color = new Color(1f, 1f, 1f, 0.5f * opacity);
				GUI.Label(new Rect(center.x - 4f, center.y - 8f, 8f, 16f), "+");
				GUI.color = prev;

				foreach(PlayerControl p in PlayerControl.AllPlayerControls)
				{
					try
					{
						if(p == null || p.Data == null || p.Data.Disconnected) continue;
						if(p == me) continue;

						bool dead = p.Data.IsDead;
						bool imp = p.Data.Role != null && p.Data.Role.IsImpostor;
						if(dead)
						{
							if(!CheatToggles.radarGhosts) continue;
						}
						else if(imp)
						{
							if(!CheatToggles.radarImps) continue;
						}
						else
						{
							if(!CheatToggles.radarCrew) continue;
						}

						Vector2 pos = p.GetTruePosition();
						Plot(center, myPos, pos, scale, size, dead ? Color.white : p.Data.Color, opacity, dead);
					}
					catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "RadarPanel.OnGUI: plotting player"); }
				}

				if(CheatToggles.radarBodies)
				{
					try
					{
						if(Time.unscaledTime >= _nextBodyScan)
						{
							_nextBodyScan = Time.unscaledTime + 0.5f;
							_bodies = UnityEngine.Object.FindObjectsOfType<DeadBody>();
						}
						if(_bodies != null)
						{
							foreach(DeadBody b in _bodies)
							{
								if(b == null) continue;
								Plot(center, myPos, (Vector2)b.transform.position, scale, size, Color.yellow, opacity, false);
							}
						}
					}
					catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "RadarPanel.OnGUI: plotting bodies"); }
				}
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "RadarPanel.OnGUI: drawing radar"); }
		}

		// Room bounds are pulled from each room's Collider2D, which lives in the same world
		// space as PlayerControl.GetTruePosition(), so the floorplan lines up with the dots.
		private static void RefreshRooms()
		{
			if (ShipStatus.Instance == null)
			{
				if (_rooms.Count > 0) _rooms.Clear();
				return;
			}

			if (Time.unscaledTime < _nextMapScan) return;
			_nextMapScan = Time.unscaledTime + 1f;

			PlainShipRoom[] all;
			try { all = ShipStatus.Instance.AllRooms; }
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "RadarPanel.RefreshRooms: read room list"); return; }

			if (all == null) return;

			_rooms.Clear();
			for (int i = 0; i < all.Length; i++)
			{
				try
				{
					PlainShipRoom room = all[i];
					if (room == null) continue;
					Collider2D area = room.roomArea;
					if (area == null) continue;
					Bounds b = area.bounds;
					if (b.size.x <= 0f || b.size.y <= 0f) continue;
					if (float.IsNaN(b.min.x) || float.IsNaN(b.min.y) ||
						float.IsInfinity(b.min.x) || float.IsInfinity(b.min.y)) continue;
					_rooms.Add(new Rect(b.min.x, b.min.y, b.size.x, b.size.y));
				}
				catch
				{
					// One malformed room must not stop the rest of the map.
				}
			}
		}

		private static Vector2 ToPanel(Vector2 world, Vector2 center, Vector2 myPos, float scale)
		{
			return new Vector2(center.x + (world.x - myPos.x) * scale, center.y - (world.y - myPos.y) * scale);
		}

		private static void DrawRooms(Rect area, Vector2 center, Vector2 myPos, float scale, float opacity)
		{
			Color prev = GUI.color;
			int drawn = 0;

			for (int i = 0; i < _rooms.Count; i++)
			{
				Rect w = _rooms[i];

				Vector2 a = ToPanel(w.min, center, myPos, scale);
				Vector2 b = ToPanel(w.max, center, myPos, scale);

				float x0 = Mathf.Min(a.x, b.x);
				float y0 = Mathf.Min(a.y, b.y);
				float x1 = Mathf.Max(a.x, b.x);
				float y1 = Mathf.Max(a.y, b.y);

				// Reject rooms that fall outside the radar window entirely.
				if (x1 <= area.x || x0 >= area.xMax || y1 <= area.y || y0 >= area.yMax) continue;

				drawn++;

				// Clip to the panel so a room never paints outside the box.
				float cx0 = Mathf.Max(x0, area.x);
				float cy0 = Mathf.Max(y0, area.y);
				float cx1 = Mathf.Min(x1, area.xMax);
				float cy1 = Mathf.Min(y1, area.yMax);
				if (cx1 <= cx0 || cy1 <= cy0) continue;

				GUI.color = new Color(0.58f, 0.74f, 0.95f, 0.17f * opacity);
				GUI.DrawTexture(new Rect(cx0, cy0, cx1 - cx0, cy1 - cy0), Texture2D.whiteTexture);
			}

			// Outlines only when the visible room count is sane, so a wide range cannot turn
			// this into 4 extra matrix draws per room every frame.
			if (drawn <= 24)
			{
				GUI.color = new Color(0.70f, 0.84f, 1f, 0.55f * opacity);
				for (int i = 0; i < _rooms.Count; i++)
				{
					Rect w = _rooms[i];
					Vector2 a = ToPanel(new Vector2(w.xMin, w.yMax), center, myPos, scale);
					Vector2 b = ToPanel(new Vector2(w.xMax, w.yMin), center, myPos, scale);

					if (Mathf.Max(a.x, b.x) <= area.x || Mathf.Min(a.x, b.x) >= area.xMax ||
						Mathf.Max(a.y, b.y) <= area.y || Mathf.Min(a.y, b.y) >= area.yMax) continue;

					DrawLine(a, b, 1f);
					DrawLine(new Vector2(b.x, a.y), new Vector2(a.x, b.y), 1f);
				}
			}

			GUI.color = prev;
		}

		// Same approach as MatchReplay.DrawLine, which is proven in this codebase.
		private static void DrawLine(Vector2 a, Vector2 b, float w)
		{
			Color old = GUI.color;
			Vector2 d = b - a;
			float len = d.magnitude;
			if (len <= 0.01f) { GUI.color = old; return; }
			float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
			GUI.matrix = Matrix4x4.TRS(a, Quaternion.Euler(0f, 0f, ang), Vector3.one) * Matrix4x4.Scale(new Vector3(len, w, 1f));
			GUI.DrawTexture(new Rect(0f, -w * 0.5f, 1f, 1f), Texture2D.whiteTexture);
			GUI.matrix = Matrix4x4.identity;
			GUI.color = old;
		}

		private static void Plot(Vector2 center, Vector2 myPos, Vector2 pos, float scale, float size, Color color, float opacity, bool ghost)
		{
			Vector2 d = (pos - myPos) * scale;
			float maxR = size * 0.5f - 8f;
			if(d.magnitude > maxR)
				d = d.normalized * maxR;

			float x = center.x + d.x;
			float y = center.y - d.y; // world +Y is up, screen +Y is down

			Color prev = GUI.color;
			GUI.color = new Color(color.r, color.g, color.b, opacity);
			GUI.Label(new Rect(x - 6f, y - 10f, 12f, 20f), ghost ? "x" : "●");
			GUI.color = prev;
		}

		internal static string RangeName()
		{
			float range = Ranges[Mathf.Clamp(CheatToggles.radarRangeIdx, 0, Ranges.Length - 1)];
			return $"{range:0}m";
		}

		internal static void CycleRange()
		{
			CheatToggles.radarRangeIdx = (CheatToggles.radarRangeIdx + 1) % Ranges.Length;
		}
	}
}
