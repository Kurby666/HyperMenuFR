using System;
using UnityEngine;

namespace MalumMenu;

public class ChatTab : ITab
{
    private const int HandlingId = 60004;
    public string name => "Chat";

    private TextField _chatColorField;
    private TextField _msgField;
    private TextField _qcField;
    private bool _initialized = false;

    public void Draw()
    {
        try
        {
            if (!_initialized)
            {
                Initialize();
                _initialized = true;
            }

            GUILayout.BeginVertical(GUILayout.Width(MenuUI.windowWidth * 0.425f));

            DrawGeneral();

            GUILayout.Space(15);

            DrawTextbox();

            GUILayout.Space(15);

            DrawColorSettings();

            GUILayout.Space(15);

            DrawSender();

            GUILayout.Space(15);

            DrawAdvanced();

            GUILayout.EndVertical();
        }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "ChatTab.Draw: draw chat settings"); }
    }

    public void Initialize()
    {
        _chatColorField = new TextField(MalumMenu.menuChatColor.Value);
        _msgField = new TextField(Cheats.ChatTools.ChatSender.Message);
        _qcField = new TextField(Cheats.ChatTools.QuickChat.ChainIds);
    }

    private void DrawGeneral()
    {
        CheatToggles.enableChat = GUILayout.Toggle(CheatToggles.enableChat, " Enable Chat");

        CheatToggles.bypassUrlBlock = GUILayout.Toggle(CheatToggles.bypassUrlBlock, " Bypass URL Block");

        CheatToggles.lowerRateLimits = GUILayout.Toggle(CheatToggles.lowerRateLimits, " Lower Rate Limits");

        CheatToggles.showGhostsChat = GUILayout.Toggle(CheatToggles.showGhostsChat, " Show Ghost Messages");
        features.Chat.OnChat.ShowMessagesByGhosts = CheatToggles.showGhostsChat;

        CheatToggles.alwaysVisibleChat = GUILayout.Toggle(CheatToggles.alwaysVisibleChat, " Always Visible Chat");
        features.Chat.AlwaysVisibleChat.Enabled = CheatToggles.alwaysVisibleChat;

        GUILayout.Space(5);
        GUILayout.Label("Overhead Chat", GUIStylePreset.TabSubtitle);
        CheatToggles.overheadChat = GUILayout.Toggle(CheatToggles.overheadChat, " Chat Bubbles Above Players");
        if (CheatToggles.overheadChat)
        {
            GUILayout.Label($"  Bubble Time: {CheatToggles.overheadChatTime:F0}s");
            CheatToggles.overheadChatTime = GUILayout.HorizontalSlider(CheatToggles.overheadChatTime, 2f, 15f);
            string[] whereNames = { "Lobby + Match", "Match Only", "Lobby Only" };
            if (GUILayout.Button($"  Show In: {whereNames[Mathf.Clamp(CheatToggles.overheadChatWhere, 0, 2)]}"))
                CheatToggles.overheadChatWhere = (CheatToggles.overheadChatWhere + 1) % 3;
        }
    }

    private void DrawTextbox()
    {
        GUILayout.Label("Textbox", GUIStylePreset.TabSubtitle);

        CheatToggles.unlockCharacters = GUILayout.Toggle(CheatToggles.unlockCharacters, " Unlock Extra Characters");

        CheatToggles.longerMessages = GUILayout.Toggle(CheatToggles.longerMessages, " Allow Longer Messages");

        CheatToggles.unlockClipboard = GUILayout.Toggle(CheatToggles.unlockClipboard, " Unlock Clipboard");
    }

    private void DrawColorSettings()
    {
        GUILayout.Label("Chat Color", GUIStylePreset.TabSubtitle);
        CheatToggles.colorAsPlayer = GUILayout.Toggle(CheatToggles.colorAsPlayer, " Chat messages colored as the player who sent them");
        CheatToggles.changeChatColor = GUILayout.Toggle(CheatToggles.changeChatColor, " Enable Custom Chat Color");
        GUILayout.Space(5);
        GUILayout.BeginHorizontal();
        GUILayout.Label("Chat HTML Color:", GUILayout.Width(150));
        _chatColorField.Draw(150);
        if (GUILayout.Button("Save", GUILayout.Width(100)))
        {
            MalumMenu.menuChatColor.Value = _chatColorField.Content;
        }
        GUILayout.EndHorizontal();
    }

    private void DrawSender()
    {
        GUILayout.Label("Chat Sender", GUIStylePreset.TabSubtitle);
        GUILayout.Label("Message:");
        Cheats.ChatTools.ChatSender.Message = _msgField.Content;
        _msgField.Draw(200);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("SEND"))
        {
            Cheats.ChatTools.ChatSender.Message = _msgField.Content;
            MalumMenu.notifications.Send("Chat", Cheats.ChatTools.ChatSender.SendNow());
        }
        if (GUILayout.Button("FLOOD"))
        {
            MalumMenu.notifications.Send("Chat", Cheats.ChatTools.ChatSender.Flood());
        }
        GUILayout.EndHorizontal();
        if (!Cheats.ChatTools.ChatSender.FloodReady)
        {
            GUILayout.Label($"Flood cooldown: {Cheats.ChatTools.ChatSender.FloodCooldownLeft:F0}s");
        }
        CheatToggles.chatSpam = GUILayout.Toggle(CheatToggles.chatSpam, " Spam Message");
        if (CheatToggles.chatSpam)
        {
            Cheats.ChatTools.ChatSender.Message = _msgField.Content;
            GUILayout.Label($" Spam Delay: {CheatToggles.chatSpamDelay:F1}s");
            CheatToggles.chatSpamDelay = GUILayout.HorizontalSlider(CheatToggles.chatSpamDelay, 1.5f, 10f);
        }

        GUILayout.Space(5);
        GUILayout.Label("Quick Chat Chain (ids: root sub sub...):");
        _qcField.Draw(200);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("SEND CHAIN"))
        {
            Cheats.ChatTools.QuickChat.ChainIds = _qcField.Content;
            MalumMenu.notifications.Send("Quick Chat", Cheats.ChatTools.QuickChat.SendFromText(_qcField.Content));
        }
        if (GUILayout.Button("WITH PLAYER"))
        {
            PlayerControl target = PlayersSection.selectedPlayer;
            MalumMenu.notifications.Send("Quick Chat", Cheats.ChatTools.QuickChat.SendTemplateFor(target, _qcField.Content));
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(5);
        CheatToggles.chatCmds = GUILayout.Toggle(CheatToggles.chatCmds, " Host Chat Commands (/kick /ban /mute /color /role /start /end /meeting /close /fix)");
        CheatToggles.colorCmd = GUILayout.Toggle(CheatToggles.colorCmd, " Color Commands (/c <color>)");
        CheatToggles.colorCmdNotify = GUILayout.Toggle(CheatToggles.colorCmdNotify, " Notify Who Used Color Command");
    }

    private void DrawAdvanced()
    {
        GUILayout.Label("Advanced Chat", GUIStylePreset.TabSubtitle);
        CheatToggles.darkChatTheme = GUILayout.Toggle(CheatToggles.darkChatTheme, " Dark Chat Theme");
        CheatToggles.chatSenderInfo = GUILayout.Toggle(CheatToggles.chatSenderInfo, " Show Sender Info (host / level / platform)");
        CheatToggles.chatTimestamps = GUILayout.Toggle(CheatToggles.chatTimestamps, " Show Timestamps");
        CheatToggles.noChatCooldown = GUILayout.Toggle(CheatToggles.noChatCooldown, " No Chat Cooldown");

        GUILayout.Label($"Bubble History: {CheatToggles.chatHistorySize}");
        CheatToggles.chatHistorySize = (int)GUILayout.HorizontalSlider(CheatToggles.chatHistorySize, 4, 200);

        GUILayout.Space(5);
        CheatToggles.banWords = GUILayout.Toggle(CheatToggles.banWords, " Ban-Word Filter (HyperMenu/BanWords.txt)");
        CheatToggles.xmasHostCommand = GUILayout.Toggle(CheatToggles.xmasHostCommand, " Host /xmas Rainbow Command");
    }
}
