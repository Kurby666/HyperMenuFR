using UnityEngine;

namespace MalumMenu;

public class ChatTab : ITab
{
    public string name => "Chat";

    private TextField _chatColorField;
    private bool _initialized = false;

    public void Draw()
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

        GUILayout.EndVertical();
    }

    public void Initialize()
    {
        _chatColorField = new TextField(MalumMenu.menuChatColor.Value);
    }

    private void DrawGeneral()
    {
        CheatToggles.enableChat = GUILayout.Toggle(CheatToggles.enableChat, " Activer le chat");

        CheatToggles.bypassUrlBlock = GUILayout.Toggle(CheatToggles.bypassUrlBlock, " Contourner le blocage d'URL");

        CheatToggles.lowerRateLimits = GUILayout.Toggle(CheatToggles.lowerRateLimits, " Réduire les limites de débit");
    }

    private void DrawTextbox()
    {
        GUILayout.Label("Zone de texte", GUIStylePreset.TabSubtitle);

        CheatToggles.unlockCharacters = GUILayout.Toggle(CheatToggles.unlockCharacters, " Débloquer les caractères spéciaux");

        CheatToggles.longerMessages = GUILayout.Toggle(CheatToggles.longerMessages, " Autoriser les messages plus longs");

        CheatToggles.unlockClipboard = GUILayout.Toggle(CheatToggles.unlockClipboard, " Débloquer le presse-papiers");
    }

    private void DrawColorSettings()
    {
        GUILayout.Label("Couleur du chat", GUIStylePreset.TabSubtitle);
        CheatToggles.colorAsPlayer = GUILayout.Toggle(CheatToggles.colorAsPlayer, " Messages colorés selon la couleur de l'expéditeur");
        CheatToggles.changeChatColor = GUILayout.Toggle(CheatToggles.changeChatColor, " Activer la couleur de chat personnalisée");
        GUILayout.Space(5);
        GUILayout.BeginHorizontal();
        GUILayout.Label("Couleur HTML du chat :", GUILayout.Width(150));
        _chatColorField.Draw(150);
        if (GUILayout.Button("Sauvegarder", GUILayout.Width(100)))
        {
            MalumMenu.menuChatColor.Value = _chatColorField.Content;
        }
        GUILayout.EndHorizontal();
    }
}