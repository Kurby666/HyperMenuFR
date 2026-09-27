using System;
using System.Collections.Generic;
using System.Text;
using AmongUs.GameOptions;
using AmongUs.InnerNet.GameDataMessages;
using AmongUs.QuickChat;
using HarmonyLib;
using Hazel;
using UnityEngine;

namespace MalumMenu.Cheats
{
	// Chat toolbox: sender + spam + flood, quick-chat chains, host slash-commands,
	// /c color commands, whispers, per-player mute. Ported from othermenu
	// Chat/HyperChatSender.cs, HyperQuickChatChain.cs, HyperCommands.cs,
	// HyperColorCmd.cs, HyperWhisper.cs, HyperMuteList.cs and
	// Patches/ChatMutePatch.cs. Adaptations: English-only strings (no RU/EN toggle
	// in src), local-only /help (no broadcast), role parsing from English names.
	internal static class ChatTools
	{
		private const int HandlingId = 20054;

		// ---------------------------------------------------------------- sender
		internal static class ChatSender
		{
			internal static string Message = "";
			internal static bool Spamming
			{
				get => CheatToggles.chatSpam;
				set => CheatToggles.chatSpam = value;
			}

			private static float _next;
			private static bool _floodSending;

			internal static float FloodCooldownLeft;
			internal static bool FloodReady => FloodCooldownLeft <= 0f;

			private static readonly string HugeMessage = " " + new string('\u2029', 118) + " ";
			private const float FloodCooldownBase = 18f;
			private const float FloodCooldownPenalty = 3f;

			internal static bool Send(string text)
			{
				try
				{
					PlayerControl me = PlayerControl.LocalPlayer;
					if(me == null || string.IsNullOrWhiteSpace(text)) return false;
					me.RpcSendChat(text);
					return true;
				}
				catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "ChatTools.ChatSender.Send: sending chat"); return false; }
			}

			internal static string SendNow() => Send(Message) ? "Sent." : "Failed to send.";

			internal static string Flood()
			{
				try
				{
					if(PlayerControl.LocalPlayer == null) return "Not in a game.";
					_floodSending = true;
					for(int i = 0; i < 6; i++) Send(HugeMessage);
					_floodSending = false;
					FloodCooldownLeft = FloodCooldownBase;
					return "Chat flooded.";
				}
				catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "ChatTools.ChatSender.Flood: flooding chat"); return "Failed."; }
			}

			internal static void NoteChatSent()
			{
				if(_floodSending || FloodCooldownLeft <= 0f) return;
				FloodCooldownLeft += FloodCooldownPenalty;
			}

			internal static void Tick()
			{
				try
				{
					if(FloodCooldownLeft > 0f)
						FloodCooldownLeft = Mathf.Max(0f, FloodCooldownLeft - Time.unscaledDeltaTime);
					if(!CheatToggles.chatSpam) return;
					if(PlayerControl.LocalPlayer == null || string.IsNullOrWhiteSpace(Message))
					{
						CheatToggles.chatSpam = false;
						return;
					}
					float now = Time.unscaledTime;
					if(now < _next) return;
					_next = now + Mathf.Max(1.5f, CheatToggles.chatSpamDelay);
					Send(Message);
				}
				catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "ChatTools.ChatSender.Tick: spamming chat"); }
			}
		}

		[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.RpcSendChat))]
		private static class FloodChatPatch
		{
			public static void Postfix(PlayerControl __instance)
			{
				try
				{
					if(__instance == PlayerControl.LocalPlayer)
						ChatSender.NoteChatSent();
				}
				catch { }
			}
		}

		// ------------------------------------------------------------ quick-chat
		internal static class QuickChat
		{
			internal static string ChainIds = "";

			private const byte CallSendQuickChat = 33;

			internal static string SendFromText(string ids)
			{
				try
				{
					if(string.IsNullOrWhiteSpace(ids)) return "Empty.";
					string[] parts = ids.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
					if(parts.Length == 0) return "Empty.";
					if(!int.TryParse(parts[0], out int root)) return "No root phrase.";
					int[] subs = new int[parts.Length - 1];
					for(int i = 1; i < parts.Length; i++)
						if(!int.TryParse(parts[i], out subs[i - 1])) return "Bad phrase id.";
					return Send(root, subs);
				}
				catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "ChatTools.QuickChat.SendFromText: parsing ids"); return "Failed."; }
			}

			internal static string Send(int root, int[] subs)
			{
				try
				{
					PlayerControl me = PlayerControl.LocalPlayer;
					if(me == null || AmongUsClient.Instance == null || !AmongUsClient.Instance.AmConnected)
						return "Not in game.";
					if(root <= 0 || root > ushort.MaxValue) return "No root phrase.";
					int count = subs != null ? subs.Length : 0;
					if(count > byte.MaxValue) return "Too many phrases.";
					for(int i = 0; i < count; i++)
						if(subs[i] <= 0 || subs[i] > ushort.MaxValue) return "Bad phrase id.";
					bool ok = PushChain(me, root, subs);
					return ok ? "Sent." : "Failed.";
				}
				catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "ChatTools.QuickChat.Send: sending chain"); return "Failed."; }
			}

			private static bool PushChain(PlayerControl me, int root, int[] subs)
			{
				MessageWriter w = null;
				try
				{
					w = AmongUsClient.Instance.StartRpcImmediately(me.NetId, CallSendQuickChat, SendOption.Reliable, -1);
					if(w == null) return false;
					int count = subs != null ? subs.Length : 0;
					w.Write((byte)(count > 0 ? 3 : 2));
					w.Write((ushort)root);
					w.Write((byte)count);
					for(int i = 0; i < count; i++)
					{
						w.Write((byte)2);
						w.Write((ushort)subs[i]);
					}
					AmongUsClient.Instance.FinishRpcImmediately(w);
					return true;
				}
				catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "ChatTools.QuickChat.PushChain: pushing chain"); return false; }
			}

			internal static string SendTemplateFor(PlayerControl target, string rootText)
			{
				try
				{
					if(target == null || target.Data == null) return "No target.";
					if(!int.TryParse((rootText ?? "").Trim(), out int root)) return "No root phrase.";
					PlayerControl me = PlayerControl.LocalPlayer;
					if(me == null || AmongUsClient.Instance == null || !AmongUsClient.Instance.AmConnected)
						return "Not in game.";
					QuickChatPhrase[] array = { QuickChatPhrase.NewPlayerId(target.PlayerId) };
					QuickChatPhraseBuilderResult result = new QuickChatPhraseBuilderResult((QuickChatPhraseType)3, (StringNames)root, 0, array);
					MessageWriter w = AmongUsClient.Instance.StartRpcImmediately(me.NetId, CallSendQuickChat, SendOption.Reliable, -1);
					if(w == null) return "Failed.";
					RpcSendQuickChatMessage msg = new RpcSendQuickChatMessage(me.NetId, result);
					msg.SerializeRpcValues(w);
					AmongUsClient.Instance.FinishRpcImmediately(w);
					return "Sent.";
				}
				catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "ChatTools.QuickChat.SendTemplateFor: sending template"); return "Failed."; }
			}
		}

		// ----------------------------------------------------------------- mute
		internal static class MuteList
		{
			private static readonly HashSet<string> Muted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			internal static bool IsMuted(string fc) => !string.IsNullOrWhiteSpace(fc) && Muted.Contains(fc.Trim());

			internal static bool IsMuted(PlayerControl p) => p != null && p.Data != null && IsMuted(p.Data.FriendCode);

			internal static string Toggle(PlayerControl p)
			{
				try
				{
					if(p == null || p.Data == null) return "No target.";
					string fc = p.Data.FriendCode.Trim();
					if(Muted.Contains(fc))
					{
						Muted.Remove(fc);
						return Strip(p.Data.PlayerName) + " unmuted.";
					}
					Muted.Add(fc);
					return Strip(p.Data.PlayerName) + " muted.";
				}
				catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "ChatTools.MuteList.Toggle: toggling mute"); return "Failed."; }
			}

			internal static string Strip(string s)
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
				return sb.ToString().Trim();
			}
		}

		[HarmonyPatch(typeof(ChatController), nameof(ChatController.AddChat))]
		private static class MutePatch
		{
			public static bool Prefix(PlayerControl sourcePlayer)
			{
				try
				{
					if(sourcePlayer == null || sourcePlayer.Data == null) return true;
					if(sourcePlayer == PlayerControl.LocalPlayer) return true;
					return !MuteList.IsMuted(sourcePlayer.Data.FriendCode);
				}
				catch { return true; }
			}
		}

		// -------------------------------------------------------------- whisper
		internal static class Whisper
		{
			private static byte _keep = 255;

			internal static void Reset() => _keep = 255;

			internal static bool TryHandle(ChatController chat)
			{
				try
				{
					if(chat == null || chat.freeChatField == null || chat.freeChatField.textArea == null)
						return false;
					string text = chat.freeChatField.textArea.text;
					if(string.IsNullOrWhiteSpace(text)) return false;

					string trimmed = text.TrimStart();
					string low = trimmed.ToLowerInvariant();

					if(low == "/unwkeep" || low.StartsWith("/unwkeep "))
					{
						_keep = 255;
						Local("Persistent whisper off.");
						Clear(chat);
						return true;
					}

					if(low.StartsWith("/wkeep"))
					{
						string[] kp = trimmed.Split(new[] { ' ' }, 2);
						if(kp.Length < 2 || string.IsNullOrWhiteSpace(kp[1]))
						{
							Local("Usage: /wkeep [name or ID]");
							Clear(chat);
							return true;
						}
						PlayerControl kt = Find(kp[1].Trim().ToLowerInvariant());
						if(kt == null || kt.Data == null || kt == PlayerControl.LocalPlayer)
						{
							Local("Player not found.");
							Clear(chat);
							return true;
						}
						_keep = kt.PlayerId;
						Local("Persistent whisper to " + MuteList.Strip(kt.Data.PlayerName) + ". Off: /unwkeep");
						Clear(chat);
						return true;
					}

					if(low.StartsWith("/w ") || low.StartsWith("/pm ") || low.StartsWith("/msg "))
					{
						string[] parts = trimmed.Split(new[] { ' ' }, 3);
						if(parts.Length < 3 || string.IsNullOrWhiteSpace(parts[2]))
						{
							Local("Usage: /w [name or ID] message");
							Clear(chat);
							return true;
						}
						PlayerControl target = Find(parts[1].Trim().ToLowerInvariant());
						if(target == null || target.Data == null || target == PlayerControl.LocalPlayer)
						{
							Local("Player not found.");
							Clear(chat);
							return true;
						}
						Send(target, MuteList.Strip(parts[2]));
						Clear(chat);
						return true;
					}

					if(_keep != 255 && !trimmed.StartsWith("/"))
					{
						PlayerControl kt = ById(_keep);
						if(kt == null || kt.Data == null || kt.Data.Disconnected)
						{
							_keep = 255;
							Local("Target left, persistent whisper off.");
							Clear(chat);
							return true;
						}
						Send(kt, MuteList.Strip(text));
						Clear(chat);
						return true;
					}

					return false;
				}
				catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "ChatTools.Whisper.TryHandle: handling whisper"); return false; }
			}

			private const byte CallSendChat = 13;

			private static void Send(PlayerControl target, string msg)
			{
				try
				{
					MessageWriter w = AmongUsClient.Instance.StartRpcImmediately(
						PlayerControl.LocalPlayer.NetId, CallSendChat, SendOption.Reliable, target.OwnerId);
					w.Write("whispers to you:\n" + msg);
					AmongUsClient.Instance.FinishRpcImmediately(w);
					Local("Whisper to " + MuteList.Strip(target.Data.PlayerName) + ":\n" + msg);
				}
				catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "ChatTools.Whisper.Send: sending whisper"); }
			}

			internal static void Prefill(string name)
			{
				try
				{
					if(HudManager.Instance == null || HudManager.Instance.Chat == null) return;
					ChatController chat = HudManager.Instance.Chat;
					chat.SetVisible(true);
					if(chat.freeChatField != null && chat.freeChatField.textArea != null)
						chat.freeChatField.textArea.SetText("/w " + name + " ", string.Empty);
				}
				catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "ChatTools.Whisper.Prefill: prefilling whisper"); }
			}

			private static PlayerControl ById(byte id)
			{
				foreach(PlayerControl p in PlayerControl.AllPlayerControls)
					if(p != null && p.PlayerId == id) return p;
				return null;
			}

			private static PlayerControl Find(string q)
			{
				try
				{
					if(PlayerControl.AllPlayerControls == null) return null;
					if(byte.TryParse(q, out byte id))
					{
						PlayerControl byId = ById(id);
						if(byId != null) return byId;
					}
					PlayerControl partial = null;
					foreach(PlayerControl p in PlayerControl.AllPlayerControls)
					{
						if(p == null || p.Data == null || p.Data.Disconnected || p == PlayerControl.LocalPlayer)
							continue;
						string pname = MuteList.Strip(p.Data.PlayerName).ToLowerInvariant().Trim();
						if(pname == q) return p;
						if(partial == null && pname.StartsWith(q)) partial = p;
					}
					return partial;
				}
				catch { return null; }
			}

			private static void Local(string msg)
			{
				try
				{
					if(HudManager.Instance != null && HudManager.Instance.Chat != null && PlayerControl.LocalPlayer != null)
						HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, msg);
				}
				catch { }
			}

			private static void Clear(ChatController chat)
			{
				try { chat.freeChatField.textArea.SetText(string.Empty, string.Empty); }
				catch { }
			}
		}

		[HarmonyPatch(typeof(LobbyBehaviour), nameof(LobbyBehaviour.Start))]
		private static class WhisperResetPatch
		{
			public static void Postfix() => Whisper.Reset();
		}

		// ---------------------------------------------------------- color cmds
		internal static class ColorCmd
		{
			internal static readonly string[] Names =
			{
				"Red", "Blue", "Green", "Pink", "Orange", "Yellow", "Black", "White", "Purple",
				"Brown", "Cyan", "Lime", "Maroon", "Rose", "Banana", "Gray", "Tan", "Coral",
			};

			internal static int ColorId(string a)
			{
				if(string.IsNullOrEmpty(a)) return -1;
				if(int.TryParse(a, out int n) && n >= 0 && n <= 17) return n;
				switch(a)
				{
					case "red": return 0;
					case "blue": return 1;
					case "green": return 2;
					case "pink": return 3;
					case "orange": return 4;
					case "yellow":
					case "yel": return 5;
					case "black": return 6;
					case "white": return 7;
					case "purple":
					case "purp": return 8;
					case "brown": return 9;
					case "cyan": return 10;
					case "lime": return 11;
					case "maroon":
					case "mar": return 12;
					case "rose": return 13;
					case "banana": return 14;
					case "gray":
					case "grey": return 15;
					case "tan": return 16;
					case "coral": return 17;
					default: return -1;
				}
			}
		}

		[HarmonyPatch(typeof(ChatController), nameof(ChatController.AddChat))]
		private static class ColorCmdPatch
		{
			public static void Postfix(PlayerControl sourcePlayer, string chatText)
			{
				try
				{
					if(sourcePlayer == null || string.IsNullOrEmpty(chatText)) return;
					string t = chatText.Trim();
					int sp = t.IndexOf(' ');
					string cmd = (sp < 0 ? t : t.Substring(0, sp)).ToLowerInvariant();

					if(cmd == "/help")
					{
						if(sourcePlayer != PlayerControl.LocalPlayer) return;
						ShowHelp();
						return;
					}

					if(!CheatToggles.colorCmd) return;
					if(cmd != "/c" && cmd != "/color") return;

					int id = ColorCmd.ColorId(sp < 0 ? "" : t.Substring(sp + 1).Trim().ToLowerInvariant());
					if(id < 0) return;

					bool host = AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost;
					if(host)
						sourcePlayer.RpcSetColor((byte)id);
					else if(sourcePlayer == PlayerControl.LocalPlayer)
						PlayerControl.LocalPlayer.CmdCheckColor((byte)id);

					if(CheatToggles.colorCmdNotify && MalumMenu.notifications != null)
					{
						string who = sourcePlayer.Data != null ? MuteList.Strip(sourcePlayer.Data.PlayerName) : "?";
						MalumMenu.notifications.Send("Color", who + " -> " + ColorCmd.Names[id]);
					}
				}
				catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "ChatTools.ColorCmdPatch.Postfix: handling color command"); }
			}

			private static void ShowHelp()
			{
				try
				{
					if(HudManager.Instance == null || HudManager.Instance.Chat == null || PlayerControl.LocalPlayer == null) return;
					HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer,
						"Host: /kick /ban /mute <name> /color <name> <color> /role <name> <role> /start /end /meeting /close /fix — Everyone: /w <name|ID> <text> /wkeep <name|ID> /unwkeep /c <color>");
				}
				catch { }
			}
		}

		// ------------------------------------------------------- host commands
		[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.RpcSendChat))]
		private static class CommandsPatch
		{
			public static bool Prefix(PlayerControl __instance, string chatText)
			{
				try
				{
					if(__instance != PlayerControl.LocalPlayer || string.IsNullOrEmpty(chatText)) return true;
					if(!CheatToggles.chatCmds) return true;

					string t = chatText.Trim();
					if(t.Length < 2 || t[0] != '/') return true;
					int sp = t.IndexOf(' ');
					string cmd = (sp < 0 ? t : t.Substring(0, sp)).ToLowerInvariant();
					string rest = sp < 0 ? "" : t.Substring(sp + 1).Trim();
					bool host = AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost;

					switch(cmd)
					{
						case "/kick":
							Kick(rest, false, host);
							return false;
						case "/ban":
							Kick(rest, true, host);
							return false;
						case "/mute":
							Mute(rest, host);
							return false;
						case "/color":
							if(rest.IndexOf(' ') < 0) return true;
							ColorOther(rest, host);
							return false;
						case "/role":
							Role(rest, host);
							return false;
						case "/start":
							if(RequireHost(host)) MalumCheats.ForceStartGameCheat();
							return false;
						case "/end":
							if(RequireHost(host)) GameManager.Instance.RpcEndGame(GameOverReason.CrewmatesByTask, false);
							return false;
						case "/meeting":
							if(RequireHost(host)) PlayerControl.LocalPlayer.CmdReportDeadBody(null);
							return false;
						case "/close":
							if(RequireHost(host)) MalumMenu.notifications.Send("Meeting", MeetingTools.CloseMeetingNoEject());
							return false;
						case "/fix":
							if(RequireHost(host))
							{
								Sabotage.FixAllSabotages();
								MalumMenu.notifications.Send("Sabotages", "All sabotages fixed.");
							}
							return false;
						default:
							return true;
					}
				}
				catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "ChatTools.CommandsPatch.Prefix: handling command"); return true; }
			}

			private static void Kick(string pname, bool ban, bool host)
			{
				if(!RequireHost(host)) return;
				PlayerControl p = Find(pname);
				if(p == null) { NotFound(pname); return; }
				try { AmongUsClient.Instance.KickPlayer(p.OwnerId, ban); }
				catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "ChatTools.CommandsPatch.Kick: kicking player"); }
				MalumMenu.notifications.Send(ban ? "Ban" : "Kick", MuteList.Strip(p.Data.PlayerName));
			}

			private static void Mute(string pname, bool host)
			{
				if(!RequireHost(host)) return;
				PlayerControl p = Find(pname);
				if(p == null || p.Data == null) { NotFound(pname); return; }
				MalumMenu.notifications.Send("Mute", MuteList.Toggle(p));
			}

			private static void ColorOther(string rest, bool host)
			{
				if(!RequireHost(host)) return;
				int sp = rest.LastIndexOf(' ');
				if(sp < 0) return;
				PlayerControl p = Find(rest.Substring(0, sp));
				int id = ColorCmd.ColorId(rest.Substring(sp + 1).Trim().ToLowerInvariant());
				if(p == null) { NotFound(rest.Substring(0, sp)); return; }
				if(id < 0) return;
				try { p.RpcSetColor((byte)id); }
				catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "ChatTools.CommandsPatch.ColorOther: setting color"); }
				MalumMenu.notifications.Send("Color", MuteList.Strip(p.Data.PlayerName));
			}

			private static readonly Dictionary<string, RoleTypes> RoleAliases = new Dictionary<string, RoleTypes>(StringComparer.OrdinalIgnoreCase)
			{
				{ "imp", RoleTypes.Impostor }, { "impostor", RoleTypes.Impostor },
				{ "crew", RoleTypes.Crewmate }, { "crewmate", RoleTypes.Crewmate },
				{ "sci", RoleTypes.Scientist }, { "scientist", RoleTypes.Scientist },
				{ "eng", RoleTypes.Engineer }, { "engineer", RoleTypes.Engineer },
				{ "ga", RoleTypes.GuardianAngel }, { "guardian", RoleTypes.GuardianAngel }, { "guardianangel", RoleTypes.GuardianAngel },
				{ "shifter", RoleTypes.Shapeshifter }, { "shapeshifter", RoleTypes.Shapeshifter }, { "shift", RoleTypes.Shapeshifter },
				{ "phantom", RoleTypes.Phantom },
				{ "tracker", RoleTypes.Tracker }, { "track", RoleTypes.Tracker },
				{ "noisemaker", RoleTypes.Noisemaker }, { "noise", RoleTypes.Noisemaker },
				{ "det", RoleTypes.Detective }, { "detective", RoleTypes.Detective },
			};

			private static void Role(string rest, bool host)
			{
				if(!RequireHost(host)) return;
				int sp = rest.LastIndexOf(' ');
				if(sp < 0) return;
				PlayerControl p = Find(rest.Substring(0, sp));
				if(p == null || p.Data == null) { NotFound(rest.Substring(0, sp)); return; }
				string arg = rest.Substring(sp + 1).Trim();
				RoleTypes role;
				if(!RoleAliases.TryGetValue(arg, out role))
				{
					try { role = (RoleTypes)Enum.Parse(typeof(RoleTypes), arg, true); }
					catch { return; }
				}
				try { p.RpcSetRole(role, true); }
				catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "ChatTools.CommandsPatch.Role: setting role"); return; }
				MalumMenu.notifications.Send("Role", MuteList.Strip(p.Data.PlayerName) + " -> " + role);
			}

			private static PlayerControl Find(string pname)
			{
				if(string.IsNullOrWhiteSpace(pname)) return null;
				pname = pname.Trim().ToLowerInvariant();
				PlayerControl partial = null;
				foreach(PlayerControl pc in PlayerControl.AllPlayerControls)
				{
					if(pc == null || pc.Data == null) continue;
					string n = MuteList.Strip(pc.Data.PlayerName).ToLowerInvariant();
					if(n == pname) return pc;
					if(partial == null && (n.StartsWith(pname) || n.Contains(pname))) partial = pc;
				}
				return partial;
			}

			private static bool RequireHost(bool host)
			{
				if(host) return true;
				MalumMenu.notifications.Send("Command", "Host only.");
				return false;
			}

			private static void NotFound(string pname) => MalumMenu.notifications.Send("Command", "Player not found: " + pname.Trim());
		}
	}
}
