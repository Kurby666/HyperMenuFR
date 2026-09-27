using System;
using System.Collections.Generic;
using UnityEngine;

namespace MalumMenu;

// Neon outline: client-side glow outline on every player, Rainbow / By-Role / By-Color.
// Port of othermenu Cheats/HyperNeonOutline.cs. othermenu pokes the body
// material's _Outline shader props; those renderer paths don't exist in this
// codebase, so this uses the verified CosmeticsLayer.SetOutline machinery
// (same calls as MouseTools.Outlined). Skips MouseTools.Selected so the blue
// selection outline is never fought over.
internal static class NeonOutline
{
    private const int HandlingId = 20042;

    private static readonly HashSet<byte> _lit = new HashSet<byte>();

    private static readonly Color RoleImp = new Color(1f, 0.22f, 0.22f);
    private static readonly Color RoleCrew = new Color(0.30f, 1f, 0.55f);

    internal static string ModeName()
    {
        return CheatToggles.neonMode switch
        {
            1 => "By-Role",
            2 => "By-Color",
            _ => "Rainbow",
        };
    }

    internal static void CycleMode()
    {
        CheatToggles.neonMode = (CheatToggles.neonMode + 1) % 3;
    }

    // Called every frame from PlayerPhysics_LateUpdate.
    internal static void Tick()
    {
        try
        {
            if (!CheatToggles.neonOutline)
            {
                if (_lit.Count > 0)
                    RestoreAll();
                return;
            }
            if ((ShipStatus.Instance == null && LobbyBehaviour.Instance == null)
                || PlayerControl.LocalPlayer == null)
            {
                if (_lit.Count > 0)
                    RestoreAll();
                return;
            }

            foreach (PlayerControl pc in PlayerControl.AllPlayerControls)
            {
                if (pc == null || pc.Data == null || pc.Data.Disconnected || pc.cosmetics == null)
                    continue;
                if (pc == MouseTools.Selected)
                    continue;
                Apply(pc);
            }
        }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "NeonOutline.Tick: applying neon outlines"); }
    }

    private static void Apply(PlayerControl pc)
    {
        try
        {
            pc.cosmetics.SetOutline(true, new Il2CppSystem.Nullable<Color>(ColorFor(pc)));
            _lit.Add(pc.PlayerId);
        }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "NeonOutline.Apply: setting outline"); }
    }

    internal static void RestoreAll()
    {
        try
        {
            foreach (PlayerControl pc in PlayerControl.AllPlayerControls)
            {
                if (pc == null || pc.cosmetics == null)
                    continue;
                if (!_lit.Contains(pc.PlayerId))
                    continue;
                try
                {
                    pc.cosmetics.SetOutline(true, new Il2CppSystem.Nullable<Color>(Color.clear));
                    pc.cosmetics.SetOutline(false, (Il2CppSystem.Nullable<Color>)null);
                }
                catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "NeonOutline.RestoreAll: clearing outline"); }
            }
        }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "NeonOutline.RestoreAll: restoring outlines"); }
        _lit.Clear();
    }

    private static Color ColorFor(PlayerControl pc)
    {
        try
        {
            int mode = CheatToggles.neonMode;
            if (mode == 2)
            {
                int cid = pc.CurrentOutfit != null ? pc.CurrentOutfit.ColorId : -1;
                if (cid >= 0 && cid < Palette.PlayerColors.Length)
                    return Palette.PlayerColors[cid];
                return Color.white;
            }
            if (mode == 1)
            {
                bool imp = pc.Data != null && pc.Data.Role != null && pc.Data.Role.IsImpostor;
                return imp ? RoleImp : RoleCrew;
            }
            return Color.HSVToRGB(Mathf.Repeat(Time.unscaledTime * 0.15f, 1f), 1f, 1f);
        }
        catch (Exception ex)
        {
            ErrorReporter.Report(ex, HandlingId, "NeonOutline.ColorFor: picking outline color");
            return Color.white;
        }
    }
}
