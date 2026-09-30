using UnityEngine;
using System.Collections.Generic;
using UnityEngine.SceneManagement;


namespace MalumMenu;

public class MenuUI : MonoBehaviour
{
    public static int windowHeight => (int)(600 * MalumMenu.menuScale.Value * MalumMenu.menuHeightMult.Value);
    public static int windowWidth => (int)(800 * MalumMenu.menuScale.Value * MalumMenu.menuWidthMult.Value);

    public static bool isGUIActive = false;
    private Rect windowRect;
    public Rect WindowRect => windowRect;
    private List<ITab> _tabs = new();
    private int _selectedTab;
    private Vector2 _tabScrollPosition = Vector2.zero;
    public static float hue; // Pour le mode RGB
    private bool _wasInGameplay = false;
    private Vector2 _contentScrollPosition = Vector2.zero;

    private void Start()
    {
        // Ajoute tous les onglets au démarrage
        _tabs.Add(new MovementTab());
        _tabs.Add(new SelfTab());
        _tabs.Add(new ESPTab());
        _tabs.Add(new RolesTab());
        _tabs.Add(new PlayersTab());
        _tabs.Add(new ShipTab());
        _tabs.Add(new SabotageTab());
        _tabs.Add(new ChatTab());
        _tabs.Add(new AnimationsTab());
        _tabs.Add(new ConsoleTab());
        _tabs.Add(new HostOnlyTab());
        _tabs.Add(new HostOnlyTab2());
        _tabs.Add(new PassiveTab());
        _tabs.Add(new TrollTab());
        _tabs.Add(new ProtectionsTab());
        _tabs.Add(new AnticheatTab());
        _tabs.Add(new ModesTab());
        _tabs.Add(new ConfigTab());
        // _tabs.Add(new OverloadTab());

        // Initialise la zone 2D de MenuUI
        windowRect = new(
            Screen.width / 2f - windowWidth / 2f,
            Screen.height / 2f - windowHeight / 2f,
            windowWidth,
            windowHeight
        );
        _tabs.Add(new SettingsTab());
    }

    public void InitStyles()
    {
        int fontSize = (int)(14 * MalumMenu.menuTextScale.Value);
        GUI.skin.toggle.fontSize = GUI.skin.button.fontSize = GUI.skin.label.fontSize = fontSize;
        GUI.skin.window.padding = new RectOffset { left = 12, right = 12, top = 30, bottom = 12 };
        GUI.skin.window.margin = new RectOffset { left = 8, right = 8, top = 8, bottom = 8 };

        GUIStylePreset.ApplyScale(MalumMenu.menuTextScale.Value);
    }

    private void Update()
    {

        if (Input.GetKeyDown(Utils.StringToKeycode(MalumMenu.menuKeybind.Value)))
        {
            // Active ou désactive l'interface avec la touche DELETE
            isGUIActive = !isGUIActive;

            if (MalumMenu.menuOpenOnMouse.Value)
            {
                // Téléporte la fenêtre vers la souris pour une utilisation immédiate
                Vector2 mousePosition = Input.mousePosition;
                windowRect.position = new Vector2(mousePosition.x, Screen.height - mousePosition.y);
            }
        }

        if (CheatToggles.rgbMode)
        {
            hue += Time.deltaTime * 0.3f; // Ajuste la vitesse de changement de couleur, multiplicateur plus élevé = plus rapide
            if (hue > 1f) hue -= 1f; // Fait boucler la teinte à 0 quand elle dépasse 1
        }

        if (CheatToggles.stealthMode != MalumMenu.inStealthMode)
        {
            MalumMenu.inStealthMode = CheatToggles.stealthMode;

            Scene scene = SceneManager.GetActiveScene();

            if (scene.name == "MainMenu" || scene.name == "MatchMaking")
            {
                SceneManager.LoadScene(scene.name);
            }
        }

        if (CheatToggles.panicMode) Utils.Panic();

        var stamp = ModManager.Instance.ModStamp;
        if (stamp) stamp.enabled = !(MalumMenu.inStealthMode || MalumMenu.isPanicked);

        if (CheatToggles.openConfig)
        {
            Utils.OpenConfigFile();
            CheatToggles.openConfig = false;
        }

        // Vérifie si la manche vient de se terminer et désactive les triches de sabotage
        bool currentlyInGameplay = Utils.isPlayer && Utils.isShip;
        if (_wasInGameplay && !currentlyInGameplay)
        {
            DisableSabotageCheats();
        }
        _wasInGameplay = currentlyInGameplay;
        if (CheatToggles.reloadConfig)
        {
            MalumMenu.Plugin.Config.Reload();
            CheatToggles.reloadConfig = false;
        }

        if (CheatToggles.saveProfile)
        {
            CheatToggles.saveProfile = false; // Désactive d'abord pour éviter de le sauvegarder dans le profil
            CheatToggles.SaveTogglesToProfile();
        }

        if (CheatToggles.loadProfile)
        {
            CheatToggles.LoadTogglesFromProfile();
            CheatToggles.loadProfile = false;
        }

        // Certaines triches ne fonctionnent que si le LocalPlayer existe, donc elles sont désactivées s'il n'existe pas
        if(!Utils.isPlayer)
        {
            CheatToggles.setFakeRole = false;
            CheatToggles.setFakeAlive = false;
            CheatToggles.killAll = false;
            CheatToggles.telekillPlayer = false;
            CheatToggles.killAllCrew = false;
            CheatToggles.killAllImps = false;
            CheatToggles.teleportPlayer = false;
            CheatToggles.spectate = false;
            CheatToggles.freecam = false;
            CheatToggles.killPlayer = false;
            CheatToggles.callMeeting = false;

            if (CheatToggles.runOverload)
            {
                OverloadUI.StopOverload();
            }
        }

        // Certaines triches ne fonctionnent que si le vaisseau existe, donc elles sont désactivées s'il n'existe pas
        if(!Utils.isShip)
        {
            CheatToggles.sabotageMap = false;
            CheatToggles.unfixableLights = false;
            CheatToggles.completeMyTasks = false;
            CheatToggles.kickVents = false;
            CheatToggles.reportBody = false;
            CheatToggles.closeMeeting = false;
            CheatToggles.reactorSab = false;
            CheatToggles.oxygenSab = false;
            CheatToggles.commsSab = false;
            CheatToggles.elecSab = false;
            CheatToggles.mushSab = false;
            CheatToggles.closeAllDoors = false;
            CheatToggles.openAllDoors = false;
            CheatToggles.spamCloseAllDoors = false;
            CheatToggles.spamOpenAllDoors = false;
            CheatToggles.mushSpore = false;

            MalumCheats.StopShipAnimCheats();
            MalumCheats.CleanUpInjectedTasks();
        }

        if(!Utils.isHost && !Utils.isFreePlay)
        {
            CheatToggles.killAll = false;
            CheatToggles.telekillPlayer = false;
            CheatToggles.killAllCrew = false;
            CheatToggles.killAllImps = false;
            CheatToggles.killPlayer = false;
            CheatToggles.ejectPlayer = false;
            CheatToggles.noKillCd = false;
            CheatToggles.killAnyone = false;
            CheatToggles.killVanished = false;
            CheatToggles.forceStartGame = false;
            CheatToggles.skipMeeting = false;
            CheatToggles.voteImmune = false;
            CheatToggles.noGameEnd = false;
            CheatToggles.showProtectMenu = false;
            CheatToggles.showRolesMenu = false;
            CheatToggles.noOptionsLimits = false;
        }

        // Certaines triches ne fonctionnent qu'en réunion, donc elles sont désactivées si ce n'est pas le cas
        if (!Utils.isMeeting)
        {
            CheatToggles.skipMeeting = false;
            CheatToggles.ejectPlayer = false;
        }
    }

    public void OnGUI()
    {
        if (!isGUIActive || MalumMenu.isPanicked) return;

        InitStyles();

        if (Mathf.Abs(windowRect.width - windowWidth) > 1f || Mathf.Abs(windowRect.height - windowHeight) > 1f)
        {
            windowRect.width = windowWidth;
            windowRect.height = windowHeight;
            windowRect.x = Screen.width / 2f - windowRect.width / 2f;
            windowRect.y = Screen.height / 2f - windowRect.height / 2f;
        }

        UIHelpers.ApplyUIColor();

        // ✏️ C'EST ICI QUE TU CHANGES LE TITRE DU MENU !
        windowRect = GUI.Window((int)WindowId.MenuUI, windowRect, (GUI.WindowFunction)WindowFunction, "AmongUs By Dylan " + MalumMenu.hyperVersion + ", " + MalumMenu.hyperBuild + " build.");
    }

    private void DisableSabotageCheats()
    {
        CheatToggles.sabotageMap = false;
        CheatToggles.unfixableLights = false;
        CheatToggles.commsSab = false;
        CheatToggles.elecSab = false;
        CheatToggles.reactorSab = false;
        CheatToggles.oxygenSab = false;
        CheatToggles.mushSab = false;
        CheatToggles.mushSpore = false;
        CheatToggles.closeAllDoors = false;
        CheatToggles.openAllDoors = false;
        CheatToggles.spamCloseAllDoors = false;
        CheatToggles.spamOpenAllDoors = false;
    }

    public void WindowFunction(int windowID)
    {
        GUILayout.BeginHorizontal();

        // Sélecteur d'onglets à gauche (18% de largeur)
        GUILayout.BeginVertical(GUIStylePreset.ModernBox, GUILayout.Width(windowWidth * 0.2f));
        GUILayout.Space(2);

        _tabScrollPosition = GUILayout.BeginScrollView(_tabScrollPosition, false, true);

        for (var i = 0; i < _tabs.Count; i++)
        {
            Color standardColor = GUI.backgroundColor;

            if (_selectedTab == i)
            {
                GUI.backgroundColor = new Color(0.35f, 0.42f, 0.55f, 1f);
            }

            if (GUILayout.Button(_tabs[i].name, GUIStylePreset.TabButton, GUILayout.Height(40)))
                _selectedTab = i;

            GUI.backgroundColor = standardColor;
        }

        GUILayout.EndScrollView();

        GUILayout.Space(4);
        GUILayout.EndVertical();

        GUILayout.Space(10f);

        // Contenu et contrôles de l'onglet à droite (82% de largeur)
        GUILayout.BeginVertical(GUIStylePreset.ModernBox, GUILayout.Width(windowWidth * 0.8f));
        GUILayout.Space(2);

        // Contenu spécifique à l'onglet
        if (_selectedTab >= 0 && _selectedTab < _tabs.Count)
        {
            GUILayout.Label(_tabs[_selectedTab].name, GUIStylePreset.TabTitle);
            GUILayout.Box("", GUIStylePreset.Separator, GUILayout.Height(2f), GUILayout.ExpandWidth(true));
            GUILayout.Space(6);
            _contentScrollPosition = GUILayout.BeginScrollView(_contentScrollPosition, false, false);
            _tabs[_selectedTab].Draw();
            GUILayout.EndScrollView();
        }

        GUILayout.EndVertical();

        GUILayout.EndHorizontal();

        // Rend la fenêtre déplaçable
        GUI.DragWindow();
    }
}