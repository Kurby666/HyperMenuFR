using System;
using UnityEngine;

namespace MalumMenu;

public class KeybindListener : MonoBehaviour
{
    private const int HandlingId = 60201;

    public void Update()
    {
        try
        {
            if (MalumMenu.isPanicked) return;

            // Keybinds aren't triggered from typing in the chat
            if (HudManager.InstanceExists && HudManager.Instance.Chat && HudManager.Instance.Chat.IsOpenOrOpening) return;

            // Typing into any menu text field (search box, keybind filter, colour picker) must
            // not also toggle the cheat that key happens to be bound to.
            if (TextField.AnyFocused) return;

            // Master off switch from the Settings tab. It covers the per-cheat Keybinds below and,
            // via the identical guard inside Cheats.Hotkeys.Down/Held, the action hotkeys that
            // Cheats.Hotkeys.Tick() dispatches further down.
            if (!CheatToggles.hotkeysEnabled) return;

            // Check each keybind to see if the user pressed it and toggle the corresponding cheat
            foreach (var (name, key) in CheatToggles.Keybinds)
            {
                if (key == KeyCode.None) continue;
                if (!Input.GetKeyDown(key)) continue;

                if (!CheatToggles.ToggleFields.TryGetValue(name, out var field)) continue;

                var current = (bool)field.GetValue(null);
                field.SetValue(null, !current);
            }

            // Action hotkeys (Menu/NocturneQuick equivalents) share this update so they inherit
            // the same panic + chat-open guards the cheat keybinds already had.
            Cheats.Hotkeys.Tick();
        }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "KeybindListener.Update: poll cheat keybinds"); }
    }
}
