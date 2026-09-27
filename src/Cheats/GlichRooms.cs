using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using AmongUs.Data;
using BepInEx.Unity.IL2CPP.Utils;
using HarmonyLib;
using Il2CppInterop.Runtime;
using InnerNet;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using Object = UnityEngine.Object;

namespace MalumMenu.Cheats
{
	// Ported from othermenu/Extras/HyperGlichRooms.cs.
	//
	// What it does: hosts a throwaway lobby, immediately starts it, waits for the results screen,
	// reads the OTHER players' lowest level out of the post-game data, then leaves. A "bug room"
	// is a lobby whose only other participant is level 1, i.e. a host farming wins. It can either
	// hunt specific codes (the last 4 alphanumeric characters, comma separated) or just cycle
	// through whatever comes up, and it logs every hit to HyperMenu/GlichRooms.txt.
	//
	// src adaptations:
	//   * othermenu reads its options from string/float ConfigEntries; here they are CheatToggles
	//     (glichCycle / glichHunt / glichLog / glichDelay) plus one real ConfigEntry<string> for
	//     the code-ending target list, because src has no string config entries of its own.
	//   * HyperText.T(ru, en) -> English literals (src is English-only by convention).
	//   * HyperToast.Push -> MalumMenu.notifications.Send(title, message, ttl).
	//   * HyperPlugin.Logger.LogInfo -> ConsoleUI.Log, which is src's log sink and which the new
	//     Event Log window's LOG tab already reads.
	//   * The stored file moved from <root>/Hyper/GlichRooms.txt to <root>/HyperMenu/GlichRooms.txt
	//     to match every other HyperMenu data file; MoveOldFile still picks up the old one.
	//   * DROPPED: othermenu's Wild() one-shot dump of every PassiveButton in the scene and the
	//     Russian "кнопка:" / "СБОРКА-4 кнопок" chatter around it. That is debug scaffolding for a
	//     developer poking at an unfamiliar build, not a feature, and it spammed the log on the very
	//     first run of every cycle. The button-name matching Hunt() fallback itself IS kept, minus
	//     the dump.
	public sealed class GlichRooms : MonoBehaviour
	{
		internal const int HandlingId = 20097;

		private enum Step
		{
			Off, Wait, Started, Ending, Reading, Out, Back
		}

		internal static GlichRooms Instance { get; private set; }
		internal static string Stage = "";
		internal static int Runs;
		internal static int Hits;
		internal static int Lvl;
		internal static int Was => Instance == null ? 0 : Instance._was;

		private Step _s;
		private float _at;
		private int _code;
		private float _gap;
		private float _cut;
		private float _poke;
		private float _look;
		private float _firm;
		private string _tail = "";
		private int _was;
		private bool _done;
		private readonly HashSet<int> _seen = new HashSet<int>();

		internal static readonly BepInEx.Configuration.ConfigEntry<string> Targets =
			MalumMenu.Plugin.Config.Bind("HyperMenu.GlichRooms", "Targets", string.Empty, "Comma separated code endings to hunt");

		private static string Dir => Path.Combine(BepInEx.Paths.GameRootPath, "HyperMenu");
		private static string Txt => Path.Combine(Dir, "GlichRooms.txt");
		private static string OldTxt => Path.Combine(BepInEx.Paths.GameRootPath, "Hyper", "GlichRooms.txt");

		internal static string TargetList => Targets?.Value ?? string.Empty;

		public void Awake()
		{
			Instance = this;
			MoveOldFile();
		}

		private static void MoveOldFile()
		{
			try
			{
				if (File.Exists(OldTxt) && !File.Exists(Txt))
				{
					Directory.CreateDirectory(Dir);
					File.Move(OldTxt, Txt);
				}
			}
			catch { }
		}

		// ------------------------------------------------------------------ environment

		private static int LocalLevel()
		{
			try
			{
				uint l = DataManager.Player.Stats.Level;
				if (l < 10000u)
				{
					return (int)l + 1;
				}
			}
			catch { }

			return 0;
		}

		private static bool InRoom() => LobbyBehaviour.Instance != null || ShipStatus.Instance != null;

		private static bool EndScreen()
		{
			try
			{
				return Object.FindObjectOfType<EndGameManager>() != null;
			}
			catch
			{
				return false;
			}
		}

		private static bool Intro()
		{
			try
			{
				IntroCutscene c = Object.FindObjectOfType<IntroCutscene>();
				if (c != null && ((Component)c).gameObject.activeInHierarchy)
				{
					return true;
				}
			}
			catch { }

			try
			{
				ShhhBehaviour s = Object.FindObjectOfType<ShhhBehaviour>();
				if (s != null && ((Component)s).gameObject.activeInHierarchy)
				{
					return true;
				}
			}
			catch { }

			return false;
		}

		private static InnerNetClient Net()
		{
			return AmongUsClient.Instance == null ? null : (InnerNetClient)AmongUsClient.Instance;
		}

		private static int Gid(InnerNetClient c) => c == null ? 0 : c.GameId;

		internal static MainMenuManager Held;

		private static MainMenuManager Menu()
		{
			if (Held != null && ((Component)Held).gameObject != null)
			{
				return Held;
			}

			try
			{
				MainMenuManager m = Object.FindObjectOfType<MainMenuManager>();
				if (m != null)
				{
					return m;
				}
			}
			catch { }

			try
			{
				foreach (Object o in Resources.FindObjectsOfTypeAll(Il2CppType.Of<MainMenuManager>()))
				{
					MainMenuManager m = o == null ? null : o.TryCast<MainMenuManager>();
					if (m != null)
					{
						return m;
					}
				}
			}
			catch (Exception e) { Say("menu search: " + e.Message); }

			return null;
		}

		// ------------------------------------------------------------------ actions

		private static bool Launch()
		{
			try
			{
				if (LobbyBehaviour.Instance == null)
				{
					return false;
				}

				GameStartManager g = DestroyableSingleton<GameStartManager>.InstanceExists
					? DestroyableSingleton<GameStartManager>.Instance
					: Object.FindObjectOfType<GameStartManager>();
				if (g == null)
				{
					return false;
				}

				g.MinPlayers = 1;
				g.startState = GameStartManager.StartingStates.Countdown;
				g.countDownTimer = 0f;
				return true;
			}
			catch
			{
				return false;
			}
		}

		private static void Finish()
		{
			try
			{
				GameManager.Instance.RpcEndGame(GameOverReason.ImpostorsByKill, false);
			}
			catch { }
		}

		private static void Quit()
		{
			try
			{
				AmongUsClient.Instance.ExitGame((DisconnectReasons)0);
			}
			catch { }
		}

		private static void Enter(int code)
		{
			try
			{
				AmongUsClient c = AmongUsClient.Instance;
				if (c == null || code == 0)
				{
					return;
				}

				((InnerNetClient)c).GameId = code;
				var it = c.CoJoinOnlineGameFromCode(code, false);
				if (it != null)
				{
					((MonoBehaviour)c).StartCoroutine(it);
				}
			}
			catch { }
		}

		private static bool Shoo()
		{
			try
			{
				DisconnectPopup pop = Object.FindObjectOfType<DisconnectPopup>();
				if (pop == null || !((Component)pop).gameObject.activeInHierarchy)
				{
					return false;
				}

				pop.Close();
				return true;
			}
			catch
			{
				return false;
			}
		}

		// ------------------------------------------------------------------ main-menu poking

		private void Nudge()
		{
			if (Time.unscaledTime < _poke)
			{
				return;
			}

			_poke = Time.unscaledTime + 0.9f;
			Poke();
		}

		private static void Poke()
		{
			if (Shoo())
			{
				return;
			}

			CreateGameOptions opt = Sheet();
			if (opt != null)
			{
				Say("Confirm");
				try
				{
					opt.Confirm();
				}
				catch { }

				return;
			}

			try
			{
				MainMenuManager m = Menu();
				if (m != null)
				{
					if (Tap(m.createGameButton))
					{
						Say("createGameButton");
						return;
					}

					if (Tap(m.PlayOnlineButton))
					{
						Say("PlayOnlineButton");
						return;
					}

					if (Tap(m.playButton))
					{
						Say("playButton");
						return;
					}
				}
				else
				{
					Say("no MainMenuManager");
				}

				if (Hunt("create game", "createbutton", "create"))
				{
					return;
				}

				if (Hunt("online"))
				{
					return;
				}

				if (Hunt("play"))
				{
					return;
				}

				Say("nothing pressable");
			}
			catch (Exception e)
			{
				Say("failed: " + e.Message);
			}
		}

		private static bool Hunt(params string[] want)
		{
			try
			{
				foreach (PassiveButton b in Object.FindObjectsOfType<PassiveButton>())
				{
					if (b == null)
					{
						continue;
					}

					string t = Label(b);
					if (t.Contains("back") || t.Contains("cancel") || t.Contains("enter code") || t.Contains("find game"))
					{
						continue;
					}

					bool ok = false;
					for (int i = 0; i < want.Length && !ok; i++)
					{
						ok = t.Contains(want[i]);
					}

					if (!ok || !Tap(b))
					{
						continue;
					}

					Say("pressed: " + t);
					return true;
				}
			}
			catch (Exception e)
			{
				Say("button search: " + e.Message);
			}

			return false;
		}

		private static string Label(PassiveButton b)
		{
			string t = ((Object)b).name.ToLowerInvariant();
			try
			{
				foreach (TMP_Text x in ((Component)b).GetComponentsInChildren<TMP_Text>(true))
				{
					if (x != null && !string.IsNullOrEmpty(x.text))
					{
						t += " " + x.text.ToLowerInvariant();
					}
				}
			}
			catch { }

			return t;
		}

		private static CreateGameOptions Sheet()
		{
			try
			{
				ConfirmCreatePopUp pop = Object.FindObjectOfType<ConfirmCreatePopUp>();
				if (pop != null && ((Component)pop).gameObject.activeInHierarchy)
				{
					CreateGameOptions own = null;
					try
					{
						own = pop.createGameOptions;
					}
					catch { }

					if (own != null)
					{
						return own;
					}
				}
			}
			catch { }

			try
			{
				CreateGameOptions o = Object.FindObjectOfType<CreateGameOptions>();
				return o != null && ((Component)o).gameObject.activeInHierarchy ? o : null;
			}
			catch
			{
				return null;
			}
		}

		private static bool Tap(PassiveButton b)
		{
			if (b == null)
			{
				return false;
			}

			try
			{
				if (!((Component)b).gameObject.activeInHierarchy || !((Behaviour)b).isActiveAndEnabled)
				{
					return false;
				}
			}
			catch
			{
				return false;
			}

			bool hit = false;
			try
			{
				if (b.OnClick != null)
				{
					((UnityEvent)b.OnClick).Invoke();
					hit = true;
				}
			}
			catch { }

			try
			{
				((PassiveUiElement)b).ReceiveClickDown();
				((PassiveUiElement)b).ReceiveClickUp();
				hit = true;
			}
			catch { }

			return hit;
		}

		private static void Say(string what)
		{
			global::MalumMenu.ConsoleUI.Log("[GlichRooms] " + what);
		}

		// ------------------------------------------------------------------ bookkeeping

		private static void Keep(int code)
		{
			try
			{
				Directory.CreateDirectory(Dir);
				string name = GameCode.IntToGameName(code);
				if (string.IsNullOrEmpty(name))
				{
					return;
				}

				foreach (string line in File.Exists(Txt) ? File.ReadAllLines(Txt) : new string[0])
				{
					if (line.StartsWith(name, StringComparison.OrdinalIgnoreCase))
					{
						return;
					}
				}

				File.AppendAllText(Txt, name + "  " + DateTime.Now.ToString("dd.MM HH:mm") + Environment.NewLine);
			}
			catch { }
		}

		internal static string Tail(int code)
		{
			try
			{
				string s = GameCode.IntToGameName(code);
				if (string.IsNullOrEmpty(s))
				{
					return "";
				}

				StringBuilder sb = new StringBuilder(8);
				foreach (char c in s)
				{
					if (char.IsLetterOrDigit(c))
					{
						sb.Append(char.ToUpperInvariant(c));
					}
				}

				string t = sb.ToString();
				return t.Length <= 4 ? t : t.Substring(t.Length - 4);
			}
			catch
			{
				return "";
			}
		}

		private static bool Listed() => !string.IsNullOrWhiteSpace(TargetList);

		private static bool Wanted(string tail)
		{
			if (tail.Length < 4)
			{
				return false;
			}

			foreach (string p in TargetList.Split(',', ';', ' ', '\n', '\r', '\t'))
			{
				string q = p.Trim();
				if (q.Length > 0 && string.Equals(q, tail, StringComparison.OrdinalIgnoreCase))
				{
					return true;
				}
			}

			return false;
		}

		internal static string[] Found()
		{
			try
			{
				return File.Exists(Txt) ? File.ReadAllLines(Txt) : new string[0];
			}
			catch
			{
				return new string[0];
			}
		}

		internal static void ClearFound()
		{
			try
			{
				if (File.Exists(Txt))
				{
					File.Delete(Txt);
				}
			}
			catch { }
		}

		private void Tally(float now)
		{
			Runs++;
			_done = true;

			bool got = Lvl == 1 && _code != 0;
			if (got)
			{
				if (CheatToggles.glichLog)
				{
					Keep(_code);
				}

				Hits++;
				MalumMenu.notifications.Send("Bug room", GameCode.IntToGameName(_code) + " — staying here", 5f);
			}

			if (got && CheatToggles.glichHunt)
			{
				CheatToggles.glichHunt = false;
				_s = Step.Wait;
				_at = now + _gap;
				Stage = "found";
				return;
			}

			_s = Step.Out;
			_at = now + 1f;
			Stage = "leaving";
		}

		// ------------------------------------------------------------------ state machine

		public void Update()
		{
			try
			{
				if (!CheatToggles.glichCycle)
				{
					if (_s != Step.Off)
					{
						_s = Step.Off;
						_at = 0f;
						_poke = 0f;
						_done = false;
						Stage = "";
					}

					return;
				}

				float now = Time.unscaledTime;
				InnerNetClient net = Net();
				int gid = Gid(net);

				if (Held == null && now >= _look)
				{
					_look = now + 1f;
					try
					{
						Held = Object.FindObjectOfType<MainMenuManager>();
					}
					catch { }
				}

				_gap = Mathf.Clamp(CheatToggles.glichDelay, 1f, 10f);

				if (Shoo())
				{
					_s = Step.Out;
					_at = now + 1.5f;
					Stage = "dropped, retry";
					return;
				}

				if (EndScreen())
				{
					if (_done)
					{
						Stage = "results";
						return;
					}

					if (_s != Step.Reading)
					{
						_s = Step.Reading;
						_at = now + 1f;
						_cut = now + 8f;
					}

					if (now < _at)
					{
						Stage = "results";
						return;
					}

					Lvl = LocalLevel();
					if (now < _cut && (Lvl == 0 || Lvl == _was))
					{
						Stage = "waiting level";
						return;
					}

					Tally(now);
					return;
				}

				if (ShipStatus.Instance != null && LobbyBehaviour.Instance == null)
				{
					if (_s != Step.Ending)
					{
						_s = Step.Ending;
						_at = now + 1f;
						_cut = now + 10f;
						_done = false;
					}

					if (Intro())
					{
						_at = now + 1f;
						Stage = "intro";
						return;
					}

					if (now < _at)
					{
						Stage = "in game";
						return;
					}

					Finish();
					_at = now + 2.5f;
					Stage = "ending";
					return;
				}

				if (LobbyBehaviour.Instance != null && gid != 0 && _s != Step.Out)
				{
					if ((_s == Step.Ending || _s == Step.Reading) && !_done)
					{
						_code = gid;
						Lvl = LocalLevel();
						if (now < _cut && (Lvl == 0 || Lvl == _was))
						{
							Stage = "waiting level";
							return;
						}

						Tally(now);
						return;
					}

					_done = false;
					if (_code != gid)
					{
						_code = gid;
						_tail = "";
						_s = Step.Wait;
						_at = now + _gap;
						if (CheatToggles.glichHunt && !_seen.Add(_code))
						{
							_s = Step.Out;
							_at = now;
							Stage = "seen already";
							return;
						}
					}

					if (CheatToggles.glichHunt && Listed())
					{
						string tail = Tail(gid);
						if (tail.Length < 4)
						{
							_tail = "";
							Stage = "waiting code";
							return;
						}

						if (tail != _tail)
						{
							_tail = tail;
							_firm = now + 0.6f;
						}

						if (now < _firm)
						{
							Stage = "reading code " + tail;
							return;
						}

						if (!Wanted(tail))
						{
							_s = Step.Out;
							_at = now;
							Stage = "skip: " + tail;
							return;
						}

						CheatToggles.glichHunt = false;
						Hits++;
						if (CheatToggles.glichLog)
						{
							Keep(gid);
						}

						MalumMenu.notifications.Send("Bug room", GameCode.IntToGameName(gid) + " — matched", 6f);
						Stage = "found " + tail;
					}

					if (net == null || !net.AmHost)
					{
						Stage = "not host";
						return;
					}

					if (_s == Step.Started)
					{
						if (now < _at)
						{
							Stage = "starting";
							return;
						}

						_s = Step.Wait;
						_at = now;
					}

					if (_s != Step.Wait)
					{
						_s = Step.Wait;
						_at = now + _gap;
					}

					if (now < _at)
					{
						Stage = "idle";
						return;
					}

					if (Launch())
					{
						_was = LocalLevel();
						_s = Step.Started;
						_at = now + 8f;
						Stage = "starting";
					}
					else
					{
						_at = now + 1f;
					}

					return;
				}

				bool hunt = CheatToggles.glichHunt;

				if (_s == Step.Out)
				{
					if (InRoom() || gid != 0)
					{
						if (now >= _at)
						{
							Quit();
							_at = now + 1.5f;
						}

						Stage = "leaving";
						return;
					}

					if (now < _at)
					{
						return;
					}

					if (hunt)
					{
						Nudge();
						_s = Step.Back;
						_at = now + 20f;
						Stage = "new room";
					}
					else
					{
						Enter(_code);
						_s = Step.Back;
						_at = now + 20f;
						Stage = "rejoining";
					}

					return;
				}

				if (_s == Step.Back)
				{
					if (InRoom())
					{
						_s = Step.Wait;
						_at = now + _gap;
						Stage = "in lobby";
						return;
					}

					if (now < _at)
					{
						if (gid != 0)
						{
							Stage = "connecting";
							return;
						}

						if (hunt)
						{
							Nudge();
						}

						return;
					}

					if (hunt)
					{
						_s = Step.Out;
						_at = now + 1f;
						return;
					}

					CheatToggles.glichCycle = false;
					try
					{
						GUIUtility.systemCopyBuffer = GameCode.IntToGameName(_code);
					}
					catch { }

					MalumMenu.notifications.Send("Glich Rooms", "Rejoin failed, code copied.", 5f);
					_s = Step.Off;
					return;
				}

				if (gid != 0)
				{
					Stage = "waiting lobby";
					return;
				}

				if (now < _at)
				{
					Stage = "idle";
					return;
				}

				if (!hunt && _code != 0)
				{
					Enter(_code);
					_s = Step.Back;
					_at = now + 20f;
					Stage = "rejoining";
					return;
				}

				Nudge();
				Stage = "creating room";
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "GlichRooms.Update"); }
		}
	}

	[HarmonyPatch(typeof(MainMenuManager), "Start")]
	internal static class GlichRooms_MenuHook
	{
		public static void Postfix(MainMenuManager __instance)
		{
			try
			{
				GlichRooms.Held = __instance;
			}
			catch (Exception ex) { ErrorReporter.Report(ex, GlichRooms.HandlingId, "GlichRooms_MenuHook.Postfix"); }
		}
	}
}
