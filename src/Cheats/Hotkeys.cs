using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using MalumMenu.features;
using UnityEngine;

namespace MalumMenu.Cheats
{
	// Ported from othermenu/Core/HyperKeys.cs plus the whole action dispatcher that othermenu
	// keeps inside HyperHud.Update (Menu/HyperHud.cs lines 32-240).
	//
	// This is a SECOND, independent binding layer. src's existing KeybindListener
	// (Components/KeybindListener.cs) binds a single KeyCode per CheatToggles bool and toggles it
	// on key-down, persisting through the profile file. That one is left completely untouched.
	// What is ported here is the part src has no equivalent for: action hotkeys (things that are
	// not a plain bool flip - "close voting", "kick selected", "teleport to cursor"), an optional
	// modifier per hotkey, and a rebindable list shown in the menu.
	//
	// Key storage follows src's own convention: MalumMenu.menuKeybind is a ConfigEntry<string>
	// holding a key name, not a ConfigEntry<KeyCode>, so every hotkey here is a
	// ConfigEntry<string> too and the Settings-style TextField rebind UI works unchanged.

	internal sealed class HotkeyEntry
	{
		internal string Name;
		internal string Label;
		internal ConfigEntry<string> Key;
		internal int ModCache;
		internal bool Ready;
	}

	internal static class Hotkeys
	{
		internal const int HandlingId = 20093;

		internal const int NoMod = 0;
		internal const int Ctrl = 1;
		internal const int Alt = 2;
		internal const int Shift = 3;

		private const string Section = "HyperMenu.Hotkeys";

		private static readonly List<HotkeyEntry> Entries = new List<HotkeyEntry>();
		private static readonly Dictionary<string, HotkeyEntry> ByName = new Dictionary<string, HotkeyEntry>();
		private static ConfigEntry<string> _modsRaw;
		private static bool _modsLoaded;
		private static readonly Dictionary<string, int> Mods = new Dictionary<string, int>();

		private static int _stateFrame = -1;
		private static bool _ctrl, _alt, _shift;

		/// <summary>True while the hotkey rebind field has focus, so a press does not also fire an action.</summary>
		internal static bool Rebinding;

		// ------------------------------------------------------------------ table

		private static HotkeyEntry Add(string name, string label, string defKey)
		{
			HotkeyEntry e = new HotkeyEntry
			{
				Name = name,
				Label = label,
				Key = MalumMenu.Plugin.Config.Bind(Section, name, defKey, label)
			};
			Entries.Add(e);
			ByName[name] = e;
			return e;
		}

		private static void EnsureBuilt()
		{
			if (Entries.Count == 0)
			{
				Build();
			}
		}

		internal static List<HotkeyEntry> All
		{
			get
			{
				EnsureBuilt();
				return Entries;
			}
		}

		internal static HotkeyEntry Get(string name)
		{
			EnsureBuilt();
			return ByName.TryGetValue(name, out HotkeyEntry e) ? e : null;
		}

		private static void Build()
		{
			_modsRaw = MalumMenu.Plugin.Config.Bind(Section, "Modifiers", string.Empty,
				"One entry per hotkey in the form Name=1..3 (1=Ctrl 2=Alt 3=Shift). Edited by the Hotkeys card.");

			Add("CopyCode", "Copy lobby code", "F6");
			Add("RejoinLast", "Rejoin last lobby", "F7");
			Add("EndMatch", "End match", "F8");

			Add("CloseVoting", "Close voting (tally)", "F9");
			Add("CloseMeeting", "Close meeting (no eject)", "F10");
			Add("CallMeeting", "Call meeting", "F11");
			Add("MeetingRoam", "Meeting: exit and roam", "F12");

			Add("GodMode", "Become immortal", "B");
			Add("Phantom", "Phantom in lobby", "P");
			Add("Appear", "Phantom: appear", "O");
			Add("Corpse", "Leave a body", "N");
			Add("Suicide", "Suicide (impostor)", "K");

			Add("Mirage", "Mirage", "M");
			Add("Invisible", "Invisibility", "I");
			Add("NoClip", "No-clip", "H");
			Add("Zoom", "Camera zoom", "Z");
			Add("SeeGhosts", "See ghosts", "J");

			Add("VotekickToggle", "Auto-votekick: start/stop", "F1");
			Add("VotekickAll", "Votekick: vote all and stay", "F2");
			Add("VotekickHost", "Votekick: vote host", "F3");
			Add("VentKickSelect", "Vent kick: select all", "F4");
			Add("VentKick", "Vent kick: kick selected", "F5");

			Add("VentTpSend", "Vent-TP: send marked", "V");
			Add("VentTpSelect", "Vent-TP: mark all", "G");
			Add("VentTpCycle", "Vent-TP: cycle vent", "L");

			Add("SabotageAll", "Sabotage all", "X");
			Add("DoorsCloseAll", "Close all doors", "C");
			Add("ChatSpam", "Chat spam", "T");

			Add("ShowConsole", "Event console", "F13");
			Add("ShowReplay", "Replay window", "F14");
			Add("Dummies", "Dummies", "D");

			Add("ZiplineSelect", "Zipline: select all", "Q");
			Add("ZiplineDown", "Zipline: ride down", "DownArrow");
			Add("ZiplineUp", "Zipline: ride up", "UpArrow");

			Add("SendChat", "Send the chat field", "Y");
			Add("Radial", "Quick menu (hold)", "Tab");
		}

		// ------------------------------------------------------------------ modifiers

		private static void LoadMods()
		{
			_modsLoaded = true;
			Mods.Clear();

			string raw = _modsRaw != null ? _modsRaw.Value : null;
			if (string.IsNullOrEmpty(raw))
			{
				return;
			}

			string[] parts = raw.Split(';');
			for (int i = 0; i < parts.Length; i++)
			{
				int eq = parts[i].LastIndexOf('=');
				if (eq <= 0 || eq >= parts[i].Length - 1)
				{
					continue;
				}

				if (!int.TryParse(parts[i].Substring(eq + 1), out int m))
				{
					continue;
				}

				if (m < Ctrl || m > Shift)
				{
					continue;
				}

				Mods[parts[i].Substring(0, eq)] = m;
			}
		}

		private static void SaveMods()
		{
			if (_modsRaw == null)
			{
				return;
			}

			System.Text.StringBuilder sb = new System.Text.StringBuilder();
			foreach (KeyValuePair<string, int> kv in Mods)
			{
				if (sb.Length > 0)
				{
					sb.Append(';');
				}

				sb.Append(kv.Key).Append('=').Append(kv.Value);
			}

			_modsRaw.Value = sb.ToString();
		}

		internal static int Mod(HotkeyEntry e)
		{
			if (e == null)
			{
				return NoMod;
			}

			if (!e.Ready)
			{
				if (!_modsLoaded)
				{
					LoadMods();
				}

				e.ModCache = Mods.TryGetValue(e.Name, out int found) ? found : NoMod;
				e.Ready = true;
			}

			return e.ModCache;
		}

		internal static void SetMod(HotkeyEntry e, int mod)
		{
			if (e == null)
			{
				return;
			}

			if (!_modsLoaded)
			{
				LoadMods();
			}

			if (mod < Ctrl || mod > Shift)
			{
				Mods.Remove(e.Name);
			}
			else
			{
				Mods[e.Name] = mod;
			}

			e.Ready = false;
			SaveMods();
		}

		internal static void CycleMod(HotkeyEntry e) => SetMod(e, (Mod(e) + 1) % 4);

		internal static string Prefix(int mod)
		{
			if (mod == Ctrl)
			{
				return "CTRL+";
			}

			if (mod == Alt)
			{
				return "ALT+";
			}

			if (mod == Shift)
			{
				return "SHIFT+";
			}

			return string.Empty;
		}

		internal static string Display(HotkeyEntry e) => e == null ? "—" : Prefix(Mod(e)) + KeyLabel(e);

		internal static string KeyLabel(HotkeyEntry e)
		{
			if (e == null || e.Key == null || string.IsNullOrEmpty(e.Key.Value))
			{
				return "None";
			}

			return e.Key.Value;
		}

		internal static void Set(HotkeyEntry e, KeyCode k)
		{
			if (e == null || e.Key == null)
			{
				return;
			}

			if (k == KeyCode.None)
			{
				Clear(e);
				return;
			}

			e.Key.Value = k.ToString();
			e.ModCache = 0;
			e.Ready = false;
		}

		internal static void Clear(HotkeyEntry e)
		{
			if (e == null || e.Key == null)
			{
				return;
			}

			e.Key.Value = KeyCode.None.ToString();
			e.ModCache = 0;
			e.Ready = false;
		}

		// Wipes every action hotkey back to unbound. Used by the "Clear all" button in the
		// Settings tab. Safe to call while entries are still being built - EnsureBuilt first.
		internal static int ClearAll()
		{
			EnsureBuilt();
			int cleared = 0;
			for (int i = 0; i < Entries.Count; i++)
			{
				HotkeyEntry e = Entries[i];
				if (e?.Key == null)
				{
					continue;
				}

				string current = e.Key.Value;
				if (!string.IsNullOrEmpty(current) && current != KeyCode.None.ToString())
				{
					cleared++;
				}

				e.Key.Value = KeyCode.None.ToString();
				e.ModCache = 0;
				e.Ready = false;
			}

			LoadMods();
			return cleared;
		}

		internal static int BoundCount()
		{
			EnsureBuilt();
			int bound = 0;
			for (int i = 0; i < Entries.Count; i++)
			{
				HotkeyEntry e = Entries[i];
				if (e?.Key == null)
				{
					continue;
				}

				string current = e.Key.Value;
				if (!string.IsNullOrEmpty(current) && current != KeyCode.None.ToString())
				{
					bound++;
				}
			}

			return bound;
		}

		internal static bool IsModKey(KeyCode k)
		{
			return k == KeyCode.LeftControl || k == KeyCode.RightControl
				|| k == KeyCode.LeftAlt || k == KeyCode.RightAlt
				|| k == KeyCode.LeftShift || k == KeyCode.RightShift
				|| k == KeyCode.AltGr;
		}

		// ------------------------------------------------------------------ input

		private static void Refresh()
		{
			if (_stateFrame == Time.frameCount)
			{
				return;
			}

			_stateFrame = Time.frameCount;
			_ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
			_alt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
			_shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
		}

		private static bool Match(int mod)
		{
			Refresh();
			switch (mod)
			{
				case Ctrl:
					return _ctrl && !_alt && !_shift;
				case Alt:
					return _alt && !_ctrl && !_shift;
				case Shift:
					return _shift && !_ctrl && !_alt;
				default:
					return !_ctrl && !_alt && !_shift;
			}
		}

		internal static bool Down(HotkeyEntry e)
		{
			// Master off switch from the Settings tab. Every consumer routes through Down/Held -
			// the 35 action dispatchers in Tick() and RadialMenu's hold-to-open - so guarding the
			// two primitives here covers all of them at once.
			if (!CheatToggles.hotkeysEnabled)
			{
				return false;
			}

			if (e == null || e.Key == null)
			{
				return false;
			}

			if (!TryKey(e.Key.Value, out KeyCode k) || k == KeyCode.None)
			{
				return false;
			}

			if (!Match(Mod(e)))
			{
				return false;
			}

			return Input.GetKeyDown(k);
		}

		internal static bool Held(HotkeyEntry e)
		{
			if (!CheatToggles.hotkeysEnabled)
			{
				return false;
			}

			if (e == null || e.Key == null)
			{
				return false;
			}

			if (!TryKey(e.Key.Value, out KeyCode k) || k == KeyCode.None)
			{
				return false;
			}

			if (!Match(Mod(e)))
			{
				return false;
			}

			return Input.GetKey(k);
		}

		internal static bool TryKey(string name, out KeyCode key)
		{
			key = KeyCode.None;
			if (string.IsNullOrEmpty(name))
			{
				return false;
			}

			if (name.StartsWith("KeyCode.", StringComparison.Ordinal))
			{
				name = name.Substring("KeyCode.".Length);
			}

			return Enum.TryParse(name, true, out key);
		}

		// ------------------------------------------------------------------ dispatcher

		private static void Note(string title, string detail, float ttl = 1.6f)
		{
			MalumMenu.notifications.Send(title, detail, ttl);
		}

		private static void NoteOn(string title, bool on)
		{
			Note(title, on ? "On" : "Off", 1.4f);
		}

		private static bool DownName(string name) => Down(Get(name));

		private static void Safe(Action act)
		{
			try
			{
				act();
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "Hotkeys.Tick"); }
		}

		/// <summary>
		/// Fires every action hotkey whose key went down this frame. Called once per frame from
		/// Components/KeybindListener, which already owns src's per-frame input pass and already
		/// applies the chat-open guard.
		/// </summary>
		internal static void Tick()
		{
			EnsureBuilt();

			if (MalumMenu.isPanicked || Rebinding)
			{
				return;
			}

			// The menu window itself captures input; a hotkey must not fire while typing in a field.
			if (MenuUI.isGUIActive)
			{
				return;
			}

			if (HudManager.InstanceExists && HudManager.Instance.Chat && HudManager.Instance.Chat.IsOpenOrOpening)
			{
				return;
			}

			if (DownName("GodMode"))
			{
				Safe(() => Immortality.Enabled = !Immortality.Enabled);
			}

			if (DownName("Phantom"))
			{
				Safe(() =>
				{
					Invisibility.LobbyPhantom.Vanish();
					Note("Phantom", "Vanished", 1.4f);
				});
			}

			if (DownName("Appear"))
			{
				Safe(() =>
				{
					Invisibility.LobbyPhantom.Appear();
					Note("Phantom", "Appeared", 1.4f);
				});
			}

			if (DownName("Corpse"))
			{
				Safe(() => Note("Body", Cheats.LeaveBody.Drop(), 2.4f));
			}

			if (DownName("Suicide"))
			{
				Safe(() => Note("Suicide", GhostTools.SuicideNow(), 2.4f));
			}

			if (DownName("Mirage"))
			{
				Safe(() =>
				{
					CheatToggles.mirage = !CheatToggles.mirage;
					NoteOn("Mirage", CheatToggles.mirage);
				});
			}

			if (DownName("Invisible"))
			{
				Safe(() =>
				{
					CheatToggles.invisible = !CheatToggles.invisible;
					NoteOn("Invisibility", CheatToggles.invisible);
				});
			}

			if (DownName("NoClip"))
			{
				Safe(() =>
				{
					CheatToggles.noClip = !CheatToggles.noClip;
					NoteOn("No-clip", CheatToggles.noClip);
				});
			}

			if (DownName("Zoom"))
			{
				Safe(() =>
				{
					CheatToggles.zoomOut = !CheatToggles.zoomOut;
					NoteOn("Camera zoom", CheatToggles.zoomOut);
				});
			}

			if (DownName("SeeGhosts"))
			{
				Safe(() =>
				{
					CheatToggles.seeGhosts = !CheatToggles.seeGhosts;
					NoteOn("See ghosts", CheatToggles.seeGhosts);
				});
			}

			if (DownName("CloseVoting"))
			{
				Safe(() => Note("Voting", MeetingTools.CloseVoting(), 2.4f));
			}

			if (DownName("CloseMeeting"))
			{
				Safe(() => Note("Meeting", MeetingTools.CloseMeetingNoEject(), 2.4f));
			}

			if (DownName("MeetingRoam"))
			{
				Safe(() => Note("Meeting", MeetingTools.Roam(), 2.5f));
			}

			if (DownName("CallMeeting"))
			{
				Safe(() =>
				{
					Utilities.OpenMeeting(PlayerControl.LocalPlayer, null);
					Note("Meeting", "Called", 1.4f);
				});
			}

			if (DownName("VotekickToggle"))
			{
				Safe(() =>
				{
					VotekickTools.ToggleAuto();
					Note("Votekick", "Toggled", 1.4f);
				});
			}

			if (DownName("VotekickAll"))
			{
				Safe(() =>
				{
					VotekickTools.VoteAllStay();
					Note("Votekick", "Voted all and stayed", 2f);
				});
			}

			if (DownName("VotekickHost"))
			{
				Safe(() =>
				{
					VotekickTools.VoteHost();
					Note("Votekick", "Voted host", 2f);
				});
			}

			if (DownName("RejoinLast"))
			{
				Safe(() =>
				{
					VotekickTools.RejoinLast();
					Note("Lobby", "Rejoining", 1.6f);
				});
			}

			if (DownName("VentKickSelect"))
			{
				Safe(() =>
				{
					VentKick.SelectAll();
					Note("Vent kick", "Selected all", 1.4f);
				});
			}

			if (DownName("VentKick"))
			{
				Safe(() => Note("Vent kick", VentKick.KickSelected(), 2.4f));
			}

			if (DownName("VentTpSend"))
			{
				Safe(() => Note("Vent-TP", VentTpTools.SendMarked(), 1.8f));
			}

			if (DownName("VentTpSelect"))
			{
				Safe(() =>
				{
					VentTpTools.MarkAll();
					Note("Vent-TP", "Marked all", 1.4f);
				});
			}

			if (DownName("VentTpCycle"))
			{
				Safe(() => Note("Vent-TP", VentTpTools.CycleVent(1), 1.8f));
			}

			if (DownName("SabotageAll"))
			{
				Safe(() =>
				{
					Sabotage.SabotageAll();
					Note("Sabotage", "Triggered all", 1.6f);
				});
			}

			if (DownName("DoorsCloseAll"))
			{
				Safe(() =>
				{
					DoorsHandler.CloseAllDoors();
					Note("Doors", "Closed all", 1.6f);
				});
			}

			if (DownName("ChatSpam"))
			{
				Safe(() =>
				{
					CheatToggles.chatSpam = !CheatToggles.chatSpam;
					NoteOn("Chat spam", CheatToggles.chatSpam);
				});
			}

			if (DownName("GhostChat"))
			{
				Safe(() => Note("Chat", Cheats.ChatTools.ChatSender.SendNow(), 1.8f));
			}

			if (DownName("ShowConsole"))
			{
				Safe(() =>
				{
					CheatToggles.showConsole = !CheatToggles.showConsole;
					NoteOn("Event console", CheatToggles.showConsole);
				});
			}

			if (DownName("ShowReplay"))
			{
				Safe(() =>
				{
					CheatToggles.showReplay = !CheatToggles.showReplay;
					NoteOn("Replay", CheatToggles.showReplay);
				});
			}

			if (DownName("Dummies"))
			{
				Safe(() =>
				{
					CheatToggles.enableDummies = !CheatToggles.enableDummies;
					NoteOn("Dummies", CheatToggles.enableDummies);
				});
			}

			if (DownName("ZiplineSelect"))
			{
				Safe(() =>
				{
					Cheats.RideTargets.All();
					Note("Targets", "Selected " + Cheats.RideTargets.Count, 1.6f);
				});
			}

			if (DownName("ZiplineDown"))
			{
				Safe(() => Note("Zipline", Cheats.ZiplineRide.RideSelected(true), 2.4f));
			}

			if (DownName("ZiplineUp"))
			{
				Safe(() => Note("Zipline", Cheats.ZiplineRide.RideSelected(false), 2.4f));
			}

			if (DownName("EndMatch"))
			{
				Safe(() =>
				{
					if (AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost)
					{
						CheatToggles.noGameEnd = !CheatToggles.noGameEnd;
						NoteOn("No game end", CheatToggles.noGameEnd);
					}
					else
					{
						Note("End match", "Host only", 1.6f);
					}
				});
			}

		}
	}
}
