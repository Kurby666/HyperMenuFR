using System;
using InnerNet;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MalumMenu.Cheats
{
	// Ported from othermenu/Patches/AutoHostPatches.cs (HyperAutoHost + HyperAutoHostService).
	// Host-only lobby auto-start state machine: warmup, load wait, player minimum, countdown,
	// fast start, force start (lobby lifetime / N minutes), backoff after failed starts,
	// and an optional instant start. Driven from RoutineManager.Update.
	internal static class AutoHost
	{
		internal const int HandlingId = 20068;

		private enum AutoHostState
		{
			Disabled, Idle, Warmup, WaitingPlayers, WaitingLoad, Countdown, Starting, InGame, Returning, Backoff,
		}

		private const float TickIntervalSeconds = 0.2f;
		private const float StartRequestGraceSeconds = 7f;
		private const float LobbyLifetimeSeconds = 10f * 60f;
		private const float LastMinuteStartSeconds = 60f;
		private const float NotificationCooldownSeconds = 0.75f;

		private static AutoHostState state = AutoHostState.Disabled;
		private static string lastReason = "disabled";
		private static float nextTickAt;
		private static float countdownStartedAt = -1f;
		private static float activeCountdownDelay = -1f;
		private static float backoffUntil = -1f;
		private static float lastStartIssuedAt = -1f;
		private static float lobbyOpenedAt = -1f;
		private static float loadWaitStartedAt = -1f;
		private static float lastNotificationAt = -1f;
		private static int lobbyGameId = -1;
		private static int lastCountdownNotice = -1;

		internal static string StateName
		{
			get
			{
				string name;
				switch(state)
				{
					case AutoHostState.Disabled: name = "disabled"; break;
					case AutoHostState.Idle: name = "idle"; break;
					case AutoHostState.Warmup: name = "warmup"; break;
					case AutoHostState.WaitingPlayers: name = "waiting for players"; break;
					case AutoHostState.WaitingLoad: name = "waiting for load"; break;
					case AutoHostState.Countdown: name = "countdown"; break;
					case AutoHostState.Starting: name = "starting"; break;
					case AutoHostState.InGame: name = "match running"; break;
					case AutoHostState.Returning: name = "returning to lobby"; break;
					default: name = "cooldown after attempt"; break;
				}
				return string.IsNullOrWhiteSpace(lastReason) ? name : $"{name} ({lastReason})";
			}
		}

		internal static bool ShouldReturnAfterMatch => IsEnabled && CheatToggles.autoHostReturn;

		internal static void Tick()
		{
			try
			{
				float now = Time.unscaledTime;
				if(now < nextTickAt) return;
				nextTickAt = now + TickIntervalSeconds;

				if(!IsEnabled)
				{
					ResetLobbyFlow(clearBackoff: true);
					SetState(AutoHostState.Disabled, "disabled");
					return;
				}

				InnerNetClient client = TryGetClient();
				if(client == null)
				{
					ResetLobbyFlow(clearBackoff: false);
					SetState(AutoHostState.Idle, "client unavailable");
					return;
				}

				if(!client.AmHost)
				{
					ResetLobbyFlow(clearBackoff: false);
					SetState(AutoHostState.Idle, "waiting for host context");
					return;
				}

				if(IsEndGameScreen())
				{
					ResetLobbyFlow(clearBackoff: false);
					SetState(ShouldReturnAfterMatch ? AutoHostState.Returning : AutoHostState.InGame,
						ShouldReturnAfterMatch ? "returning to lobby" : "match finished");
					return;
				}

				if(IsInMatch())
				{
					ResetLobbyFlow(clearBackoff: true);
					SetState(AutoHostState.InGame, "match running");
					return;
				}

				if(LobbyBehaviour.Instance == null)
				{
					ResetLobbyFlow(clearBackoff: false);
					lobbyOpenedAt = -1f;
					lobbyGameId = -1;
					SetState(AutoHostState.Idle, "not in a lobby");
					return;
				}

				TrackLobby(client, now);
				TickHostedLobby(client, now);
			}
			catch(Exception ex)
			{
				ErrorReporter.Report(ex, HandlingId, "AutoHost.Tick: auto-host state machine");
			}
		}

		internal static void ResetTransientState()
		{
			nextTickAt = 0f;
			ResetLobbyFlow(clearBackoff: true);
			SetState(IsEnabled ? AutoHostState.Idle : AutoHostState.Disabled, IsEnabled ? "reset" : "disabled");
		}

		private static void TickHostedLobby(InnerNetClient client, float now)
		{
			int connectedPlayers = CountLobbyPlayers(client, out int readyPlayers, out string loadingName);
			bool forceStart = ShouldForceStart(connectedPlayers, out string forceReason);
			float warmupRemaining = WarmupRemaining;

			if(!forceStart && warmupRemaining > 0.05f)
			{
				countdownStartedAt = -1f;
				activeCountdownDelay = -1f;
				lastStartIssuedAt = -1f;
				lastCountdownNotice = -1;
				SetState(AutoHostState.Warmup, $"lobby warmup {Mathf.CeilToInt(warmupRemaining)}s");
				return;
			}

			bool waitingForLoad = CheatToggles.autoHostWaitLoad && connectedPlayers > readyPlayers;

			if(waitingForLoad && !forceStart && !CanBypassLoadWait(now, readyPlayers, connectedPlayers, loadingName))
			{
				countdownStartedAt = -1f;
				activeCountdownDelay = -1f;
				lastStartIssuedAt = -1f;
				lastCountdownNotice = -1;
				SetState(AutoHostState.WaitingLoad, $"waiting for load {readyPlayers}/{connectedPlayers}: {loadingName}");
				return;
			}
			if(!waitingForLoad) loadWaitStartedAt = -1f;

			if(lastStartIssuedAt > 0f)
			{
				if(now - lastStartIssuedAt < StartRequestGraceSeconds)
				{
					SetState(AutoHostState.Starting, "start sent");
					return;
				}

				lastStartIssuedAt = -1f;
				EnterBackoff("start not confirmed");
				return;
			}

			if(backoffUntil > now)
			{
				SetState(AutoHostState.Backoff, "cooldown after attempt");
				return;
			}

			int requiredPlayers = RequiredPlayers;
			bool enoughPlayers = CheatToggles.autoHostWaitLoad ? readyPlayers >= requiredPlayers : connectedPlayers >= requiredPlayers;
			bool continueBelowMin = !CheatToggles.autoHostCancelBelowMin && countdownStartedAt >= 0f && connectedPlayers >= 2;
			if(!forceStart && !enoughPlayers && !continueBelowMin)
			{
				if(countdownStartedAt >= 0f)
					Notify("Auto-host", "Countdown canceled: below minimum players.");

				countdownStartedAt = -1f;
				activeCountdownDelay = -1f;
				lastCountdownNotice = -1;
				SetState(AutoHostState.WaitingPlayers, $"players {connectedPlayers}/{requiredPlayers}");
				return;
			}

			float delay = EffectiveStartDelay(connectedPlayers);
			if(!forceStart && countdownStartedAt < 0f)
			{
				countdownStartedAt = now;
				activeCountdownDelay = delay;
				lastCountdownNotice = -1;
				SetState(AutoHostState.Countdown, IsFastStartActive(connectedPlayers) ? "fast start" : "minimum players reached");
				Notify("Auto-host", $"Start in {Mathf.CeilToInt(delay)}s");
			}

			if(!forceStart && now - countdownStartedAt < delay)
			{
				AnnounceCountdown(delay - (now - countdownStartedAt));
				SetState(AutoHostState.Countdown, "countdown");
				return;
			}

			GameStartManager manager = TryGetGameStartManager();
			if(manager == null)
			{
				EnterBackoff("start button not found");
				return;
			}

			if(!TryConfiguredStart(manager))
			{
				EnterBackoff(forceStart ? "force start rejected" : "start rejected");
				return;
			}

			countdownStartedAt = -1f;
			activeCountdownDelay = -1f;
			backoffUntil = -1f;
			lastStartIssuedAt = now;
			lastCountdownNotice = -1;
			SetState(AutoHostState.Starting, forceStart ? forceReason : "starting match");
			Notify("Auto-host", forceStart ? forceReason : "Minimum reached, starting match.");
		}

		private static void TrackLobby(InnerNetClient client, float now)
		{
			int gameId;
			try
			{
				gameId = client.GameId;
			}
			catch
			{
				gameId = 0;
			}

			if(lobbyOpenedAt >= 0f && lobbyGameId == gameId) return;

			lobbyOpenedAt = now;
			lobbyGameId = gameId;
			ResetLobbyFlow(clearBackoff: true);
			SetState(AutoHostState.WaitingPlayers, "new lobby");
		}

		private static void AnnounceCountdown(float remaining)
		{
			int whole = Mathf.CeilToInt(Mathf.Max(0f, remaining));
			if(whole == lastCountdownNotice) return;

			if(whole == 60 || whole == 30 || whole == 15 || whole == 10 || whole == 5 || whole == 3 || whole == 2 || whole == 1)
			{
				lastCountdownNotice = whole;
				Notify("Auto-host", $"Start in {whole}s");
			}
		}

		private static bool TryConfiguredStart(GameStartManager manager)
		{
			if(manager == null || AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost || LobbyBehaviour.Instance == null)
				return false;

			try
			{
				manager.MinPlayers = 1;
				StartControls.UnlockStartButton(manager);
				if(CheatToggles.autoHostInstant) return StartControls.TryInstantStart(manager);

				manager.BeginGame();
				return true;
			}
			catch
			{
				return false;
			}
		}

		private static void EnterBackoff(string reason)
		{
			countdownStartedAt = -1f;
			activeCountdownDelay = -1f;
			lastStartIssuedAt = -1f;
			loadWaitStartedAt = -1f;
			lastCountdownNotice = -1;
			backoffUntil = Time.unscaledTime + BackoffSeconds;
			SetState(AutoHostState.Backoff, reason);
			Notify("Auto-host: paused", reason);
		}

		private static void ResetLobbyFlow(bool clearBackoff)
		{
			countdownStartedAt = -1f;
			lastStartIssuedAt = -1f;
			lastCountdownNotice = -1;
			if(clearBackoff) backoffUntil = -1f;
		}

		private static void SetState(AutoHostState nextState, string reason)
		{
			if(!string.IsNullOrWhiteSpace(reason)) lastReason = reason.Trim();
			state = nextState;
		}

		private static int CountLobbyPlayers(InnerNetClient client, out int readyPlayers, out string loadingName)
		{
			readyPlayers = 0;
			loadingName = "player";
			if(client == null || client.allClients == null) return 0;

			int connected = 0;
			try
			{
				var cursor = client.allClients.GetEnumerator();
				while(cursor.MoveNext())
				{
					ClientData data = cursor.Current;
					if(data == null || data.Id < 0) continue;
					if(IsDisconnected(data)) continue;

					connected++;
					if(IsReady(data)) readyPlayers++;
					else loadingName = CleanName(data.PlayerName);
				}
			}
			catch
			{
				return CountReadyPlayerControls(out readyPlayers);
			}

			return connected;
		}

		private static int CountReadyPlayerControls(out int readyPlayers)
		{
			readyPlayers = 0;
			try
			{
				if(PlayerControl.AllPlayerControls == null) return 0;

				int count = 0;
				var cursor = PlayerControl.AllPlayerControls.GetEnumerator();
				while(cursor.MoveNext())
				{
					PlayerControl player = cursor.Current;
					if(player == null || player.Data == null || player.Data.Disconnected || player.PlayerId >= 100) continue;

					count++;
					readyPlayers++;
				}

				return count;
			}
			catch
			{
				return 0;
			}
		}

		private static bool IsReady(ClientData data)
		{
			try
			{
				PlayerControl character = data.Character;
				return character != null && character.Data != null && !character.Data.Disconnected && character.PlayerId < 100;
			}
			catch
			{
				return false;
			}
		}

		private static bool IsDisconnected(ClientData data)
		{
			try
			{
				return data.Character != null && data.Character.Data != null && data.Character.Data.Disconnected;
			}
			catch
			{
				return false;
			}
		}

		private static GameStartManager TryGetGameStartManager()
		{
			if(DestroyableSingleton<GameStartManager>.InstanceExists) return DestroyableSingleton<GameStartManager>.Instance;

			try
			{
				return Object.FindObjectOfType<GameStartManager>();
			}
			catch
			{
				return null;
			}
		}

		private static InnerNetClient TryGetClient()
		{
			return AmongUsClient.Instance == null ? null : (InnerNetClient)AmongUsClient.Instance;
		}

		private static bool CanBypassLoadWait(float now, int readyPlayers, int connectedPlayers, string loadingName)
		{
			if(readyPlayers < RequiredPlayers)
			{
				loadWaitStartedAt = -1f;
				return false;
			}

			int grace = Mathf.Clamp(CheatToggles.autoHostLoadGrace, 0, 90);
			if(grace <= 0)
			{
				loadWaitStartedAt = -1f;
				return false;
			}

			if(loadWaitStartedAt < 0f) loadWaitStartedAt = now;

			if(now - loadWaitStartedAt < grace)
			{
				SetState(AutoHostState.WaitingLoad, $"waiting for load {readyPlayers}/{connectedPlayers}: {loadingName}");
				return false;
			}

			SetState(AutoHostState.Countdown, "load stalled, starting with ready players");
			return true;
		}

		private static bool ShouldForceStart(int connectedPlayers, out string reason)
		{
			int minPlayers = ForceMinPlayers;
			if(CheatToggles.autoHostForceLastMinute && connectedPlayers >= minPlayers && LobbyLifeRemaining >= 0f && LobbyLifeRemaining <= LastMinuteStartSeconds)
			{
				reason = "force start: lobby closing soon";
				return true;
			}

			int forceAfterMinutes = Mathf.Clamp(CheatToggles.autoHostForceAfterMinutes, 0, 10);
			if(forceAfterMinutes > 0 && connectedPlayers >= minPlayers && lobbyOpenedAt > 0f && Time.unscaledTime - lobbyOpenedAt >= forceAfterMinutes * 60f)
			{
				reason = $"force start: waited {forceAfterMinutes} min";
				return true;
			}

			reason = string.Empty;
			return false;
		}

		private static bool IsFastStartActive(int connectedPlayers)
		{
			int threshold = Mathf.Clamp(CheatToggles.autoHostFastStartPlayers, 0, 15);
			return threshold > 0 && connectedPlayers >= threshold;
		}

		private static float EffectiveStartDelay(int connectedPlayers)
		{
			float delay = StartDelaySeconds;
			if(IsFastStartActive(connectedPlayers))
				delay = Mathf.Min(delay, Mathf.Clamp(CheatToggles.autoHostFastStartDelay, 0, 60));

			return delay;
		}

		private static bool IsInMatch()
		{
			return ShipStatus.Instance != null && LobbyBehaviour.Instance == null && !IsEndGameScreen();
		}

		private static bool IsEndGameScreen()
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

		private static void Notify(string title, string detail)
		{
			if(!CheatToggles.autoHostNotify) return;

			float now = Time.unscaledTime;
			if(lastNotificationAt > 0f && now - lastNotificationAt < NotificationCooldownSeconds) return;

			lastNotificationAt = now;
			MalumMenu.notifications.Send(title, detail, 3.2f);
		}

		private static string CleanName(string value)
		{
			if(string.IsNullOrWhiteSpace(value)) return "player";

			string clean = value.Replace("\r", " ").Replace("\n", " ").Trim();
			return clean.Length <= 18 ? clean : clean.Substring(0, 17) + "...";
		}

		private static bool IsEnabled => CheatToggles.autoHost;
		private static bool ForceLastMinuteEnabled => CheatToggles.autoHostForceLastMinute;
		private static int RequiredPlayers => Mathf.Clamp(CheatToggles.autoHostMinPlayers, 1, 15);
		private static int ForceMinPlayers => Mathf.Clamp(CheatToggles.autoHostForceMinPlayers, 1, 15);
		private static float StartDelaySeconds => Mathf.Clamp(CheatToggles.autoHostStartDelay, 0f, 180f);
		private static float BackoffSeconds => Mathf.Clamp(CheatToggles.autoHostBackoff, 2f, 60f);
		private static float LobbyLifeRemaining => lobbyOpenedAt < 0f ? -1f : Mathf.Clamp(LobbyLifetimeSeconds - (Time.unscaledTime - lobbyOpenedAt), 0f, LobbyLifetimeSeconds);
		private static float WarmupRemaining => lobbyOpenedAt < 0f ? 0f : Mathf.Clamp(CheatToggles.autoHostWarmup - (Time.unscaledTime - lobbyOpenedAt), 0f, 120f);
	}
}
