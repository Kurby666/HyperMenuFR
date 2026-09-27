using System;
using UnityEngine;

namespace MalumMenu;

// Port of othermenu NocturneMouseTools.cs (mouse select + wheel resize + self-drag).
// RMB teleport is NOT duplicated here — src already has it (MalumCheats.TeleportCursorCheat).
internal static class MouseTools
{
    private const int HandlingId = 20023;

    private static PlayerControl _selected;
    private static float _lastPick = -99f;
    private static bool _dragging;
    private static float _lastDragSnap = -99f;
    private static readonly Color Outline = new Color(0.30f, 0.62f, 1f, 1f);

    internal static PlayerControl Selected => _selected;

    internal static void Select(PlayerControl pc) => _selected = pc;

    // Called every frame from PlayerPhysics_LateUpdate.
    internal static void Tick()
    {
        try
        {
            bool sel = CheatToggles.mouseSelect;
            bool drag = CheatToggles.selfDrag;
            if ((!sel && !drag) || AmongUsClient.Instance == null || PlayerControl.LocalPlayer == null)
            {
                _dragging = false;
                Clear();
                return;
            }

            // Never hijack clicks while typing in game chat or clicking our own menu.
            if (MenuUI.isGUIActive || ChatOpen())
            {
                _dragging = false;
                return;
            }

            Camera cam = Camera.main;
            if (cam == null) return;

            bool grabbed = drag && SelfDrag(cam);

            if (!sel)
            {
                Clear();
                return;
            }

            if (_selected != null && (_selected.Data == null || _selected.Data.Disconnected))
                Clear();
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Clear();
                return;
            }

            if (!grabbed && Input.GetMouseButtonDown(0) && Time.unscaledTime - _lastPick > 0.2f)
            {
                _lastPick = Time.unscaledTime;
                Pick(cam);
            }

            Resize();
        }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "MouseTools.Tick: mouse select/drag"); }
    }

    private static bool ChatOpen()
    {
        try
        {
            HudManager hud = HudManager.Instance;
            ChatController chat = hud != null ? hud.Chat : null;
            return chat != null && chat.IsOpenOrOpening;
        }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "MouseTools.ChatOpen: check chat state"); return true; }
    }

    private static bool SelfDrag(Camera cam)
    {
        PlayerControl me = PlayerControl.LocalPlayer;
        if (me == null || me.inVent || me.onLadder)
        {
            _dragging = false;
            return false;
        }

        Vector2 m = World(cam, me);

        if (Input.GetMouseButtonDown(0))
            _dragging = me.CanMove;
        else if (Input.GetMouseButtonUp(0))
            _dragging = false;

        if (!_dragging || !me.CanMove) return _dragging;

        Vector2 cur = me.transform.position;
        Vector2 next = CheatToggles.selfDragSmooth
            ? Vector2.MoveTowards(cur, m, CheatToggles.selfDragSpeed * 0.05f)
            : m;
        if ((next - cur).sqrMagnitude < 0.0001f) return true;
        if (Time.time - _lastDragSnap < 0.05f) return true;

        _lastDragSnap = Time.time;
        try
        {
            me.NetTransform.RpcSnapTo(next);
        }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "MouseTools.SelfDrag: snap to cursor"); }
        return true;
    }

    private static Vector2 World(Camera cam, PlayerControl me)
    {
        Vector3 sp = Input.mousePosition;
        sp.z = me != null ? cam.WorldToScreenPoint(me.transform.position).z : -cam.transform.position.z;
        Vector3 w = cam.ScreenToWorldPoint(sp);
        return new Vector2(w.x, w.y);
    }

    private static void Pick(Camera cam)
    {
        Vector2 m = cam.ScreenToWorldPoint(Input.mousePosition);
        PlayerControl best = null;
        float bestD = 1.6f;
        var e = PlayerControl.AllPlayerControls.GetEnumerator();
        while (e.MoveNext())
        {
            PlayerControl p = e.Current;
            if (p == null || p.Data == null || p.Data.Disconnected) continue;
            float d = Vector2.Distance(p.transform.position, m);
            if (d < bestD)
            {
                bestD = d;
                best = p;
            }
        }

        if (best == null) return;
        if (best == _selected)
        {
            Clear();
            return;
        }
        Clear();
        _selected = best;
        Outlined(best, true);
    }

    private static void Resize()
    {
        if (_selected == null) return;
        float w = Input.mouseScrollDelta.y;
        if (Mathf.Abs(w) < 0.01f) return;
        Transform t = _selected.transform;
        float s = Mathf.Clamp(t.localScale.x + (w > 0f ? 0.15f : -0.15f), 0.25f, 2f);
        t.localScale = new Vector3(s, s, 1f);
    }

    internal static void Clear()
    {
        if (_selected == null) return;
        Outlined(_selected, false);
        _selected = null;
    }

    private static void Outlined(PlayerControl p, bool on)
    {
        try
        {
            CosmeticsLayer c = p != null ? p.cosmetics : null;
            if (c == null) return;
            if (on)
                c.SetOutline(true, new Il2CppSystem.Nullable<Color>(Outline));
            else
            {
                c.SetOutline(true, new Il2CppSystem.Nullable<Color>(Color.clear));
                c.SetOutline(false, (Il2CppSystem.Nullable<Color>)null);
            }
        }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "MouseTools.Outlined: toggle outline"); }
    }
}
