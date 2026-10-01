using UnityEngine;
using Il2CppSystem.Collections.Generic;

namespace MalumMenu;

public class DoorsUI : MonoBehaviour
{
    public static int windowHeight = 270;
    public static int windowWidth = 480;
    public static Rect windowRect;

    private List<SystemTypes> _doorsToSpamOpen = new();
    private List<SystemTypes> _doorsToSpamClose = new();

    private void Start()
    {
        // Initialise la zone 2D de DoorsUI
        windowRect = new(
            Screen.width / 2f - windowWidth / 2f,
            Screen.height / 2f - windowHeight / 2f,
            windowWidth,
            windowHeight
        );
    }

    private void OnGUI()
    {
        if (!CheatToggles.showDoorsMenu || !(MenuUI.isGUIActive || MalumMenu.menuKeepSubwindowsOpen.Value) || MalumMenu.isPanicked) return;

        UIHelpers.ApplyUIColor();

        windowRect = GUI.Window((int)WindowId.DoorsUI, windowRect, (GUI.WindowFunction)DoorsWindow, "Portes");
    }

    private string GetRoomName(SystemTypes room)
    {
        switch (room.ToString())
        {
            case "Cafeteria": return "Cafétéria";
            case "Storage": return "Stockage";
            case "MedBay": return "Infirmerie";
            case "Security": return "Sécurité";
            case "UpperEngine": return "Moteur supérieur";
            case "LowerEngine": return "Moteur inférieur";
            case "Electrical": return "Électrique";
            case "Office": return "Bureau";
            case "Laboratory": return "Laboratoire";
            case "Decontamination": return "Décontamination";
            case "Weapons": return "Armurerie";
            case "Comms": return "Communications";
            case "Brig": return "Cellule";
            case "Records": return "Archives";
            case "MainHall": return "Hall principal";
            case "Kitchen": return "Cuisine";
            case "Medical": return "Médical";
            case "Lounge": return "Salon";
            default: return room.ToString();
        }
    }

    private void DoorsWindow(int windowID)
    {
        if (!Utils.isShip)
        {
            GUI.DragWindow();
            return;
        }

        var map = (MapNames)Utils.GetCurrentMapID();

        if (map is MapNames.MiraHQ)
        {
            GUI.DragWindow();
            return;
        }

        GUILayout.BeginVertical();

        foreach (var doorRoom in DoorsHandler.GetRoomsWithDoors())
        {
            GUILayout.BeginHorizontal();

            GUILayout.Label($"{GetRoomName(doorRoom)}", GUILayout.Width(110f));

            GUILayout.BeginHorizontal();

            GUILayout.Label($"{DoorsHandler.GetStatusOfDoorsInRoom(doorRoom, true)}");

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Fermer", GUIStylePreset.NormalButton, GUILayout.Width(50f)))
            {
                DoorsHandler.CloseDoorsInRoom(doorRoom);
            }

            if (map is MapNames.Polus or MapNames.Airship or MapNames.Fungle)
            {
                if (GUILayout.Button("Ouvrir", GUIStylePreset.NormalButton, GUILayout.Width(50f)))
                {
                    DoorsHandler.OpenDoorsInRoom(doorRoom);
                }
            }

            if (Utils.isHost)
            {
                var spamClose = _doorsToSpamClose.Contains(doorRoom);
                spamClose = GUILayout.Toggle(spamClose, "Spam fermeture", GUIStylePreset.NormalToggle);

                if (spamClose && !_doorsToSpamClose.Contains(doorRoom))
                {
                    _doorsToSpamClose.Add(doorRoom);
                }
                else if (!spamClose && _doorsToSpamClose.Contains(doorRoom))
                {
                    _doorsToSpamClose.Remove(doorRoom);
                }

                if (map is MapNames.Polus or MapNames.Airship or MapNames.Fungle)
                {
                    var spamOpen = _doorsToSpamOpen.Contains(doorRoom);
                    spamOpen = GUILayout.Toggle(spamOpen, "Spam ouverture", GUIStylePreset.NormalToggle);

                    if (spamOpen && !_doorsToSpamOpen.Contains(doorRoom))
                    {
                        _doorsToSpamOpen.Add(doorRoom);
                    }
                    else if (!spamOpen && _doorsToSpamOpen.Contains(doorRoom))
                    {
                        _doorsToSpamOpen.Remove(doorRoom);
                    }
                }
            }
            else
            {
                // Vide les listes de spam si on n'est pas hôte
                if (_doorsToSpamClose.Count != 0 || _doorsToSpamOpen.Count != 0)
                {
                    _doorsToSpamClose.Clear();
                    _doorsToSpamOpen.Clear();
                }
            }

            GUILayout.EndHorizontal();

            GUILayout.EndHorizontal();
        }

        GUILayout.FlexibleSpace();

        GUILayout.Box("", GUIStylePreset.Separator, GUILayout.Height(1f), GUILayout.ExpandWidth(true));
        GUILayout.Space(1f);

        GUILayout.BeginHorizontal();

        if (GUILayout.Button("Tout fermer", GUIStylePreset.NormalButton))
        {
            CheatToggles.closeAllDoors = true;
        }

        if (map is MapNames.Polus or MapNames.Airship or MapNames.Fungle)
        {
            if (GUILayout.Button("Tout ouvrir", GUIStylePreset.NormalButton))
            {
                CheatToggles.openAllDoors = true;
            }
        }

        GUILayout.FlexibleSpace();

        if (Utils.isHost)
        {
            CheatToggles.spamCloseAllDoors = GUILayout.Toggle(CheatToggles.spamCloseAllDoors, "Spam fermer tout", GUIStylePreset.NormalToggle);

            if (map is MapNames.Polus or MapNames.Airship or MapNames.Fungle)
            {
                CheatToggles.spamOpenAllDoors = GUILayout.Toggle(CheatToggles.spamOpenAllDoors, "Spam ouvrir tout", GUIStylePreset.NormalToggle);
            }
        }
        else
        {
            CheatToggles.spamCloseAllDoors = CheatToggles.spamOpenAllDoors = false;
        }

        GUILayout.EndHorizontal();

        GUILayout.EndVertical();

        GUI.DragWindow();
    }

    public void Update()
    {
        if (!Utils.isShip) return;

        // Spam de fermeture des portes sélectionnées
        foreach (var doorRoom in _doorsToSpamClose)
        {
            DoorsHandler.CloseDoorsInRoom(doorRoom);
        }

        // Spam d'ouverture des portes sélectionnées
        var map = (MapNames)Utils.GetCurrentMapID();

        if (map is MapNames.Polus or MapNames.Airship or MapNames.Fungle)
        {
            foreach (var doorRoom in _doorsToSpamOpen)
            {
                DoorsHandler.OpenDoorsInRoom(doorRoom);
            }
        }
    }
}
