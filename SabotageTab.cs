using UnityEngine;
using System.Collections.Generic;

namespace MalumMenu;

public class SabotageTab : ITab
{
    public string name => "Sabotage";

    public void Draw()
    {
        GUILayout.BeginVertical(GUILayout.Width(MenuUI.windowWidth * 0.425f));

        if (ShipStatus.Instance == null)
        {
            GUILayout.Label("Tu n'es pas actuellement en partie, ou la partie n'a pas encore commencé. Ces options ne fonctionneront pas.");
        }

        Sabotage.UpdateSystemsDirectly = GUILayout.Toggle(Sabotage.UpdateSystemsDirectly, "Mettre à jour les systèmes de sabotage directement");

        Dictionary<string, SystemTypes> sabotages = Sabotage.GetSabotages();
        Dictionary<string, SystemTypes> doors = Sabotage.GetDoors();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Sabotager tout"))
        {
            Sabotage.SabotageAll();
            MalumMenu.notifications.Send("Sabotage", "Tous les sabotages ont été activés.", 5);
        }

        if (GUILayout.Button("Fermer toutes les portes"))
        {
            Sabotage.LockAll();
            MalumMenu.notifications.Send("Sabotage", "Toutes les portes ont été fermées.", 5);
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Réparer tous les sabotages"))
        {
            Sabotage.FixAllSabotages();
            MalumMenu.notifications.Send("Sabotage", "Tous les sabotages ont été réparés.", 5);
        }

        if (GUILayout.Button("Déverrouiller toutes les portes"))
        {
            if (Sabotage.CanUnlockDoors())
            {
                Sabotage.UnlockAll();
                MalumMenu.notifications.Send("Sabotage", "Toutes les portes ont été déverrouillées.", 5);
            }
            else
            {
                MalumMenu.notifications.Send("Sabotage", "La carte sur laquelle tu es ne permet pas de déverrouiller les portes.", 10);
            }
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(5);
        GUILayout.Label("Sabotages :");
        foreach (var (key, value) in sabotages)
        {
            if (GUILayout.Button(key))
            {
                Sabotage.SabotageSystem(value);
                MalumMenu.notifications.Send("Sabotage", $"{key} a été saboté.", 5);
            }
        }

        GUILayout.Label("Fermer les portes :");
        if (doors.Count == 0)
        {
            GUILayout.Label("Cette carte n'a pas de portes pouvant être fermées.");
            return;
        }

        byte i = 0;
        foreach (var (key, value) in doors)
        {
            if (i % 2 == 0)
            {
                GUILayout.BeginHorizontal();
            }

            if (GUILayout.Button(key))
            {
                Sabotage.LockDoor(value);
            }

            if (i % 2 != 0)
            {
                GUILayout.EndHorizontal();
            }

            i++;
        }

        // Si le nombre de sabotages de portes est impair, on ne termine pas la ligne horizontale, donc on vérifie ici
        if (i % 2 != 0)
        {
            GUILayout.EndHorizontal();
        }

        GUILayout.EndVertical();
    }
}