using System;
using AmongUs.GameOptions;
using HarmonyLib;
using Hazel;
using InnerNet;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MalumMenu;

public static class Invisibility
{
    private const int HandlingId = 20021;

    private static bool _was;

    public static void Tick()
    {
        try
        {
            bool on = CheatToggles.invisible;
            if (on != _was)
            {
                _was = on;

                if (CheatToggles.invisiblePoof)
                {
                    PlayerControl me = PlayerControl.LocalPlayer;
                    if (me != null)
                    {
                        if (on)
                            PhantomPoof.Vanish(me);
                        else
                            PhantomPoof.Appear(me);
                    }
                }
            }

            LobbyPhantom.Tick();
        }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "Invisibility.Tick: poof transitions and lobby phantom"); }
    }

    [HarmonyPatch(typeof(CustomNetworkTransform), nameof(CustomNetworkTransform.FixedUpdate))]
    private static class SuppressPosition
    {
        static bool Prefix(CustomNetworkTransform __instance)
        {
            try
            {
                if (!CheatToggles.invisible || __instance == null) return true;
                if (!((InnerNetObject)__instance).AmOwner || __instance.myPlayer != PlayerControl.LocalPlayer)
                    return true;
                if (MeetingHud.Instance != null) return true;

                ushort seq = (ushort)(__instance.lastSequenceId + 1);
                __instance.lastSequenceId = seq;
                MessageWriter w = AmongUsClient.Instance.StartRpcImmediately(((InnerNetObject)__instance).NetId, 21, SendOption.Reliable, -1);
                NetHelpers.WriteVector2(new Vector2(454f, 454f), w);
                w.Write(seq);
                AmongUsClient.Instance.FinishRpcImmediately(w);
            }
            catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "Invisibility.SuppressPosition.Prefix: spoof off-map position"); }
            return false;
        }
    }

    internal static class PhantomPoof
    {
        internal static void Vanish(PlayerControl pc)
        {
            try
            {
                RoleManager rm = RoleManager.Instance;
                if (rm != null) Play(rm.vanish_PoofAnim, pc);
            }
            catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "Invisibility.PhantomPoof.Vanish: play vanish poof"); }
        }

        internal static void Appear(PlayerControl pc)
        {
            try
            {
                RoleManager rm = RoleManager.Instance;
                if (rm != null) Play(rm.appear_PoofAnim, pc);
            }
            catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "Invisibility.PhantomPoof.Appear: play appear poof"); }
        }

        private static void Play(RoleEffectAnimation prefab, PlayerControl pc)
        {
            if (prefab == null || pc == null || pc.cosmetics == null)
                return;

            try
            {
                RoleEffectAnimation anim = Object.Instantiate(prefab, pc.transform);
                anim.SetMaterialColor(pc.CurrentOutfit != null ? pc.CurrentOutfit.ColorId : 0);
                anim.Play(pc, null, pc.cosmetics.FlipX, RoleEffectAnimation.SoundType.Local, 0f, true, 0f);
            }
            catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "Invisibility.PhantomPoof.Play: instantiate poof anim"); }
        }
    }

    public static class LobbyPhantom
    {
        private const float RoleWait = 0.6f;
        private const float BackWait = 0.5f;
        private const float GiveUp = 3f;

        private static RoleTypes _prev = RoleTypes.Crewmate;
        private static float _at;
        private static float _dead;
        private static int _step;

        public static bool IsPhantom(PlayerControl pc) =>
            pc != null && pc.Data != null && pc.Data.Role != null && pc.Data.Role.Role == RoleTypes.Phantom;

        private static RoleTypes RoleOf(PlayerControl pc) =>
            pc != null && pc.Data != null && pc.Data.Role != null ? pc.Data.Role.Role : RoleTypes.Crewmate;

        public static void Vanish()
        {
            try
            {
                if (!AmongUsClient.Instance.AmHost || LobbyBehaviour.Instance == null)
                {
                    MalumMenu.notifications.Send("Lobby Phantom", "Host-only and lobby-only.", 5);
                    return;
                }

                PlayerControl me = PlayerControl.LocalPlayer;
                if (me == null || me.Data == null) return;

                if (IsPhantom(me))
                {
                    Hide(me);
                    return;
                }

                _prev = RoleOf(me);
                me.RpcSetRole(RoleTypes.Phantom);

                _step = 1;
                _at = Time.unscaledTime + RoleWait;
                _dead = Time.unscaledTime + GiveUp;
            }
            catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "Invisibility.LobbyPhantom.Vanish: vanish in lobby"); }
        }

        public static void Appear()
        {
            try
            {
                PlayerControl me = PlayerControl.LocalPlayer;
                if (me == null) return;

                if (_step != 0)
                {
                    _step = 0;
                    Restore(me);
                }

                me.RpcAppear(true);
                me.SetRoleInvisibility(false, true, true);
            }
            catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "Invisibility.LobbyPhantom.Appear: appear in lobby"); }
        }

        public static void Tick()
        {
            try
            {
                if (_step == 0) return;

                float now = Time.unscaledTime;
                PlayerControl me = PlayerControl.LocalPlayer;
                if (me == null || me.Data == null)
                {
                    _step = 0;
                    return;
                }

                if (_step == 1)
                {
                    if (!IsPhantom(me))
                    {
                        if (now >= _dead) _step = 0;
                        return;
                    }
                    if (now < _at) return;

                    Hide(me);
                    _step = 2;
                    _at = now + BackWait;
                    return;
                }

                if (now < _at) return;
                _step = 0;
                Restore(me);
            }
            catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "Invisibility.LobbyPhantom.Tick: phantom role sequence"); }
        }

        private static void Hide(PlayerControl me)
        {
            try
            {
                me.RpcVanish();
                me.SetRoleInvisibility(true, true, true);
            }
            catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "Invisibility.LobbyPhantom.Hide: vanish RPCs"); }
        }

        private static void Restore(PlayerControl me)
        {
            try
            {
                if (RoleOf(me) == _prev) return;
                me.RpcSetRole(_prev);
            }
            catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "Invisibility.LobbyPhantom.Restore: restore previous role"); }
        }
    }
}
