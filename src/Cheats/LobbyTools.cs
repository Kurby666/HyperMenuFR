using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using HarmonyLib;
using InnerNet;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using TMPro;

namespace MalumMenu.Cheats
{
	// Lobby toys: destroy/create lobby, confirm-gated leave, fake map in lobby,
	// auto-return after match, lobby history with rejoin. Ported from othermenu
	// Lobby/HyperLobbyTools.cs, HyperFakeMap.cs, HyperAutoLobbyReturn.cs
	// and LobbyHistory.cs. Adaptations: destroy/create reuse src's existing
	// Despawn/Spawn Lobby logic as host-guarded feedback-string helpers;
	// FakeMap spawns via src's InstantiateAsync pattern (HostOnlyTab2.SpawnMap)
	// instead of othermenu's asset-handle path; FakeTasks skipped (src already
	// has scan/cams/shields/asteroids/garbage); English-only strings.
	internal static class LobbyTools
	{
		private const int HandlingId = 20060;

		internal static string DestroyLobby()
		{
			try
			{
				if(!Utils.isHost) return "Host only.";
				LobbyBehaviour lobby = LobbyBehaviour.Instance;
				if(lobby == null) return "No lobby object right now.";
				lobby.Despawn();
				return "Lobby destroyed.";
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "LobbyTools.DestroyLobby: destroying lobby"); return "Failed."; }
		}

		internal static string CreateLobby()
		{
			try
			{
				if(!Utils.isHost) return "Host only.";
				if(LobbyBehaviour.Instance != null) return "Lobby already exists.";
				if(GameStartManager.Instance == null || GameStartManager.Instance.LobbyPrefab == null)
					return "Lobby prefab not found.";
				LobbyBehaviour.Instance = Object.Instantiate(GameStartManager.Instance.LobbyPrefab);
				AmongUsClient.Instance.Spawn(LobbyBehaviour.Instance, -2, SpawnFlags.None);
				return "Lobby re-created.";
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "LobbyTools.CreateLobby: creating lobby"); return "Failed."; }
		}

		private const float LeaveConfirmSeconds = 3f;
		private static float _leaveAt = -1f;

		internal static void RequestLeave()
		{
			try
			{
				if(AmongUsClient.Instance == null) return;
				if(LobbyBehaviour.Instance == null && ShipStatus.Instance == null)
				{
					_leaveAt = -1f;
					MalumMenu.notifications.Send("Leave", "You are not in a lobby.");
					return;
				}
				float now = Time.unscaledTime;
				if(_leaveAt > 0f && now <= _leaveAt)
				{
					_leaveAt = -1f;
					MalumMenu.notifications.Send("Leave", "Leaving lobby.");
					try { AmongUsClient.Instance.ExitGame(DisconnectReasons.ExitGame); }
					catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "LobbyTools.RequestLeave: exiting game"); }
					return;
				}
				_leaveAt = now + LeaveConfirmSeconds;
				MalumMenu.notifications.Send("Leave Lobby", "Press again to confirm.");
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "LobbyTools.RequestLeave: requesting leave"); }
		}

		// ---------------------------------------------------------------- fake map
		internal static class FakeMap
		{
			internal static bool Active { get; private set; }
			internal static bool Loading { get; private set; }
			internal static int FakeMapId { get; set; }
			private static ShipStatus _ship;

			internal static void Enable(int mapId)
			{
				try
				{
					if(Active || Loading || !Utils.isHost) return;
					if(AmongUsClient.Instance == null || AmongUsClient.Instance.ShipPrefabs == null) return;
					if(mapId < 0 || mapId >= AmongUsClient.Instance.ShipPrefabs.Count) return;
					FakeMapId = mapId;
					AmongUsClient.Instance.StartCoroutine(CoEnable(mapId).WrapToIl2Cpp());
				}
				catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "LobbyTools.FakeMap.Enable: enabling fake map"); }
			}

			internal static void Disable()
			{
				try
				{
					if(Loading || !Active || !Utils.isHost) return;
					if(_ship != null)
					{
						try
						{
							_ship.Despawn();
							Object.Destroy(_ship.gameObject);
						}
						catch { }
						_ship = null;
						ShipStatus.Instance = null;
					}
					try
					{
						if(PlayerControl.LocalPlayer != null && PlayerControl.LocalPlayer.NetTransform != null)
							PlayerControl.LocalPlayer.NetTransform.SnapTo(Vector2.zero);
					}
					catch { }
					Active = false;
					Loading = false;
				}
				catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "LobbyTools.FakeMap.Disable: disabling fake map"); }
			}

			internal static string DisableAndRestoreLobby()
			{
				Disable();
				return LobbyTools.CreateLobby();
			}

			private static IEnumerator CoEnable(int mapId)
			{
				Loading = true;
				if(AmongUsClient.Instance == null || AmongUsClient.Instance.ShipPrefabs == null
					|| mapId < 0 || mapId >= AmongUsClient.Instance.ShipPrefabs.Count)
				{
					Loading = false;
					yield break;
				}

				LobbyBehaviour lobby = LobbyBehaviour.Instance;
				if(lobby != null)
				{
					try
					{
						lobby.Despawn();
						Object.Destroy(lobby.gameObject);
						LobbyBehaviour.Instance = null;
					}
					catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "LobbyTools.FakeMap.CoEnable: despawning lobby"); }
					yield return null;
				}

				ShipStatus ship = null;
				var asyncHandle = AmongUsClient.Instance.ShipPrefabs[mapId].InstantiateAsync(null, false);
				yield return asyncHandle;
				try { ship = asyncHandle.Result.GetComponent<ShipStatus>(); }
				catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "LobbyTools.FakeMap.CoEnable: instantiating map"); }

				if(ship == null)
				{
					Loading = false;
					yield break;
				}

				_ship = ship;
				DisableInteractions(_ship);
				ShipStatus.Instance = _ship;
				try { AmongUsClient.Instance.Spawn(_ship, -2, SpawnFlags.None); }
				catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "LobbyTools.FakeMap.CoEnable: spawning map"); }

				foreach(PlayerControl pc in PlayerControl.AllPlayerControls)
				{
					if(pc == null) continue;
					try { ShipStatus.Instance.SpawnPlayer(pc, 5, false); }
					catch { }
				}

				Active = true;
				Loading = false;
			}

			private static void DisableInteractions(ShipStatus ship)
			{
				try
				{
					if(ship == null) return;
					if(ship.EmergencyButton != null)
					{
						try { ship.BreakEmergencyButton(); }
						catch { }
						((Behaviour)ship.EmergencyButton).enabled = false;
						((Component)ship.EmergencyButton).gameObject.SetActive(false);
					}
					AirshipStatus airship = ship.TryCast<AirshipStatus>();
					if(airship != null)
					{
						if(airship.GapPlatform != null)
						{
							((Behaviour)airship.GapPlatform).enabled = false;
							((Component)airship.GapPlatform).gameObject.SetActive(false);
						}
						foreach(MovingPlatformBehaviour mpb in ((Component)ship).GetComponentsInChildren<MovingPlatformBehaviour>(true))
						{
							if(mpb == null) continue;
							((Behaviour)mpb).enabled = false;
							((Component)mpb).gameObject.SetActive(false);
						}
					}
					FungleShipStatus fungle = ship.TryCast<FungleShipStatus>();
					if(fungle == null) return;
					foreach(ZiplineConsole zc in ((Component)ship).GetComponentsInChildren<ZiplineConsole>(true))
					{
						if(zc == null) continue;
						((Behaviour)zc).enabled = false;
						((Component)zc).gameObject.SetActive(false);
					}
					foreach(Mushroom m in ((Component)ship).GetComponentsInChildren<Mushroom>(true))
					{
						if(m == null) continue;
						((Behaviour)m).enabled = false;
						((Component)m).gameObject.SetActive(false);
					}
				}
				catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "LobbyTools.FakeMap.DisableInteractions: disabling interactions"); }
			}
		}

		// ------------------------------------------------------------ auto-return
		private static class AutoReturn
		{
			private const float DelaySeconds = 3f;
			private const float RetrySeconds = 0.4f;
			private const int MaxAttempts = 40;

			private static int _tracked;
			private static int _exhausted;
			private static int _attempt;
			private static float _nextAttemptAt;
			private static bool _pending;
			private static float _nextScanAt;

			internal static void Tick()
			{
				try
				{
					if(!CheatToggles.autoReturnLobby)
					{
						Reset();
						return;
					}
					if(LobbyBehaviour.Instance != null)
					{
						Reset();
						return;
					}
					if(Time.unscaledTime < _nextScanAt) return;
					_nextScanAt = Time.unscaledTime + 0.4f;

					EndGameManager manager = Object.FindObjectOfType<EndGameManager>();
					if(manager != null)
					{
						int id = manager.GetInstanceID();
						if(_tracked != id)
						{
							_tracked = id;
							_exhausted = 0;
							_attempt = 0;
							_nextAttemptAt = Time.unscaledTime + DelaySeconds;
							_pending = true;
						}
					}
					else if(_tracked == 0) return;

					if(!_pending || _exhausted == _tracked || Time.unscaledTime < _nextAttemptAt) return;

					bool acted = false;
					if(manager != null)
					{
						acted = TryInvokeAction(manager);
						acted = TryClickButtons(manager.GetComponentsInChildren<PassiveButton>(true)) || acted;
					}
					acted = TryClickButtons(Object.FindObjectsOfType<PassiveButton>()) || acted;

					if(LobbyBehaviour.Instance != null)
					{
						Reset();
						return;
					}
					_attempt++;
					if(_attempt >= MaxAttempts)
					{
						_pending = false;
						_exhausted = _tracked;
						return;
					}
					_nextAttemptAt = Time.unscaledTime + RetrySeconds;
				}
				catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "LobbyTools.AutoReturn.Tick: returning to lobby"); }
			}

			private static void Reset()
			{
				_tracked = 0;
				_exhausted = 0;
				_attempt = 0;
				_nextAttemptAt = 0f;
				_pending = false;
				_nextScanAt = 0f;
			}

			private static bool TryInvokeAction(EndGameManager manager)
			{
				string[] names = { "Continue", "NextGame", "PlayAgain" };
				foreach(string n in names)
				{
					MethodInfo m = null;
					for(Type t = manager.GetType(); t != null; t = t.BaseType)
					{
						m = t.GetMethod(n, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
						if(m != null) break;
					}
					if(m == null) continue;
					try
					{
						m.Invoke(manager, null);
						return true;
					}
					catch { }
				}
				return false;
			}

			private static bool TryClickButtons(PassiveButton[] buttons)
			{
				if(buttons == null) return false;
				foreach(PassiveButton b in buttons)
				{
					if(b == null) continue;
					Component c = b;
					if(!c.gameObject.activeInHierarchy || !b.isActiveAndEnabled) continue;
					if(!IsReturnButton(b.name, c.GetComponentsInChildren<TMP_Text>(true))) continue;
					try
					{
						((UnityEvent)b.OnClick).Invoke();
						return true;
					}
					catch { }
				}
				return false;
			}

			private static bool IsReturnButton(string objectName, TMP_Text[] texts)
			{
				string n = (objectName ?? "").ToLowerInvariant();
				if(ContainsAny(n, "exit", "quit", "menu", "back", "leave")) return false;
				if(ContainsAny(n, "continue", "nextgame", "playagain", "returntolobby", "tolobby", "lobby", "again")) return true;
				if(texts == null) return false;
				foreach(TMP_Text t in texts)
				{
					if(t == null) continue;
					string s = StripTags(t.text).ToLowerInvariant();
					if(ContainsAny(s, "exit", "quit", "menu", "back", "leave")) return false;
					if(ContainsAny(s, "continue", "next game", "play again", "return to lobby", "lobby", "again")) return true;
				}
				return false;
			}

			private static bool ContainsAny(string input, params string[] tokens)
			{
				if(string.IsNullOrEmpty(input)) return false;
				foreach(string tok in tokens)
					if(!string.IsNullOrWhiteSpace(tok) && input.Contains(tok)) return true;
				return false;
			}

			private static string StripTags(string s)
			{
				if(string.IsNullOrEmpty(s)) return "";
				StringBuilder sb = new StringBuilder(s.Length);
				bool tag = false;
				foreach(char ch in s)
				{
					if(ch == '<') tag = true;
					else if(ch == '>') tag = false;
					else if(!tag) sb.Append(ch);
				}
				return sb.ToString();
			}
		}

		// ---------------------------------------------------------------- history
		internal sealed class LobbyRow
		{
			internal int Id;
			internal string Code = "?";
			internal string Region = "?";
			internal string Map = "?";
			internal string Host = "?";
			internal int Players;
			internal bool Public;
			internal string When = "";
		}

		internal static class LobbyHistory
		{
			private const int Max = 25;
			private static readonly List<LobbyRow> Rows = new List<LobbyRow>();
			private static int _cur;
			private static float _next;
			private static bool _loaded;

			private static string Txt
			{
				get
				{
					string dir = Path.Combine(BepInEx.Paths.GameRootPath, "HyperMenu");
					return Path.Combine(dir, "LobbyHistory.txt");
				}
			}

			internal static IReadOnlyList<LobbyRow> Entries
			{
				get
				{
					Load();
					return Rows;
				}
			}

			internal static void Clear()
			{
				Rows.Clear();
				_cur = 0;
				Save();
			}

			internal static void Tick()
			{
				try
				{
					if(Time.unscaledTime < _next) return;
					_next = Time.unscaledTime + 2f;
					if(AmongUsClient.Instance == null) return;
					InnerNetClient net = AmongUsClient.Instance;
					if(net.GameId == 0 || (LobbyBehaviour.Instance == null && ShipStatus.Instance == null))
					{
						_cur = 0;
						return;
					}
					Load();
					if(net.GameId != _cur)
					{
						_cur = net.GameId;
						LobbyRow row = new LobbyRow
						{
							Id = _cur,
							Code = GameCode.IntToGameName(_cur),
							When = DateTime.Now.ToString("dd.MM HH:mm"),
						};
						Fill(row, net);
						Rows.Insert(0, row);
						while(Rows.Count > Max) Rows.RemoveAt(Rows.Count - 1);
						Save();
						return;
					}
					if(Rows.Count > 0 && Rows[0].Id == _cur)
						Fill(Rows[0], net);
				}
				catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "LobbyTools.LobbyHistory.Tick: tracking lobby"); }
			}

			private static void Fill(LobbyRow row, InnerNetClient net)
			{
				try
				{
					if(net.allClients != null && net.allClients.Count > row.Players)
						row.Players = net.allClients.Count;
					row.Public = AmongUsClient.Instance.IsGamePublic;
				}
				catch { }
				try
				{
					if(ServerManager.Instance != null && ServerManager.Instance.CurrentRegion != null)
						row.Region = ServerManager.Instance.CurrentRegion.Name;
				}
				catch { }
				try
				{
					if(ShipStatus.Instance != null)
						row.Map = ShipStatus.Instance.name.Replace("Ship", "").Replace("Status", "").Trim();
				}
				catch { }
				try
				{
					foreach(PlayerControl pc in PlayerControl.AllPlayerControls)
					{
						if(pc == null || pc.Data == null || pc.OwnerId != net.HostId) continue;
						string nm = ChatTools.MuteList.Strip(pc.Data.PlayerName);
						if(nm.Length > 0) row.Host = nm;
						break;
					}
				}
				catch { }
			}

			internal static string Rejoin(LobbyRow row)
			{
				try
				{
					if(row == null || row.Id == 0) return "Nothing to rejoin.";
					if(AmongUsClient.Instance == null) return "Not connected.";
					AmongUsClient.Instance.GameId = row.Id;
					var e = AmongUsClient.Instance.CoJoinOnlineGameFromCode(row.Id);
					if(e != null) AmongUsClient.Instance.StartCoroutine(e);
					return "Joining " + row.Code + ".";
				}
				catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "LobbyTools.LobbyHistory.Rejoin: rejoining lobby"); return "Failed."; }
			}

			private static void Load()
			{
				if(_loaded) return;
				_loaded = true;
				try
				{
					if(!File.Exists(Txt)) return;
					foreach(string line in File.ReadAllLines(Txt))
					{
						string[] p = line.Split('|');
						if(p.Length < 7) continue;
						int.TryParse(p[0], out int id);
						int.TryParse(p[5], out int players);
						Rows.Add(new LobbyRow
						{
							Id = id,
							Code = p[1],
							When = p[2],
							Host = p[3],
							Map = p[4],
							Players = players,
							Region = p[6],
							Public = p.Length > 7 && p[7] == "1",
						});
						if(Rows.Count >= Max) break;
					}
				}
				catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "LobbyTools.LobbyHistory.Load: loading history"); }
			}

			private static void Save()
			{
				try
				{
					string dir = Path.GetDirectoryName(Txt);
					if(!Directory.Exists(dir)) Directory.CreateDirectory(dir);
					StringBuilder sb = new StringBuilder();
					for(int i = 0; i < Rows.Count; i++)
					{
						LobbyRow r = Rows[i];
						sb.Append(r.Id).Append('|').Append(r.Code).Append('|').Append(r.When).Append('|')
							.Append(r.Host).Append('|').Append(r.Map).Append('|').Append(r.Players).Append('|')
							.Append(r.Region).Append('|').Append(r.Public ? '1' : '0').Append('\n');
					}
					File.WriteAllText(Txt, sb.ToString());
				}
				catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "LobbyTools.LobbyHistory.Save: saving history"); }
			}
		}

		internal static void Tick()
		{
			LobbyHistory.Tick();
			AutoReturn.Tick();
		}
	}
}
