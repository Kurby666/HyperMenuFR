using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using InnerNet;
using UnityEngine;

namespace MalumMenu.Cheats
{
	// Ported from othermenu/Menu/NocturneEventLog.cs + othermenu/Security/NocturneEventNotify.cs.
	//
	// src substitution: othermenu keeps a single flat list that both the event window and the
	// toast reporter write to. Here EventLog is that list, and the window is a MonoBehaviour added
	// next to RadarPanel/OverheadChat so it draws with the menu closed. The window's second tab
	// ("LOG") reads ConsoleUI.GetRecentEntries instead of othermenu re-reading NocturneChatLog's
	// file by length change - src already mirrors chat into the same in-memory console tail, so
	// there is no second file to watch and no second copy of the text to keep in sync.
	internal enum EventCat
	{
		Kill = 0,
		Meeting = 1,
		Sabotage = 2,
		Vent = 3,
		Role = 4,
		Report = 5,
		Join = 6,
		Other = 7,
	}

	internal sealed class EventRow
	{
		internal string Clock;
		internal string Text;
		internal string Kind;
		internal EventCat Cat;
		internal Color H;
	}

	internal static class EventLog
	{
		internal const int HandlingId = 20073;

		private static readonly List<EventRow> Rows = new List<EventRow>();
		private const int Cap = 200;
		private static readonly Dictionary<EventCat, Color> CatColors = new Dictionary<EventCat, Color>
		{
			{ EventCat.Kill, new Color(1f, 0.42f, 0.42f) },
			{ EventCat.Meeting, new Color(1f, 0.83f, 0.4f) },
			{ EventCat.Sabotage, new Color(1f, 0.6f, 0.25f) },
			{ EventCat.Vent, new Color(0.6f, 0.85f, 1f) },
			{ EventCat.Role, new Color(0.75f, 0.6f, 1f) },
			{ EventCat.Report, new Color(0.9f, 0.9f, 0.5f) },
			{ EventCat.Join, new Color(0.6f, 1f, 0.7f) },
			{ EventCat.Other, new Color(0.85f, 0.85f, 0.85f) },
		};

		// one bit per category; all on by default
		internal static int FilterMask = 0xFF;

		internal static bool Enabled(EventCat cat) => (FilterMask & (1 << (int)cat)) != 0;

		internal static void ToggleCategory(EventCat cat) => FilterMask ^= 1 << (int)cat;

		internal static void EnableAll() => FilterMask = 0xFF;

		internal static void Clear()
		{
			Rows.Clear();
		}

		internal static int Count => Rows.Count;

		internal static List<EventRow> All => Rows;

		internal static string Text()
		{
			StringBuilder sb = new StringBuilder();
			for (int i = 0; i < Rows.Count; i++)
			{
				sb.Append('[').Append(Rows[i].Clock).Append("] ").Append(Rows[i].Text).Append('\n');
			}

			return sb.ToString();
		}

		internal static void Add(string text, string kind)
		{
			Add(text, kind, EventCat.Other);
		}

		internal static void Add(string text, string kind, EventCat cat)
		{
			if (string.IsNullOrEmpty(text))
			{
				return;
			}

			try
			{
				Rows.Add(new EventRow
				{
					Clock = DateTime.Now.ToString("HH:mm:ss"),
					Text = text,
					Kind = kind,
					Cat = cat,
					H = CatColors.TryGetValue(cat, out Color c) ? c : Color.white,
				});

				while (Rows.Count > Cap)
				{
					Rows.RemoveAt(0);
				}
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "EventLog.Add"); }
		}

		// The one reporter every guard patch goes through. Mirrors othermenu's
		// NocturneSecurityNotify.Fire / NocturneEventNotify.Fire split: the row is always
		// recorded, the toast only fires when the matching toggle is on.
		internal static void Fire(string kind, string text, bool toast, EventCat cat, float ttl = 3.5f)
		{
			Add(text, kind, cat);
			if (CheatToggles.mirrorEventsToConsole)
			{
				ConsoleUI.Log(text);
			}

			if (toast)
			{
				MalumMenu.notifications.Send(kind, text, ttl);
			}
		}

		internal static string ByClient(int clientId)
		{
			InnerNetClient net = AmongUsClient.Instance as InnerNetClient;
			return net != null ? AccessLists.ClientName(net, clientId) : "#" + clientId;
		}

		internal static string PName(PlayerControl p)
		{
			try
			{
				if (p != null && p.Data != null)
				{
					return p.Data.PlayerName;
				}
			}
			catch { }

			return "?";
		}

		internal static bool Mine(PlayerControl p) => p != null && p == PlayerControl.LocalPlayer;

		internal static void Tick()
		{
			// rising-edge sabotage detector, ported from NocturneEventNotify.Update
			if (!CheatToggles.notifySabotage)
			{
				return;
			}

			try
			{
				ShipStatus ship = ShipStatus.Instance;
				if (ship == null || ship.Systems == null)
				{
					return;
				}

				var e = ship.Systems.GetEnumerator();
				while (e.MoveNext())
				{
					Il2CppSystem.Collections.Generic.KeyValuePair<SystemTypes, ISystemType> kv = e.Current;
					bool active = false;
					try
					{
						IActivatable act = kv.Value != null ? kv.Value.TryCast<IActivatable>() : null;
						active = act != null && act.IsActive;
					}
					catch
					{
						continue;
					}

					int slot = SabSlot(kv.Key);
					if (slot < 0)
					{
						continue;
					}

					if (active && !SabPrev[slot])
					{
						Fire("Sabotage", SabName(kv.Key), true, EventCat.Sabotage, 3f);
					}

					SabPrev[slot] = active;
				}
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "EventLog.Tick"); }
		}

		private static readonly bool[] SabPrev = new bool[5];

		// othermenu's SabSys table, in the same slot order
		private static readonly SystemTypes[] SabSys =
		{
			SystemTypes.Reactor,
			SystemTypes.LifeSupp,
			SystemTypes.Comms,
			SystemTypes.HeliSabotage,
			SystemTypes.MushroomMixupSabotage,
		};

		private static int SabSlot(SystemTypes t)
		{
			for (int i = 0; i < SabSys.Length; i++)
			{
				if (SabSys[i] == t)
				{
					return i;
				}
			}

			return -1;
		}

		private static string SabName(SystemTypes t) => t switch
		{
			SystemTypes.Reactor => "Reactor sabotage",
			SystemTypes.LifeSupp => "Oxygen sabotage",
			SystemTypes.Comms => "Comms sabotage",
			SystemTypes.HeliSabotage => "Heli sabotage",
			SystemTypes.MushroomMixupSabotage => "Mushroom mixup",
			_ => t.ToString(),
		};

		private static float lastVotekickToast;

		[HarmonyPatch(typeof(VoteBanSystem), nameof(VoteBanSystem.AddVote))]
		internal static class Notify_VotePatch
		{
			public static void Postfix(int srcClient, int clientId)
			{
				try
				{
					if (!CheatToggles.notifyVotekicks)
					{
						return;
					}

					float now = Time.realtimeSinceStartup;
					if (now - lastVotekickToast < 15f)
					{
						return;
					}

					lastVotekickToast = now;
					string who = ByClient(srcClient);
					Fire("Votekick", $"{who} voted to kick {ByClient(clientId)}", true, EventCat.Meeting, 3f);
				}
				catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "Notify_VotePatch.Postfix"); }
			}
		}

		[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.MurderPlayer))]
		internal static class Notify_KillPatch
		{
			public static void Postfix(PlayerControl __instance, PlayerControl target, MurderResultFlags resultFlags)
			{
				try
				{
					if (!CheatToggles.notifyKills || !resultFlags.HasFlag(MurderResultFlags.Succeeded) || target == null || Mine(__instance))
					{
						return;
					}

					Fire("Kill", $"{PName(__instance)} killed {PName(target)}", true, EventCat.Kill, 3f);
				}
				catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "Notify_KillPatch.Postfix"); }
			}
		}

		[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Start))]
		internal static class Notify_MeetingPatch
		{
			public static void Postfix()
			{
				try
				{
					if (CheatToggles.notifyMeetings)
					{
						Fire("Meeting", "Meeting started", true, EventCat.Meeting, 3f);
					}
				}
				catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "Notify_MeetingPatch.Postfix"); }
			}
		}

		[HarmonyPatch(typeof(ExileController), nameof(ExileController.Begin))]
		internal static class Notify_EjectPatch
		{
			public static void Postfix()
			{
				try
				{
					if (CheatToggles.notifyEjects)
					{
						Fire("Eject", "A player was ejected", true, EventCat.Meeting, 3f);
					}
				}
				catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "Notify_EjectPatch.Postfix"); }
			}
		}

		[HarmonyPatch(typeof(Vent), nameof(Vent.EnterVent))]
		internal static class Notify_VentInPatch
		{
			public static void Postfix(Vent __instance, PlayerControl pc)
			{
				try
				{
					if (CheatToggles.notifyVents && !Mine(pc))
					{
						Fire("Vent", $"{PName(pc)} entered a vent", false, EventCat.Vent, 2f);
					}
				}
				catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "Notify_VentInPatch.Postfix"); }
			}
		}

		[HarmonyPatch(typeof(Vent), nameof(Vent.ExitVent))]
		internal static class Notify_VentOutPatch
		{
			public static void Postfix(Vent __instance, PlayerControl pc)
			{
				try
				{
					if (CheatToggles.notifyVents && !Mine(pc))
					{
						Fire("Vent", $"{PName(pc)} left a vent", false, EventCat.Vent, 2f);
					}
				}
				catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "Notify_VentOutPatch.Postfix"); }
			}
		}

		[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.Shapeshift))]
		internal static class Notify_ShiftPatch
		{
			public static void Postfix(PlayerControl __instance)
			{
				try
				{
					if (CheatToggles.notifyRoles && !Mine(__instance))
					{
						Fire("Shapeshift", $"{PName(__instance)} shapeshifted", false, EventCat.Role, 2f);
					}
				}
				catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "Notify_ShiftPatch.Postfix"); }
			}
		}

		[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.ProtectPlayer))]
		internal static class Notify_ProtectPatch
		{
			public static void Postfix(PlayerControl __instance)
			{
				try
				{
					if (CheatToggles.notifyRoles && !Mine(__instance))
					{
						Fire("Protect", $"{PName(__instance)} used protection", false, EventCat.Role, 2f);
					}
				}
				catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "Notify_ProtectPatch.Postfix"); }
			}
		}

		[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CmdReportDeadBody))]
		internal static class Notify_ReportPatch
		{
			public static void Postfix(PlayerControl __instance, [HarmonyArgument(0)] NetworkedPlayerInfo target)
			{
				try
				{
					if (!CheatToggles.notifyReports || Mine(__instance) || target == null)
					{
						return;
					}

					Fire("Report", $"{PName(__instance)} reported {target.PlayerName}", true, EventCat.Report, 3f);
				}
				catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "Notify_ReportPatch.Postfix"); }
			}
		}
	}

	// Ported from othermenu/Menu/NocturneEventLog.cs's MonoBehaviour window.
	public class EventLogWindow : MonoBehaviour
	{
		private const int HandlingId = 20073;
		public static Rect windowRect;
		private static Vector2 scroll;
		private static int tab;

		private void Start()
		{
			windowRect = new Rect(Screen.width / 2f - 300f, Screen.height / 2f - 220f, 600f, 440f);
		}

		private void OnGUI()
		{
			if (!CheatToggles.showEventLog || !(MenuUI.isGUIActive || MalumMenu.menuKeepSubwindowsOpen.Value) || MalumMenu.isPanicked)
			{
				return;
			}

			try
			{
				UIHelpers.ApplyUIColor();
				windowRect = GUI.Window((int)WindowId.EventLogUI, windowRect, (GUI.WindowFunction)Draw, "Event log");
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "EventLogWindow.OnGUI"); }
		}

		private static readonly (EventCat, string)[] Chips =
		{
			(EventCat.Kill, "KILL"),
			(EventCat.Meeting, "MEET"),
			(EventCat.Sabotage, "SAB"),
			(EventCat.Vent, "VENT"),
			(EventCat.Role, "ROLE"),
			(EventCat.Report, "RPT"),
			(EventCat.Join, "JOIN"),
			(EventCat.Other, "OTHER"),
		};

		private void Draw(int id)
		{
			GUILayout.BeginHorizontal();
			if (GUILayout.Toggle(tab == 0, "EVENTS", GUI.skin.button, GUILayout.Width(90f)) != (tab == 0))
			{
				tab = 0;
			}

			if (GUILayout.Toggle(tab == 1, "LOG", GUI.skin.button, GUILayout.Width(70f)) != (tab == 1))
			{
				tab = 1;
			}

			GUILayout.FlexibleSpace();
			if (GUILayout.Button("All", GUILayout.Width(50f)))
			{
				EventLog.EnableAll();
			}

			if (GUILayout.Button("Clear", GUILayout.Width(60f)))
			{
				EventLog.Clear();
			}

			if (GUILayout.Button("Copy", GUILayout.Width(60f)))
			{
				GUIUtility.systemCopyBuffer = EventLog.Text();
			}

			GUILayout.EndHorizontal();

			if (tab == 0)
			{
				GUILayout.BeginHorizontal();
				for (int i = 0; i < Chips.Length; i++)
				{
					Color old = GUI.backgroundColor;
					GUI.backgroundColor = EventLog.Enabled(Chips[i].Item1) ? Color.white : new Color(0.5f, 0.5f, 0.5f, 1f);
					if (GUILayout.Button(Chips[i].Item2, GUILayout.Width(70f)))
					{
						EventLog.ToggleCategory(Chips[i].Item1);
					}

					GUI.backgroundColor = old;
				}

				GUILayout.EndHorizontal();

				GUILayout.BeginVertical(GUI.skin.box);
				scroll = GUILayout.BeginScrollView(scroll, false, false);
				for (int i = 0; i < EventLog.All.Count; i++)
				{
					EventRow r = EventLog.All[i];
					if (!EventLog.Enabled(r.Cat))
					{
						continue;
					}

					Color old = GUI.contentColor;
					GUI.contentColor = r.H;
					GUILayout.Label($"[{r.Clock}] {r.Text}");
					GUI.contentColor = old;
				}

				GUILayout.EndScrollView();
				GUILayout.EndVertical();
			}
			else
			{
				GUILayout.BeginVertical(GUI.skin.box);
				scroll = GUILayout.BeginScrollView(scroll, false, false);
				string[] entries = ConsoleUI.GetRecentEntries(200);
				for (int i = 0; i < entries.Length; i++)
				{
					GUILayout.Label(entries[i]);
				}

				GUILayout.EndScrollView();
				GUILayout.EndVertical();
			}

			GUI.DragWindow();
		}
	}
}
