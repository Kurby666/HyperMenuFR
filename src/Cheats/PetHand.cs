using System;
using System.Collections.Generic;
using Hazel;
using InnerNet;
using UnityEngine;

namespace MalumMenu.Cheats
{
	public class PetHand : MonoBehaviour
	{
		private const int HandlingId = 20048;

		private const float Speed = 5f;
		private const float RpcDelay = 0.20f;
		private const float FollowDelay = 0.30f;
		private const float FollowStep = 0.2f;
		private const float AnimDelay = 0.55f;
		private const float PaintGap = 0.35f;
		private const int PaintMax = 400;

		private static Vector2 _hand;
		private static float _elapsed;
		private static float _anim;
		private static byte _target = 255;
		private static bool _drag;

		private static Vector2 _sent;
		private static bool _hasSent;

		private static readonly List<Vector2> _pts = new List<Vector2>();
		private static int _pi;

		private static readonly List<Collider2D> _roomAreas = new List<Collider2D>();
		private static readonly List<string> _roomNames = new List<string>();
		private static float _roomsAt;
		private static int _room;

		internal static bool On { get; private set; }
		internal static bool Manual { get; private set; }
		internal static bool Paint { get; private set; }
		internal static bool Follow { get; private set; }
		internal static Vector2 Joy;

		internal static int PaintCount => _pts.Count;

		internal static bool IsTarget(byte pid) => On && !Manual && !Paint && !Follow && _target == pid;
		internal static bool IsFollow(byte pid) => On && Follow && _target == pid;
		internal static bool Holding => On && Follow;

		internal static bool HasPet()
		{
			return HasPet(PlayerControl.LocalPlayer);
		}

		internal static string ModeName()
		{
			try
			{
				if (!On) return "off";
				if (Manual) return "manual joystick";
				if (Paint) return $"painting ({_pts.Count})";
				PlayerControl t = ById(_target);
				string name = t != null && t.Data != null ? t.Data.PlayerName : "?";
				return Follow ? $"following {name}" : $"petting {name}";
			}
			catch (Exception ex)
			{
				ErrorReporter.Report(ex, HandlingId, "PetHand.ModeName: resolving mode label");
				return "on";
			}
		}

		internal static string TogglePaint()
		{
			try
			{
				if (!HasPet(PlayerControl.LocalPlayer)) return "No pet equipped.";
				if (On && Paint)
				{
					Stop();
					return "Paint: off";
				}
				Paint = true;
				Manual = false;
				Follow = false;
				On = true;
				_pi = 0;
				_anim = 0f;
				return "Paint: draw with mouse while menu is closed";
			}
			catch (Exception ex)
			{
				ErrorReporter.Report(ex, HandlingId, "PetHand.TogglePaint: toggling paint mode");
				return "Failed.";
			}
		}

		internal static string ClearPaint()
		{
			try
			{
				_pts.Clear();
				_pi = 0;
				return "Points cleared.";
			}
			catch (Exception ex)
			{
				ErrorReporter.Report(ex, HandlingId, "PetHand.ClearPaint: clearing points");
				return "Failed.";
			}
		}

		private static void RefreshRooms()
		{
			_roomAreas.Clear();
			_roomNames.Clear();
			try
			{
				ShipStatus ss = ShipStatus.Instance;
				if (ss == null || ss.AllRooms == null) return;
				foreach (var r in ss.AllRooms)
				{
					if (r == null || r.roomArea == null) continue;
					_roomAreas.Add(r.roomArea);
					_roomNames.Add(r.RoomId.ToString());
				}
			}
			catch (Exception ex)
			{
				ErrorReporter.Report(ex, HandlingId, "PetHand.RefreshRooms: enumerating ship rooms");
				_roomAreas.Clear();
				_roomNames.Clear();
			}
		}

		private static void EnsureRooms()
		{
			try
			{
				if (ShipStatus.Instance == null)
				{
					if (_roomNames.Count > 0)
					{
						_roomAreas.Clear();
						_roomNames.Clear();
					}
					return;
				}
				if (_roomNames.Count > 0 && Time.unscaledTime - _roomsAt < 1f) return;
				_roomsAt = Time.unscaledTime;
				RefreshRooms();
			}
			catch (Exception ex)
			{
				ErrorReporter.Report(ex, HandlingId, "PetHand.EnsureRooms: refreshing cached rooms");
			}
		}

		internal static string RoomName()
		{
			try
			{
				EnsureRooms();
				if (_roomNames.Count == 0) return "-";
				_room = Mathf.Clamp(_room, 0, _roomNames.Count - 1);
				return _roomNames[_room];
			}
			catch (Exception ex)
			{
				ErrorReporter.Report(ex, HandlingId, "PetHand.RoomName: resolving room label");
				return "-";
			}
		}

		internal static void RoomStep(int d)
		{
			try
			{
				EnsureRooms();
				int n = _roomNames.Count;
				if (n == 0) return;
				_room = ((_room + d) % n + n) % n;
			}
			catch (Exception ex)
			{
				ErrorReporter.Report(ex, HandlingId, "PetHand.RoomStep: cycling room");
			}
		}

		internal static string FillRoom()
		{
			try
			{
				if (!HasPet(PlayerControl.LocalPlayer)) return "No pet equipped.";
				RefreshRooms();
				if (_roomAreas.Count == 0) return "No rooms (not in match?).";
				_room = Mathf.Clamp(_room, 0, _roomAreas.Count - 1);
				Collider2D area = _roomAreas[_room];
				if (area == null) return "No room.";

				_pts.Clear();
				_pi = 0;
				try
				{
					Bounds bb = area.bounds;
					for (float px = bb.min.x; px <= bb.max.x && _pts.Count < PaintMax; px += 0.5f)
						for (float py = bb.min.y; py <= bb.max.y && _pts.Count < PaintMax; py += 0.5f)
						{
							Vector2 p = new Vector2(px, py);
							bool inside;
							try { inside = area.OverlapPoint(p); }
							catch { inside = true; }
							if (inside)
								_pts.Add(p);
						}
				}
				catch (Exception ex)
				{
					ErrorReporter.Report(ex, HandlingId, "PetHand.FillRoom: sampling room area");
				}

				if (_pts.Count == 0) return "Empty.";
				Paint = true;
				Manual = false;
				Follow = false;
				On = true;
				_anim = 0f;
				return "Filling: " + _roomNames[_room] + $" ({_pts.Count})";
			}
			catch (Exception ex)
			{
				ErrorReporter.Report(ex, HandlingId, "PetHand.FillRoom: filling room with pet");
				return "Failed.";
			}
		}

		internal static string Grab(PlayerControl pc)
		{
			try
			{
				if (pc == null || pc.Data == null) return "No target.";
				if (!HasPet(PlayerControl.LocalPlayer)) return "No pet equipped.";
				_target = pc.PlayerId;
				Manual = false;
				Paint = false;
				Follow = false;
				On = true;
				_anim = 0f;
				return "Petting: " + pc.Data.PlayerName;
			}
			catch (Exception ex)
			{
				ErrorReporter.Report(ex, HandlingId, "PetHand.Grab: grabbing target");
				return "Failed.";
			}
		}

		internal static string Chase(PlayerControl pc)
		{
			try
			{
				if (pc == null || pc.Data == null) return "No target.";
				if (!HasPet(PlayerControl.LocalPlayer)) return "No pet equipped.";
				_target = pc.PlayerId;
				Follow = true;
				Manual = false;
				Paint = false;
				On = true;
				_anim = 0f;
				_hasSent = false;
				return "Pet follows: " + pc.Data.PlayerName;
			}
			catch (Exception ex)
			{
				ErrorReporter.Report(ex, HandlingId, "PetHand.Chase: chasing target");
				return "Failed.";
			}
		}

		internal static string ToggleManual()
		{
			try
			{
				if (!HasPet(PlayerControl.LocalPlayer)) return "No pet equipped.";
				if (On && Manual)
				{
					Stop();
					return "Manual: off";
				}
				Manual = true;
				Paint = false;
				Follow = false;
				On = true;
				_hand = Vector2.zero;
				_anim = 0f;
				return "Manual: on (joystick bottom-left, menu closed)";
			}
			catch (Exception ex)
			{
				ErrorReporter.Report(ex, HandlingId, "PetHand.ToggleManual: toggling manual mode");
				return "Failed.";
			}
		}

		internal static string Stop2()
		{
			Stop();
			return "Stopped.";
		}

		internal static void Stop()
		{
			try
			{
				On = false;
				Manual = false;
				Paint = false;
				Follow = false;
				_hand = Vector2.zero;
				_target = 255;
				Joy = Vector2.zero;
				_drag = false;
				_anim = 0f;
				_hasSent = false;

				PlayerControl me = PlayerControl.LocalPlayer;
				if (me == null || me.cosmetics == null) return;
				me.moveable = true;
				if (me.MyPhysics != null && me.MyPhysics.body != null)
					me.MyPhysics.body.velocity = Vector2.zero;
				try
				{
					if (me.cosmetics.CurrentPet != null)
						me.cosmetics.CurrentPet.SetGettingPet(false, Vector2.zero);
				}
				catch (Exception ex)
				{
					ErrorReporter.Report(ex, HandlingId, "PetHand.Stop: releasing pet visual");
				}
				try { me.MyPhysics.RpcCancelPet(); }
				catch (Exception ex)
				{
					ErrorReporter.Report(ex, HandlingId, "PetHand.Stop: cancelling pet RPC");
				}
				try
				{
					if (me.NetTransform != null && MeetingHud.Instance == null)
						me.NetTransform.RpcSnapTo(me.GetTruePosition());
				}
				catch (Exception ex)
				{
					ErrorReporter.Report(ex, HandlingId, "PetHand.Stop: resyncing position");
				}
			}
			catch (Exception ex)
			{
				ErrorReporter.Report(ex, HandlingId, "PetHand.Stop: stopping pet hand");
			}
		}

		private static bool HasPet(PlayerControl me)
		{
			return me != null && me.cosmetics != null && me.cosmetics.CurrentPet != null;
		}

		private static PlayerControl ById(byte pid)
		{
			try
			{
				foreach (var pc in PlayerControl.AllPlayerControls)
				{
					if (pc != null && pc.PlayerId == pid)
						return pc;
				}
			}
			catch (Exception ex)
			{
				ErrorReporter.Report(ex, HandlingId, "PetHand.ById: looking up player");
			}
			return null;
		}

		public void Update()
		{
			try
			{
				if (!On) return;

				if (MalumMenu.isPanicked)
				{
					Stop();
					return;
				}

				PlayerControl me = PlayerControl.LocalPlayer;
				if (me == null || LobbyBehaviour.Instance != null || ShipStatus.Instance == null)
				{
					Stop();
					return;
				}
				if (MeetingHud.Instance != null) return;
				if (!HasPet(me) || me.MyPhysics == null)
				{
					Stop();
					return;
				}
				if (me.Data == null || me.Data.IsDead)
				{
					Stop();
					return;
				}

				Tick(me);
			}
			catch (Exception ex)
			{
				ErrorReporter.Report(ex, HandlingId, "PetHand.Update: ticking pet hand");
			}
		}

		private static void Tick(PlayerControl me)
		{
			try
			{
				Vector2 petPos;
				if (Paint)
				{
					me.moveable = true;
					if (_pts.Count == 0)
					{
						try { me.cosmetics.CurrentPet.SetGettingPet(false, Vector2.zero); }
						catch (Exception ex)
						{
							ErrorReporter.Report(ex, HandlingId, "PetHand.Tick: idling pet with no points");
						}
						return;
					}
					if (_pi >= _pts.Count) _pi = 0;
					petPos = _pts[_pi];
				}
				else if (Manual)
				{
					me.moveable = true;
					_hand += Joy * Speed * Time.deltaTime;
					petPos = (Vector2)me.transform.position + _hand;
				}
				else
				{
					PlayerControl t = ById(_target);
					if (t == null || t.Data == null || t.Data.Disconnected)
					{
						Stop();
						return;
					}
					if (Follow)
						me.moveable = true;
					else
					{
						me.moveable = false;
						if (me.MyPhysics.body != null)
							me.MyPhysics.body.velocity = Vector2.zero;
					}
					petPos = t.transform.position;
					try
					{
						petPos.y -= me.cosmetics.currentPet.yOffset * 2f;
					}
					catch (Exception ex)
					{
						ErrorReporter.Report(ex, HandlingId, "PetHand.Tick: applying pet height offset");
					}
				}

				try
				{
					me.cosmetics.CurrentPet.SetGettingPet(true, petPos);
				}
				catch (Exception ex)
				{
					ErrorReporter.Report(ex, HandlingId, "PetHand.Tick: positioning pet");
				}

				_anim += Time.deltaTime;
				if (_anim >= AnimDelay)
				{
					_anim = 0f;
					try
					{
						if (me.cosmetics.PettingHand != null)
							me.cosmetics.PettingHand.StartPet(me.cosmetics.currentPet);
					}
					catch (Exception ex)
					{
						ErrorReporter.Report(ex, HandlingId, "PetHand.Tick: starting pet animation");
					}
				}

				_elapsed += Time.deltaTime;
				if (_elapsed < (Follow ? FollowDelay : RpcDelay)) return;
				_elapsed = 0f;
				if (Paint)
					_pi++;

				if (Follow && _hasSent && Vector2.Distance(_sent, petPos) < FollowStep)
					return;
				_sent = petPos;
				_hasSent = true;

				try
				{
					MessageWriter w = AmongUsClient.Instance.StartRpcImmediately(
						((InnerNetObject)me.MyPhysics).NetId,
						(byte)RpcCalls.Pet,
						SendOption.Reliable,
						-1);
					NetHelpers.WriteVector2(me.GetTruePosition(), w);
					NetHelpers.WriteVector2(petPos, w);
					AmongUsClient.Instance.FinishRpcImmediately(w);
				}
				catch (Exception ex)
				{
					ErrorReporter.Report(ex, HandlingId, "PetHand.Tick: sending pet RPC");
				}
			}
			catch (Exception ex)
			{
				ErrorReporter.Report(ex, HandlingId, "PetHand.Tick: pet tick");
			}
		}

		public void OnGUI()
		{
			try
			{
				if (!On) return;
				if (MenuUI.isGUIActive || MalumMenu.isPanicked) return;
				if (HudManager.InstanceExists && HudManager.Instance.Chat != null && HudManager.Instance.Chat.IsOpenOrOpening) return;
				if (Paint)
					DrawPaint();
				else if (Manual)
					DrawJoystick();
			}
			catch (Exception ex)
			{
				ErrorReporter.Report(ex, HandlingId, "PetHand.OnGUI: drawing pet overlay");
			}
		}

		private static void DrawJoystick()
		{
			try
			{
				const float r = 45f, kr = 16f;
				Vector2 center = new Vector2(75f, Screen.height - 75f);
				Event e = Event.current;
				if (e != null)
				{
					Vector2 mp = e.mousePosition;
					if (e.type == EventType.MouseDown && e.button == 0 && Vector2.Distance(mp, center) <= r + 15f)
						_drag = true;
					else if (e.type == EventType.MouseUp && e.button == 0)
						_drag = false;
				}

				Vector2 knob = center;
				if (_drag && Event.current != null)
				{
					Vector2 d = Vector2.ClampMagnitude((Vector2)Event.current.mousePosition - center, r);
					knob = center + d;
					Joy = new Vector2(d.x / r, -d.y / r);
				}
				else
					Joy = Vector2.zero;

				Color saved = GUI.color;
				GUI.color = new Color(0f, 0f, 0f, 0.55f);
				GUI.Box(new Rect(center.x - r, center.y - r, r * 2f, r * 2f), "");
				GUI.color = new Color(0.2f, 0.9f, 0.4f, 0.9f);
				GUI.Box(new Rect(knob.x - kr, knob.y - kr, kr * 2f, kr * 2f), "");
				GUI.color = saved;

				if (GUI.Button(new Rect(center.x - 30f, center.y + r + 6f, 60f, 20f), "CENTER"))
					_hand = Vector2.zero;
			}
			catch (Exception ex)
			{
				ErrorReporter.Report(ex, HandlingId, "PetHand.DrawJoystick: drawing joystick");
			}
		}

		private static void DrawPaint()
		{
			try
			{
				Event e = Event.current;
				if (e == null || e.type != EventType.Repaint) return;
				Camera cam = Camera.main;
				if (cam == null) return;

				Color saved = GUI.color;
				GUI.color = new Color(0.2f, 0.9f, 0.4f, 0.9f);
				for (int i = 0; i < _pts.Count; i++)
				{
					Vector3 s = cam.WorldToScreenPoint(_pts[i]);
					if (s.z <= 0f) continue;
					GUI.Box(new Rect(s.x - 4f, Screen.height - s.y - 4f, 8f, 8f), "");
				}
				GUI.color = saved;

				if (!Input.GetMouseButton(0) || _pts.Count >= PaintMax)
					return;
				Vector3 w = cam.ScreenToWorldPoint(Input.mousePosition);
				Vector2 p = new Vector2(w.x, w.y);
				if (_pts.Count == 0 || Vector2.Distance(_pts[_pts.Count - 1], p) >= PaintGap)
					_pts.Add(p);
			}
			catch (Exception ex)
			{
				ErrorReporter.Report(ex, HandlingId, "PetHand.DrawPaint: drawing paint points");
			}
		}
	}
}
