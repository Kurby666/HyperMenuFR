using UnityEngine;

namespace MalumMenu;

public class ModesTab : ITab
{
    public string name => "Modes";

    public void Draw()
    {
        GUILayout.BeginVertical(GUILayout.Width(MenuUI.windowWidth * 0.425f));

        DrawGeneral();

        GUILayout.EndVertical();
    }

    private void DrawGeneral()
    {
        CheatToggles.rgbMode = GUILayout.Toggle(CheatToggles.rgbMode, " Mode RGB");

        CheatToggles.stealthMode = GUILayout.Toggle(CheatToggles.stealthMode, " Mode furtif");

        if (MalumMenu.isDevRelease)
        {
            CheatToggles.streamerMode = GUILayout.Toggle(CheatToggles.streamerMode, " Mode streamer");
        }
        else
        {
            GUILayout.Label("Bientôt disponible : Mode streamer");

        }
        
        CheatToggles.panicMode = GUILayout.Toggle(CheatToggles.panicMode, " Mode panique");
    }
}