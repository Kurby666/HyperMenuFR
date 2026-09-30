using UnityEngine;
using System;
using System.Collections.Generic;

namespace MalumMenu;

public class MovementTab : ITab
{
    public string name => "Mouvement";

    public void Draw()
    {
        GUILayout.BeginVertical(GUILayout.Width(MenuUI.windowWidth * 0.425f));

        if (PlayerControl.LocalPlayer == null)
        {
            GUILayout.Label("Tu dois être en partie pour que ça fonctionne.");
            GUILayout.EndVertical();
            return;
        }

        Vector2 position = PlayerControl.LocalPlayer.transform.position;

        GUILayout.Label($"Carte actuelle : {Utilities.GetCurrentMap()}\nPosition actuelle :\nX: {position.x:F2}\nY: {position.y:F2}");

        GUILayout.Space(15);

        DrawGeneral();

        GUILayout.Space(15);

        DrawTeleport();

        GUILayout.EndVertical();
    }

    private void DrawGeneral()
    {
        MalumMenu.Log.LogInfo($"Drawing General Movement Tab");
        CheatToggles.noClip = GUILayout.Toggle(CheatToggles.noClip, " Traverser les murs");

        CheatToggles.invertControls = GUILayout.Toggle(CheatToggles.invertControls, " Inverser les contrôles");

        try
        {
            if (PlayerControl.LocalPlayer.Data.IsDead)
            {
                PlayerControl.LocalPlayer.MyPhysics.GhostSpeed = GUILayout.HorizontalSlider(PlayerControl.LocalPlayer.MyPhysics.GhostSpeed, 0f, 20f, GUILayout.Width(250f));
                Utils.SnapSpeedToDefault(0.05f, true);
                GUILayout.Label($"Vitesse actuelle : {PlayerControl.LocalPlayer?.MyPhysics.GhostSpeed} {(Utils.IsSpeedDefault(true) ? "(Par défaut)" : "")}");
            }
            else
            {
                PlayerControl.LocalPlayer.MyPhysics.Speed = GUILayout.HorizontalSlider(PlayerControl.LocalPlayer.MyPhysics.Speed, 0f, 20f, GUILayout.Width(250f));
                Utils.SnapSpeedToDefault(0.05f);
                GUILayout.Label($"Vitesse actuelle : {PlayerControl.LocalPlayer?.MyPhysics.Speed} {(Utils.IsSpeedDefault() ? "(Par défaut)" : "")}");
            }
        } catch (NullReferenceException) { MalumMenu.Log.LogWarning($"Failed to draw general movement tab."); }
        MalumMenu.Log.LogInfo($"Finished Drawing General Movement Tab");
    }

    private void DrawTeleport()
    {
        MalumMenu.Log.LogInfo($"Drawing Teleport Tab");
        GUILayout.Label("Téléportation", GUIStylePreset.TabSubtitle);

        CheatToggles.teleportCursor = GUILayout.Toggle(CheatToggles.teleportCursor, " Vers le curseur");

        CheatToggles.teleportPlayer = GUILayout.Toggle(CheatToggles.teleportPlayer, " Vers un joueur");

        Teleporter.UseSnapToRPC = GUILayout.Toggle(Teleporter.UseSnapToRPC, "Utiliser SnapTo RPC pour les téléportations");
        GUILayout.Label("Téléportation vers un lieu :");

        Dictionary<string, Vector2> teleportLocations = Teleporter.GetTeleportLocations();

        byte i = 0;
        foreach (var (key, value) in teleportLocations)
        {
            if (i % 2 == 0)
            {
                GUILayout.BeginHorizontal();
            }

            if (GUILayout.Button(key))
            {
                Teleporter.TeleportTo(value);
            }

            if (i % 2 != 0)
            {
                GUILayout.EndHorizontal();
            }

            i++;
        }

        // Si le nombre de lieux de téléportation est impair, on ne termine pas la ligne horizontale, donc on vérifie ici
        if (i % 2 != 0)
        {
            GUILayout.EndHorizontal();
        }
        MalumMenu.Log.LogInfo($"Finished Drawing Teleport Tab");
    }
}