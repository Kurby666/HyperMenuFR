using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using HarmonyLib;
using Il2CppInterop.Runtime;
using InnerNet;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MalumMenu.Cheats
{
	// ==========================================================================================
	// BodyModePatches.cs — force your own body type (client-side cosmetic gag).
	// othermenu maps Disabled/Horse/Seeker/Long/LongHorse onto the PlayerBodyTypes enum and
	// patches the getter, the physics setter, and the LongBoi bootstrap. src has no equivalent,
	// so the whole thing is ported.
	//
	// othermenu's HarmonyControl.Continue / SkipOriginal are its own enum; src uses plain
	// bool returns (true = run original, false = skip), which is the same thing.
	// ==========================================================================================
	internal static class BodyMode
	{
		internal const int HandlingId = 20080;

		private const int Horse = 1;
		private const int Seeker = 2;
		private const int Long = 3;
		private const int LongHorse = 4;

		private static readonly string[] Names = { "Disabled", "Horse", "Seeker", "Long", "Long horse" };

		internal static string ModeName() => Names[Mathf.Clamp(CheatToggles.bodyModeIdx, 0, Names.Length - 1)];

		internal static void Cycle()
		{
			CheatToggles.bodyModeIdx = (CheatToggles.bodyModeIdx + 1) % Names.Length;
		}

		internal static bool TryGetForcedBody(out PlayerBodyTypes bodyType)
		{
			switch (CheatToggles.bodyModeIdx)
			{
				case 1:
					bodyType = (PlayerBodyTypes)Horse;
					return true;
				case 2:
					bodyType = (PlayerBodyTypes)Seeker;
					return true;
				case 3:
					bodyType = (PlayerBodyTypes)Long;
					return true;
				case 4:
					bodyType = (PlayerBodyTypes)LongHorse;
					return true;
				default:
					bodyType = default;
					return false;
			}
		}

		internal static void ForcePhysicsBody(ref PlayerBodyTypes bodyType)
		{
			if (TryGetForcedBody(out PlayerBodyTypes replacement))
			{
				bodyType = replacement;
			}
		}

		internal static bool TryOverrideBodyGetter(ref PlayerBodyTypes result)
		{
			if (!TryGetForcedBody(out PlayerBodyTypes forcedBody))
			{
				return true;
			}

			result = forcedBody;
			return false;
		}
	}

	internal static class LongBodyBootstrap
	{
		internal static void Rewire(LongBoiPlayerBody body)
		{
			CosmeticsLayer layer = body.cosmeticLayer;
			layer.OnSetBodyAsGhost += (Action)body.SetPoolableGhost;
			layer.OnColorChange += (Action<int>)body.SetHeightFromColor;
			layer.OnCosmeticSet += (Action<string, int, CosmeticsLayer.CosmeticKind>)body.OnCosmeticSet;
		}

		internal static void Start(LongBoiPlayerBody body)
		{
			body.ShouldLongAround = true;

			if (body.hideCosmeticsQC)
			{
				body.cosmeticLayer.SetHatVisorVisible(false);
			}

			body.SetupNeckGrowth(false, true);
			if (body.isExiledPlayer && (ShipStatus.Instance == null || (int)ShipStatus.Instance.Type != 3))
			{
				body.cosmeticLayer.AdjustCosmeticRotations(-17.75f);
			}

			if (!body.isPoolablePlayer)
			{
				body.cosmeticLayer.ValidateCosmetics();
			}
		}
	}

	[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.BodyType), MethodType.Getter)]
	internal static class BodyMode_PlayerBodyGetterPatch
	{
		public static bool Prefix(PlayerControl __instance, ref PlayerBodyTypes __result)
		{
			try
			{
				if (__instance == null)
				{
					__result = (PlayerBodyTypes)0;
					return false;
				}

				return BodyMode.TryOverrideBodyGetter(ref __result);
			}
			catch (Exception ex) { ErrorReporter.Report(ex, BodyMode.HandlingId, "BodyMode_PlayerBodyGetterPatch.Prefix"); return true; }
		}
	}

	[HarmonyPatch(typeof(PlayerPhysics), nameof(PlayerPhysics.SetBodyType))]
	internal static class BodyMode_PhysicsBodyPatch
	{
		public static void Prefix(ref PlayerBodyTypes bodyType)
		{
			try
			{
				BodyMode.ForcePhysicsBody(ref bodyType);
			}
			catch (Exception ex) { ErrorReporter.Report(ex, BodyMode.HandlingId, "BodyMode_PhysicsBodyPatch.Prefix"); }
		}
	}

	[HarmonyPatch(typeof(LongBoiPlayerBody), nameof(LongBoiPlayerBody.Awake))]
	internal static class BodyMode_LongBodyAwakePatch
	{
		public static bool Prefix(LongBoiPlayerBody __instance)
		{
			try
			{
				LongBodyBootstrap.Rewire(__instance);
				return false;
			}
			catch (Exception ex) { ErrorReporter.Report(ex, BodyMode.HandlingId, "BodyMode_LongBodyAwakePatch.Prefix"); return true; }
		}
	}

	[HarmonyPatch(typeof(LongBoiPlayerBody), nameof(LongBoiPlayerBody.Start))]
	internal static class BodyMode_LongBodyStartPatch
	{
		public static bool Prefix(LongBoiPlayerBody __instance)
		{
			try
			{
				LongBodyBootstrap.Start(__instance);
				return false;
			}
			catch (Exception ex) { ErrorReporter.Report(ex, BodyMode.HandlingId, "BodyMode_LongBodyStartPatch.Prefix"); return true; }
		}
	}

	// ==========================================================================================
	// ChatModerationPatches.cs (HyperBanWords half) — local word censor.
	//
	// The other half of that file is HyperChatLog, which appends every chat line to a per-lobby
	// text file. src already covers that need better: the Config tab's "Log chat messages to
	// console" toggle routes through ConsoleUI.Log, which keeps an in-memory ring AND a file, and
	// the new Event Log window reads the in-memory side. So only the censor is ported.
	// ==========================================================================================
	internal static class BanWords
	{
		internal const int HandlingId = 20081;

		private static readonly object Lock = new object();
		private static readonly List<string> Words = new List<string>();
		private static float nextReload;
		private const string Header = "# BanWords.txt — one word per line, # = comment";

		private static string Path0 => Path.Combine(Path.Combine(BepInEx.Paths.GameRootPath, "HyperMenu"), "BanWords.txt");

		internal static void Init()
		{
			try
			{
				Directory.CreateDirectory(Path.GetDirectoryName(Path0));
				if (!File.Exists(Path0))
				{
					File.WriteAllText(Path0, Header + "\n", Encoding.UTF8);
				}
				else
				{
					var lines = new List<string>(File.ReadAllLines(Path0, Encoding.UTF8));
					if (lines.Count > 0 && lines[0].StartsWith("# BanWords.txt", StringComparison.Ordinal) && lines[0] != Header)
					{
						lines[0] = Header;
						File.WriteAllText(Path0, string.Join("\n", lines) + "\n", Encoding.UTF8);
					}
				}
			}
			catch { }

			Reload();
		}

		internal static void TickReload()
		{
			float now = Time.realtimeSinceStartup;
			if (now < nextReload)
			{
				return;
			}

			nextReload = now + 30f;
			Reload();
		}

		internal static string Censor(string text)
		{
			if (string.IsNullOrEmpty(text))
			{
				return text;
			}

			lock (Lock)
			{
				if (Words.Count == 0)
				{
					return text;
				}

				string lower = text.ToLowerInvariant();
				foreach (string w in Words)
				{
					int from = 0;
					int idx;
					while ((idx = lower.IndexOf(w, from, StringComparison.Ordinal)) >= 0)
					{
						text = text.Remove(idx, w.Length).Insert(idx, new string('*', w.Length));
						lower = text.ToLowerInvariant();
						from = idx + w.Length;
					}
				}
			}

			return text;
		}

		private static void Reload()
		{
			try
			{
				if (!File.Exists(Path0))
				{
					return;
				}

				var fresh = new List<string>();
				foreach (string line in File.ReadAllLines(Path0, Encoding.UTF8))
				{
					string w = line.Trim().ToLowerInvariant();
					if (!string.IsNullOrEmpty(w) && !w[0].Equals('#'))
					{
						fresh.Add(w);
					}
				}

				lock (Lock)
				{
					Words.Clear();
					Words.AddRange(fresh);
				}
			}
			catch { }
		}
	}

	[HarmonyPatch(typeof(ChatController), nameof(ChatController.AddChat))]
	[HarmonyPriority(Priority.First)]
	internal static class BanWords_CensorPatch
	{
		public static void Prefix(ref string chatText)
		{
			if (!CheatToggles.banWords)
			{
				return;
			}

			try
			{
				BanWords.TickReload();
				chatText = BanWords.Censor(chatText);
			}
			catch { }
		}
	}

	// ==========================================================================================
	// Extras/HyperXmas.cs + Patches/XmasChatPatches.cs — the "/xmas" colour-cycling troll.
	// ==========================================================================================
	internal static class Xmas
	{
		internal const int HandlingId = 20082;

		private const float Step = 0.1f;
		private static readonly Dictionary<byte, float> Timers = new Dictionary<byte, float>();
		private static readonly List<byte> Ids = new List<byte>();
		private static readonly List<byte> Drop = new List<byte>();
		private static readonly HashSet<int> Used = new HashSet<int>();
		private static readonly List<int> Free = new List<int>();

		internal static bool Toggle(byte id)
		{
			if (Timers.Remove(id))
			{
				return false;
			}

			Timers[id] = 0f;
			return true;
		}

		internal static bool IsCommand(string text)
		{
			if (string.IsNullOrEmpty(text))
			{
				return false;
			}

			switch (text.Trim().ToLowerInvariant())
			{
				case "/xmas":
				case "/tree":
				case "!xmas":
					return true;
				default:
					return false;
			}
		}

		internal static bool AnyActive => Timers.Count > 0;

		internal static int ActiveCount => Timers.Count;

		private static PlayerControl ById(byte id)
		{
			try
			{
				var e = PlayerControl.AllPlayerControls.GetEnumerator();
				while (e.MoveNext())
				{
					PlayerControl p = e.Current;
					if (p != null && p.PlayerId == id)
					{
						return p;
					}
				}
			}
			catch { }

			return null;
		}

		internal static void Tick()
		{
			if (Timers.Count == 0 || AmongUsClient.Instance == null)
			{
				return;
			}

			if (LobbyBehaviour.Instance == null && ShipStatus.Instance == null)
			{
				Timers.Clear();
				return;
			}

			float now = Time.realtimeSinceStartup;
			bool host = AmongUsClient.Instance.AmHost;

			Ids.Clear();
			foreach (byte id in Timers.Keys)
			{
				Ids.Add(id);
			}

			Drop.Clear();

			for (int i = 0; i < Ids.Count; i++)
			{
				byte id = Ids[i];
				if (now - Timers[id] < Step)
				{
					continue;
				}

				Timers[id] = now;

				PlayerControl p = ById(id);
				if (p == null || p.Data == null || p.Data.Disconnected)
				{
					Drop.Add(id);
					continue;
				}

				try
				{
					if (host)
					{
						p.RpcSetColor((byte)UnityEngine.Random.Range(0, MaxColor() + 1));
					}
					else if (p.AmOwner)
					{
						int free = FreeColor(p);
						if (free >= 0)
						{
							p.CmdCheckColor((byte)free);
						}
					}
				}
				catch { }
			}

			for (int i = 0; i < Drop.Count; i++)
			{
				Timers.Remove(Drop[i]);
			}
		}

		private static int FreeColor(PlayerControl self)
		{
			Used.Clear();
			try
			{
				var e = PlayerControl.AllPlayerControls.GetEnumerator();
				while (e.MoveNext())
				{
					PlayerControl o = e.Current;
					if (o != null && o.Data != null && o != self && o.Data.DefaultOutfit != null)
					{
						Used.Add(o.Data.DefaultOutfit.ColorId);
					}
				}
			}
			catch { }

			Free.Clear();
			int max = MaxColor();
			for (int i = 0; i <= max; i++)
			{
				if (!Used.Contains(i))
				{
					Free.Add(i);
				}
			}

			return Free.Count == 0 ? -1 : Free[UnityEngine.Random.Range(0, Free.Count)];
		}

		private static int MaxColor()
		{
			try
			{
				if (Palette.PlayerColors != null)
				{
					return Mathf.Max(0, Palette.PlayerColors.Length - 1);
				}
			}
			catch { }

			return 18;
		}
	}

	[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.RpcSendChat))]
	internal static class Xmas_SendPatch
	{
		public static bool Prefix(PlayerControl __instance, [HarmonyArgument(0)] string chatText)
		{
			try
			{
				if (__instance != PlayerControl.LocalPlayer || !Xmas.IsCommand(chatText))
				{
					return true;
				}

				bool on = Xmas.Toggle(__instance.PlayerId);
				MalumMenu.notifications.Send("Xmas", on ? "Colors cycling!" : "Stopped.", 2f);
				return true;
			}
			catch
			{
				return true;
			}
		}
	}

	[HarmonyPatch(typeof(ChatController), nameof(ChatController.AddChat))]
	internal static class Xmas_ChatCommandPatch
	{
		public static bool Prefix([HarmonyArgument(0)] PlayerControl sourcePlayer, [HarmonyArgument(1)] string chatText)
		{
			try
			{
				if (sourcePlayer == null || !Xmas.IsCommand(chatText))
				{
					return true;
				}

				if (sourcePlayer == PlayerControl.LocalPlayer)
				{
					return true;
				}

				if (CheatToggles.xmasHostCommand)
				{
					Xmas.Toggle(sourcePlayer.PlayerId);
				}

				return true;
			}
			catch
			{
				return true;
			}
		}
	}

	// ==========================================================================================
	// ChatPatches.cs — the pieces of othermenu's "better chat" bundle that HyperMenu did not
	// already have. Ported: bubble cache sizing, cooldown bypass, chat timestamps, sender info in
	// bubbles, and the dark chat theme.
	//
	// NOT ported from the same file: ChatHistoryNavigator (arrow-key history, needs othermenu's
	// input ownership model), ChatBubbleCopyHandler and ChatBubbleAnimations (both need
	// othermenu's theme + window shell). "Unlimited length" is also not here: src already has it
	// via Patches/TextBoxTMPPatches.cs behind the Chat tab's "Allow Longer Messages" toggle.
	//
	// ChatThemeStyler in othermenu mixes two colours with HyperStyle.Current (othermenu's own
	// theme palette). src has no such object, so a fixed dark palette is used instead and the
	// bubble-cache text colour is taken from the existing chat colour.
	// ==========================================================================================
	internal static class ChatExtras
	{
		internal const int HandlingId = 20083;

		private const float VanillaSafeChatCooldownSeconds = 3.15f;

		private static readonly Color BubbleBack = new Color(0.020f, 0.022f, 0.032f, 0.84f);
		private static readonly Color InputBack = new Color(0.018f, 0.020f, 0.030f, 1f);
		private static readonly Color BubbleText = new Color(0.90f, 0.92f, 0.96f, 1f);
		private static readonly Color InputText = new Color(0.94f, 0.95f, 0.98f, 1f);

		private static int lastChatId;
		private static string lastThemeId = string.Empty;
		private static bool lastEnabled;
		private static float nextInputRefreshAt;

		// ---- bubble cache -------------------------------------------------------------------

		internal static void TuneBubbleCache(ChatController chat)
		{
			if (chat == null || chat.chatBubblePool == null)
			{
				return;
			}

			int size = Mathf.Clamp(CheatToggles.chatHistorySize, 4, 200);
			if (chat.chatBubblePool.poolSize == size)
			{
				return;
			}

			chat.chatBubblePool.poolSize = size;
			chat.chatBubblePool.ReclaimOldest();
		}

		// ---- runtime ------------------------------------------------------------------------

		internal static void Tick(ChatController chat)
		{
			if (chat == null)
			{
				return;
			}

			if (CheatToggles.noChatCooldown)
			{
				// othermenu parks the timer above the vanilla 3.15s threshold every frame rather than
				// zeroing it, because a real zero can trip the server's own anti-spam check.
				chat.timeSinceLastMessage = Mathf.Max(chat.timeSinceLastMessage, VanillaSafeChatCooldownSeconds + 0.1f);
			}

			RefreshInputs(chat);
		}

		internal static void Awake(ChatController chat)
		{
			TuneBubbleCache(chat);
			RefreshInputs(chat, true);
		}

		// ---- dark theme ---------------------------------------------------------------------

		internal static void RefreshInputs(ChatController chat, bool force = false)
		{
			if (chat == null || !CheatToggles.darkChatTheme)
			{
				lastEnabled = false;
				return;
			}

			int chatId = chat.GetInstanceID();
			string themeId = MalumMenu.menuHtmlColor.Value;
			if (!force && lastEnabled && lastChatId == chatId && string.Equals(lastThemeId, themeId, StringComparison.Ordinal) && Time.unscaledTime < nextInputRefreshAt)
			{
				return;
			}

			lastEnabled = true;
			lastChatId = chatId;
			lastThemeId = themeId;
			nextInputRefreshAt = Time.unscaledTime + 0.75f;

			ApplyFreeChatField(chat, InputBack, InputText);
			ApplyQuickChatField(chat, InputBack, InputText);
		}

		private static void ApplyFreeChatField(ChatController chat, Color background, Color textColor)
		{
			try
			{
				if (chat == null || chat.freeChatField == null)
				{
					return;
				}

				AbstractChatInputField field = (AbstractChatInputField)chat.freeChatField;
				if (field != null && field.background != null)
				{
					field.background.color = background;
				}

				TextBoxTMP textArea = chat.freeChatField.textArea;
				if (textArea != null && textArea.outputText != null)
				{
					((Graphic)textArea.outputText).color = textColor;
				}
			}
			catch { }
		}

		private static void ApplyQuickChatField(ChatController chat, Color background, Color textColor)
		{
			try
			{
				if (chat == null || chat.quickChatField == null)
				{
					return;
				}

				AbstractChatInputField field = (AbstractChatInputField)chat.quickChatField;
				if (field != null && field.background != null)
				{
					field.background.color = background;
				}

				if (chat.quickChatField.text != null)
				{
					((Graphic)chat.quickChatField.text).color = textColor;
				}
			}
			catch { }
		}

		internal static void ApplyBubble(ChatBubble bubble)
		{
			if (bubble == null || !CheatToggles.darkChatTheme)
			{
				return;
			}

			try
			{
				if (bubble.Background != null)
				{
					bubble.Background.color = BubbleBack;
				}
			}
			catch { }

			try
			{
				if (bubble.TextArea != null)
				{
					bubble.TextArea.color = BubbleText;
				}
			}
			catch { }

			try
			{
				if (bubble.NameText != null)
				{
					bubble.NameText.color = BubbleText;
				}
			}
			catch { }
		}

		// ---- sender info --------------------------------------------------------------------

		internal static void ApplySenderInfo(ChatBubble bubble)
		{
			if (bubble == null || bubble.NameText == null || bubble.playerInfo == null)
			{
				return;
			}

			try
			{
				if (bubble.NameText.text != null && bubble.NameText.text.Contains(InfoMarker))
				{
					return;
				}

				string plainName = ColorTools.StripTags(bubble.NameText.text);
				if (plainName != null && plainName.Length > 24)
				{
					return;
				}

				PlayerControl player = bubble.playerInfo.Object;

				string levelStr = JoinLevels.Display(player);

				string platformStr = "?";
				bool isHost = false;
				if (AmongUsClient.Instance != null)
				{
					InnerNetClient client = AmongUsClient.Instance as InnerNetClient;
					if (client != null && player != null)
					{
						ClientData clientData = client.GetClientFromCharacter(player);
						if (clientData != null)
						{
							if (clientData.PlatformData != null)
							{
								platformStr = Utils.PlatformTypeToString(clientData.PlatformData.Platform);
								string rawName = (clientData.PlatformData.PlatformName ?? string.Empty).Trim();
								if (!string.IsNullOrEmpty(rawName) && !rawName.Equals("TESTNAME", StringComparison.OrdinalIgnoreCase))
								{
									rawName = TrimRawPlatformName(rawName);
									int cap = Mathf.Clamp(22 - (plainName != null ? plainName.Length : 0), 6, 16);
									if (rawName.Length > cap)
									{
										rawName = rawName.Substring(0, cap).TrimEnd() + "…";
									}

									if (!string.IsNullOrEmpty(rawName))
									{
										platformStr += " · " + rawName;
									}
								}
							}

							ClientData host = client.GetHost();
							isHost = host != null && clientData == host;
						}
					}
				}

				string hostTag = isHost ? "<color=#F5C542>Host</color> · " : string.Empty;
				bubble.NameText.text += $"  <size=65%>{hostTag}{levelStr} · {platformStr}</size>{InfoMarker}";
			}
			catch { }
		}

		internal const string InfoMarker = "<size=65%>";

		// othermenu treats the ';' and ',' in a raw platform name as name/id separators and only
		// shows the part in front of them.
		private static string TrimRawPlatformName(string raw)
		{
			string[] separators = { ";", "," };
			int earliest = -1;
			for (int i = 0; i < separators.Length; i++)
			{
				int idx = raw.IndexOf(separators[i], StringComparison.OrdinalIgnoreCase);
				if (idx > 0 && (earliest < 0 || idx < earliest))
				{
					earliest = idx;
				}
			}

			return earliest < 0 ? raw : raw.Substring(0, earliest).Trim();
		}
	}

	[HarmonyPatch(typeof(ChatController), nameof(ChatController.Awake))]
	internal static class ChatExtras_AwakePatch
	{
		public static void Postfix(ChatController __instance)
		{
			try
			{
				ChatExtras.Awake(__instance);
			}
			catch { }
		}
	}

	[HarmonyPatch(typeof(ChatController), nameof(ChatController.Update))]
	internal static class ChatExtras_UpdatePatch
	{
		public static void Postfix(ChatController __instance)
		{
			try
			{
				ChatExtras.Tick(__instance);
			}
			catch { }
		}
	}

	[HarmonyPatch(typeof(ChatBubble), nameof(ChatBubble.SetText))]
	internal static class ChatExtras_DarkBubblePatch
	{
		public static void Postfix(ChatBubble __instance)
		{
			try
			{
				ChatExtras.ApplyBubble(__instance);
			}
			catch { }
		}
	}

	[HarmonyPatch(typeof(ChatBubble), nameof(ChatBubble.SetName))]
	internal static class ChatExtras_SenderInfoPatch
	{
		public static void Postfix(ChatBubble __instance)
		{
			if (!CheatToggles.chatSenderInfo)
			{
				return;
			}

			try
			{
				ChatExtras.ApplySenderInfo(__instance);
			}
			catch { }
		}
	}

	[HarmonyPatch(typeof(ChatBubble), nameof(ChatBubble.SetText))]
	[HarmonyPriority(Priority.First)]
	internal static class ChatExtras_TimestampPatch
	{
		public static void Prefix([HarmonyArgument(0)] ref string chatText)
		{
			if (!CheatToggles.chatTimestamps)
			{
				return;
			}

			if (string.IsNullOrEmpty(chatText))
			{
				return;
			}

			chatText += "\n<align=\"right\"><size=55%><color=#8A8A8A>" + DateTime.Now.ToString("HH:mm:ss") + "</color></size></align>";
		}
	}

	// ==========================================================================================
	// SpoofPatches.cs (friend-code half) — actually change the in-game friend code.
	//
	// src already covers the platform half of SpoofPatches (features/Spoofer.cs SpoofPlatform, a
	// 0-10 slider) and the device half (Patches/OtherPatches.cs SystemInfo_deviceUniqueIdentifier
	// already mints a fresh random hex id). What src does NOT have is the friend-code flow:
	// MalumMenu.guestFriendCode is declared but its Config.Bind is commented out in
	// MalumMenu.cs:180, and MalumSpoof.SpoofFriendCode() has no callers, so that whole path was
	// dead. The 12-platform list differs from src's slider by exactly one entry (Platforms 112),
	// which is not worth a new control.
	// ==========================================================================================
	internal static class FriendCodeTools
	{
		internal const int HandlingId = 20084;

		private static readonly string[] A = { "cosmic", "stellar", "nebula", "astral", "lunar", "solar", "vortex", "plasma", "photon", "quasar", "ember", "frost", "raven" };
		private static readonly string[] B = { "flux", "wave", "beam", "core", "node", "link", "zone", "pulse", "spark", "glow", "byte", "rift", "vibe", "haze" };

		// String state, matching the pattern used by Cheats/LobbyBrowser.cs SearchHost and
		// Cheats/LobbyPresets.cs PresetName — src has no string-valued config entries.
		internal static string Value = string.Empty;
		private static string applied;
		private static bool prevOn;
		private static float nextApply;
		internal static bool fallback;

		internal static bool On => CheatToggles.fcSpoofEnabled && !string.IsNullOrEmpty(Value.Trim());

		internal static string Random() => A[UnityEngine.Random.Range(0, A.Length)] + B[UnityEngine.Random.Range(0, B.Length)] + "#" + UnityEngine.Random.Range(1000, 9999);

		internal static void Randomize()
		{
			Value = Random();
			Apply();
		}

		internal static void Apply()
		{
			string value = Value.Trim();
			if (value.Length == 0)
			{
				return;
			}

			try
			{
				FriendsListManager mgr = FriendsListManager.Instance;
				if (mgr == null)
				{
					MalumMenu.notifications.Send("FC", "Not available now.", 2.5f);
					return;
				}

				EditAccountUsername edit = EOSManager.Instance != null ? EOSManager.Instance.editAccountUsername : null;

				var managed = new Action<Assets.InnerNet.ResponseState, Assets.InnerNet.Response<Assets.InnerNet.ResponseFriendCode>>((state, resp) =>
				{
					bool usedGame = false;
					try
					{
						if (edit != null)
						{
							edit._SaveUsername_b__8_0(state, resp);
							usedGame = true;
						}
					}
					catch { }

					bool ok = state == Assets.InnerNet.ResponseState.Success;
					if (ok && !usedGame)
					{
						try
						{
							if (EOSManager.Instance != null)
							{
								EOSManager.Instance.FriendCode = value;
							}
						}
						catch { }
					}

					MalumMenu.notifications.Send("FC", ok ? "Name changed." : "Server rejected the name.", 3.5f);
				});

				mgr.SetFriendCode(value, DelegateSupport.ConvertDelegate<Il2CppSystem.Action<Assets.InnerNet.ResponseState, Assets.InnerNet.Response<Assets.InnerNet.ResponseFriendCode>>>(managed));
				MalumMenu.notifications.Send("FC", "Sent to server…", 2f);
			}
			catch (Exception ex)
			{
				MalumMenu.notifications.Send("FC", ex.Message, 3.5f);
			}
		}

		// othermenu drives this from a MonoBehaviour Update. src uses RoutineManager.Update, which
		// also runs in the lobby, so the same effect is cheaper here.
		internal static void Tick()
		{
			float now = Time.realtimeSinceStartup;
			bool on = On;
			string val = Value.Trim();
			bool edge = on && (!prevOn || val != applied);
			prevOn = on;

			if (edge && now >= nextApply)
			{
				nextApply = now + 2f;
				applied = val;
				Apply();
			}
		}

		internal static string Fallback(string value)
		{
			string plain = ColorTools.StripTags(value).Trim();
			if (plain.Length == 0)
			{
				return string.Empty;
			}

			int hash = plain.LastIndexOf('#');
			if (hash <= 0)
			{
				return plain;
			}

			string suffix = plain.Substring(hash + 1);
			if (suffix.Length == 0)
			{
				return plain.Substring(0, hash);
			}

			for (int i = 0; i < suffix.Length; i++)
			{
				if (suffix[i] < '0' || suffix[i] > '9')
				{
					return plain.Substring(0, hash);
				}
			}

			return plain;
		}
	}

	[HarmonyPatch(typeof(FriendsListManager), nameof(FriendsListManager.SetFriendCode))]
	internal static class FriendCodeTools_RegisterPatch
	{
		public static bool Prefix(
			[HarmonyArgument(0)] ref string username,
			[HarmonyArgument(1)] ref Il2CppSystem.Action<Assets.InnerNet.ResponseState, Assets.InnerNet.Response<Assets.InnerNet.ResponseFriendCode>> resultCallback)
		{
			try
			{
				if (FriendCodeTools.fallback || !FriendCodeTools.On)
				{
					return true;
				}

				string value = FriendCodeTools.Value.Trim();
				if (value.Length == 0)
				{
					return true;
				}

				username = value;

				string retry = FriendCodeTools.Fallback(value);
				if (retry.Length == 0 || retry == value)
				{
					return true;
				}

				var original = resultCallback;
				resultCallback = DelegateSupport.ConvertDelegate<Il2CppSystem.Action<Assets.InnerNet.ResponseState, Assets.InnerNet.Response<Assets.InnerNet.ResponseFriendCode>>>(
					(System.Action<Assets.InnerNet.ResponseState, Assets.InnerNet.Response<Assets.InnerNet.ResponseFriendCode>>)((state, response) =>
					{
						if (state == Assets.InnerNet.ResponseState.Failed)
						{
							try
							{
								FriendsListManager mgr = FriendsListManager.Instance;
								if (mgr != null)
								{
									FriendCodeTools.fallback = true;
									mgr.SetFriendCode(retry, original);
									return;
								}
							}
							catch { }
							finally
							{
								FriendCodeTools.fallback = false;
							}
						}

						if (original != null)
						{
							original.Invoke(state, response);
						}
					}));

				return true;
			}
			catch
			{
				return true;
			}
		}
	}

	[HarmonyPatch(typeof(EditAccountUsername), nameof(EditAccountUsername.OnEnable))]
	internal static class FriendCodeTools_ScreenPatch
	{
		public static void Postfix(EditAccountUsername __instance)
		{
			if (!FriendCodeTools.On || __instance == null)
			{
				return;
			}

			string value = FriendCodeTools.Value.Trim();
			if (value.Length == 0)
			{
				return;
			}

			if (__instance.UsernameText != null)
			{
				__instance.UsernameText.text = value;
			}

			try
			{
				foreach (PassiveButton b in __instance.GetComponentsInChildren<PassiveButton>(true))
				{
					try
					{
						if (b != null)
						{
							b.SetButtonEnableState(true);
						}
					}
					catch { }
				}
			}
			catch { }
		}
	}

	[HarmonyPatch(typeof(EditAccountUsername), nameof(EditAccountUsername.RandomizeName))]
	internal static class FriendCodeTools_RandomPatch
	{
		public static void Postfix(EditAccountUsername __instance)
		{
			try
			{
				if (!FriendCodeTools.On || __instance == null || __instance.UsernameText == null)
				{
					return;
				}

				string value = FriendCodeTools.Value.Trim();
				if (value.Length > 0)
				{
					__instance.UsernameText.text = value;
				}
			}
			catch { }
		}
	}

	[HarmonyPatch(typeof(EditAccountUsername), nameof(EditAccountUsername.SaveUsername))]
	internal static class FriendCodeTools_ConfirmPatch
	{
		public static bool Prefix(EditAccountUsername __instance)
		{
			try
			{
				if (!FriendCodeTools.On || __instance == null)
				{
					return true;
				}

				string value = FriendCodeTools.Value.Trim();
				if (value.Length == 0)
				{
					return true;
				}

				FriendsListManager mgr = FriendsListManager.Instance;
				if (mgr == null)
				{
					return true;
				}

				if (__instance.UsernameText != null)
				{
					__instance.UsernameText.text = value;
				}

				mgr.SetFriendCode(
					value,
					DelegateSupport.ConvertDelegate<Il2CppSystem.Action<Assets.InnerNet.ResponseState, Assets.InnerNet.Response<Assets.InnerNet.ResponseFriendCode>>>(
						new System.Action<Assets.InnerNet.ResponseState, Assets.InnerNet.Response<Assets.InnerNet.ResponseFriendCode>>(__instance._SaveUsername_b__8_0)));
				return false;
			}
			catch
			{
				return true;
			}
		}
	}

	[HarmonyPatch(typeof(PassiveButton), nameof(PassiveButton.SetButtonEnableState))]
	internal static class FriendCodeTools_ButtonPatch
	{
		public static void Prefix(PassiveButton __instance, [HarmonyArgument(0)] ref bool enable)
		{
			if (!FriendCodeTools.On || __instance == null || enable)
			{
				return;
			}

			try
			{
				if (__instance.GetComponentInParent<EditAccountUsername>() != null)
				{
					enable = true;
				}
			}
			catch { }
		}
	}
}
