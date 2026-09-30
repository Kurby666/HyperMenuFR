using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using MalumMenu.features;

namespace MalumMenu;

public class ConfigTab : ITab
{
    public string name => "Configuration";

    public readonly Dictionary<string, int> versions = new Dictionary<string, int>()
        {
			// Version actuelle à l'exécution
			// VersionShower::Start utilise ReferenceDataManager.Refdata.userFacingVersion pour obtenir des chaînes de version telles que "17.1" mais ça ne semble pas fonctionner avant que le jeu soit complètement chargé, donc on doit utiliser Constants::AddressablesVersion pour obtenir une chaîne de version moins lisible
			{ $"{Constants.AddressablesVersion} (Actuelle)", Constants.GetBroadcastVersion() },
            { "16.1.0", 50632950 },
            { "17.1", 50643450 },
            { "17.1.2", 50647000 },
            { "17.2", 50645050 },
            { "17.2.1", 50652900 },
            { "17.2.2", 50653700 }
        };

    private int versionSelection = 0;

    public void Draw()
    {
        GUILayout.BeginVertical(GUILayout.Width(MenuUI.windowWidth * 0.425f));

        DrawGeneral();

        GUILayout.Space(10);
        
		Chat.OnChat.LogChatMessages = GUILayout.Toggle(Chat.OnChat.LogChatMessages, "Enregistrer les messages de chat dans la console");

		if(GUILayout.Button("Effacer les notifications"))
		{
			MalumMenu.notifications.ClearNotifications();
			MalumMenu.notifications.Send("Notifications", "Toutes les notifications ont été effacées.", 5);
		}

        GUILayout.Space(10);

        Spoofer.shouldSpoofVersion = GUILayout.Toggle(Spoofer.shouldSpoofVersion, "Activer la falsification de version");

        GUILayout.Label($"Version falsifiée : {versions.ElementAt(versionSelection).Key} ({Spoofer.spoofedVersion})");
        versionSelection = (int)GUILayout.HorizontalSlider(versionSelection, 0, versions.Count - 1);
        Spoofer.spoofedVersion = versions.ElementAt(versionSelection).Value;

        Spoofer.useModdedProtocol = GUILayout.Toggle(Spoofer.useModdedProtocol, "Utiliser le protocole moddé");

        GUILayout.Label($"Plateforme falsifiée : {Spoofer.spoofedPlatform}");
        Spoofer.spoofedPlatform = (Platforms)GUILayout.HorizontalSlider((float)Spoofer.spoofedPlatform, 0, 10);

        GUILayout.EndVertical();
    }

    private void DrawGeneral()
    {
        CheatToggles.openConfig = GUILayout.Toggle(CheatToggles.openConfig, " Ouvrir la configuration");

        CheatToggles.reloadConfig = GUILayout.Toggle(CheatToggles.reloadConfig, " Recharger la configuration");

        CheatToggles.saveProfile = GUILayout.Toggle(CheatToggles.saveProfile, " Sauvegarder dans le profil");

        CheatToggles.loadProfile = GUILayout.Toggle(CheatToggles.loadProfile, " Charger depuis le profil");
    }
}