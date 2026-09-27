using System;
using UnityEngine;

namespace MalumMenu.Cheats
{
	// In-match radar overlay: IMGUI panel showing nearby players as colored dots
	// relative to your position. Display-only v1 (no click/teleport).
	public class RadarPanel : MonoBehaviour
	{
		private const int HandlingId = 20040;

		private static readonly float[] Ranges = { 15f, 30f, 60f };

		private DeadBody[] _bodies = Array.Empty<DeadBody>();
		private float _nextBodyScan;

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
