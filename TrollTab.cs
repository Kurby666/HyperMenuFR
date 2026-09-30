using MalumMenu.features;
using UnityEngine;

namespace MalumMenu;

public class TrollTab : ITab
{
    public string name => "Troll";

    public void Draw()
    {
        if (PlayerControl.LocalPlayer == null)
        {
            GUILayout.Label("Tu n'es pas actuellement en partie, ces options ne fonctionneront pas.");
        }

        GUILayout.BeginVertical(GUILayout.Width(MenuUI.windowWidth * 0.425f));

        Troll.AutoReportBodies.Enabled = GUILayout.Toggle(Troll.AutoReportBodies.Enabled, "Signaler automatiquement les corps");
        MalumMenu.routines.autoTriggerSpores.Enabled = GUILayout.Toggle(MalumMenu.routines.autoTriggerSpores.Enabled, "Déclencher auto les spores");
        Troll.BlockSabotages.Enabled = GUILayout.Toggle(Troll.BlockSabotages.Enabled, "Bloquer les sabotages");
        Troll.BlockVenting.Enabled = GUILayout.Toggle(Troll.BlockVenting.Enabled, "Désactiver les conduits");

        if (GUILayout.Button(" Déclencher toutes les spores"))
        {
            if (Utilities.GetCurrentMap() != MapNames.Fungle)
               {
                   MalumMenu.notifications.Send("Déclencher les spores", "Cette option ne fonctionne que sur la carte Fungle.");
               }
               else
               {
                   FungleShipStatus shipStatus = ShipStatus.Instance.Cast<FungleShipStatus>();

                   foreach (Mushroom mushroom in shipStatus.sporeMushrooms.Values)
                   {
                       PlayerControl.LocalPlayer.RpcTriggerSpores(mushroom);
                   }

                   MalumMenu.notifications.Send("Déclencher les spores", "Toutes les spores ont été déclenchées.", 5);
            }
        }

        if (GUILayout.Button(" Copier un joueur aléatoire"))
        {
            PlayerControl randomPl = Utilities.GetRandomPlayer();
            Utilities.CopyPlayer(randomPl);
        }

        GUILayout.Space(5);

        GUILayout.Label("Troll de portes :");
        MalumMenu.routines.doorTroller.Enabled = GUILayout.Toggle(MalumMenu.routines.doorTroller.Enabled, "Activé");

        GUILayout.Label($"Délai de verrouillage/déverrouillage : {MalumMenu.routines.doorTroller.doorDelay:F2}s");
        MalumMenu.routines.doorTroller.doorDelay = GUILayout.HorizontalSlider(MalumMenu.routines.doorTroller.doorDelay, 0.1f, 2.0f);

        GUILayout.EndVertical();
    }
}