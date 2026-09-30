using UnityEngine;
using System.Globalization;

namespace MalumMenu;

public class SettingsTab : ITab
{
    public string name => "Paramètres";

    private bool _initialized = false;

    // Champs de texte personnalisés
    private TextField _menuKeybindField;
    private TextField _menuColorField;
    private TextField _spoofLevelField;
    private TextField _spoofPlatformField;

    // Valeurs d'échelle en attente (appliquées au clic sur Appliquer)
    private float _pendingScale;
    private float _pendingWidthMult;
    private float _pendingHeightMult;
    private float _pendingTextScale;

    public void Draw()
    {
        if (!_initialized)
        {
            InitializeInputFields();
            _initialized = true;
        }

        GUILayout.BeginVertical(GUILayout.Width(MenuUI.windowWidth * 0.425f));

        DrawGUISettings();

        GUILayout.Space(15);

        DrawSpoofingSettings();

        GUILayout.Space(15);

        DrawPrivacySettings();

        GUILayout.EndVertical();
    }

    private void InitializeInputFields()
    {
        _menuKeybindField = new TextField(MalumMenu.menuKeybind.Value);
        _menuColorField = new TextField(MalumMenu.menuHtmlColor.Value);
        _spoofLevelField = new TextField(MalumMenu.spoofLevel.Value);
        _spoofPlatformField = new TextField(MalumMenu.spoofPlatform.Value);

        _pendingScale = MalumMenu.menuScale.Value;
        _pendingWidthMult = MalumMenu.menuWidthMult.Value;
        _pendingHeightMult = MalumMenu.menuHeightMult.Value;
        _pendingTextScale = MalumMenu.menuTextScale.Value;
    }


    private void DrawGUISettings()
    {
        GUILayout.Label("Paramètres de l'interface", GUIStylePreset.TabSubtitle);

        GUILayout.BeginHorizontal();
        GUILayout.Label("Raccourci du menu :", GUILayout.Width(150));
        _menuKeybindField.Draw(150);
        if (GUILayout.Button("Sauvegarder", GUILayout.Width(100)))
        {
            MalumMenu.menuKeybind.Value = _menuKeybindField.Content;
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(5);

        GUILayout.BeginHorizontal();
        GUILayout.Label("Couleur du menu (HTML) :", GUILayout.Width(150));
        _menuColorField.Draw(150);
        if (GUILayout.Button("Sauvegarder", GUILayout.Width(100)))
        {
            MalumMenu.menuHtmlColor.Value = _menuColorField.Content;
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(5);

        MalumMenu.menuOpenOnMouse.Value = GUILayout.Toggle(MalumMenu.menuOpenOnMouse.Value, " Ouvrir le menu à la position de la souris");

        GUILayout.Space(5);

        MalumMenu.autoLoadProfile.Value = GUILayout.Toggle(MalumMenu.autoLoadProfile.Value, " Charger automatiquement le profil au démarrage");

        GUILayout.Space(5);

        GUILayout.BeginHorizontal();
        GUILayout.Label($"Échelle du menu : {_pendingScale:F2}", GUILayout.Width(150));
        _pendingScale = GUILayout.HorizontalSlider(_pendingScale, 0.5f, 2.0f);
        GUILayout.EndHorizontal();

        GUILayout.Space(5);

        GUILayout.BeginHorizontal();
        GUILayout.Label($"Largeur : {_pendingWidthMult:F2}", GUILayout.Width(150));
        _pendingWidthMult = GUILayout.HorizontalSlider(_pendingWidthMult, 0.5f, 2.0f);
        GUILayout.EndHorizontal();

        GUILayout.Space(5);

        GUILayout.BeginHorizontal();
        GUILayout.Label($"Hauteur : {_pendingHeightMult:F2}", GUILayout.Width(150));
        _pendingHeightMult = GUILayout.HorizontalSlider(_pendingHeightMult, 0.5f, 2.0f);
        GUILayout.EndHorizontal();

        GUILayout.Space(5);

        GUILayout.BeginHorizontal();
        GUILayout.Label($"Échelle du texte : {_pendingTextScale:F2}", GUILayout.Width(150));
        _pendingTextScale = GUILayout.HorizontalSlider(_pendingTextScale, 0.5f, 2.0f);
        GUILayout.EndHorizontal();

        GUILayout.Space(10);

        if (GUILayout.Button("Appliquer", GUILayout.Width(200)))
        {
            MalumMenu.menuScale.Value = _pendingScale;
            MalumMenu.menuWidthMult.Value = _pendingWidthMult;
            MalumMenu.menuHeightMult.Value = _pendingHeightMult;
            MalumMenu.menuTextScale.Value = _pendingTextScale;
        }
    }

    private void DrawSpoofingSettings()
    {
        GUILayout.Label("Paramètres de falsification", GUIStylePreset.TabSubtitle);

        GUILayout.BeginHorizontal();
        GUILayout.Label("Niveau falsifié (1-100001) :", GUILayout.Width(150));
        _spoofLevelField.Draw(150);
        if (GUILayout.Button("Sauvegarder", GUILayout.Width(100)))
        {
            // Valide que c'est un nombre entre 1 et 100001
            if (int.TryParse(_spoofLevelField.Content, NumberStyles.Integer, CultureInfo.InvariantCulture, out int level) &&
                level >= 1 && level <= 100001)
            {
                MalumMenu.spoofLevel.Value = _spoofLevelField.Content;
            }
            else
            {
                _spoofLevelField.Content = MalumMenu.spoofLevel.Value;
            }
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(5);

        GUILayout.BeginHorizontal();
        GUILayout.Label("Plateforme falsifiée :", GUILayout.Width(150));
        _spoofPlatformField.Draw(150);
        if (GUILayout.Button("Sauvegarder", GUILayout.Width(100)))
        {
            MalumMenu.spoofPlatform.Value = _spoofPlatformField.Content;
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(5);

        GUILayout.Label("Plateformes supportées : StandaloneEpicPC, StandaloneSteamPC, StandaloneMac, StandaloneWin10, etc.");
    }

    private void DrawPrivacySettings()
    {
        GUILayout.Label("Paramètres de confidentialité", GUIStylePreset.TabSubtitle);

        MalumMenu.spoofDeviceId.Value = GUILayout.Toggle(MalumMenu.spoofDeviceId.Value, " Cacher l'ID de l'appareil");

        GUILayout.Space(5);

        MalumMenu.noTelemetry.Value = GUILayout.Toggle(MalumMenu.noTelemetry.Value, " Désactiver la télémétrie");

        GUILayout.Space(10);

        if (GUILayout.Button("Ouvrir le fichier de config", GUILayout.Width(200)))
        {
            Utils.OpenConfigFile();
        }

        GUILayout.Space(5);

        GUILayout.Label("Pour plus d'options de configuration avancées, clique sur 'Ouvrir le fichier de config'", GUIStylePreset.TabSubtitle);
    }
}