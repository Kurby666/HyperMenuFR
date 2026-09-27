using System;
using UnityEngine;

namespace MalumMenu.routines
{
    public class PlayerFollowerRoutine : IRoutine
    {
        private const int HandlingId = 60107;

        public PlayerFollowerRoutine() : base("PlayerFollower") { }

        public PlayerControl target;
        public bool moveable = true;

        public bool IsFollowing(PlayerControl player) => Enabled && target != null && target == player;

        public string Toggle(PlayerControl player)
        {
            if(player == null || player.Data == null)
                return "No target.";
            if(Enabled && target == player)
            {
                target = null;
                Enabled = false;
                return "Stopped.";
            }
            target = player;
            Enabled = true;
            return "Following: " + player.Data.PlayerName;
        }

        public static void GoTo(PlayerControl player)
        {
            try
            {
                PlayerControl me = PlayerControl.LocalPlayer;
                if(player == null || me == null || me.NetTransform == null)
                    return;
                me.NetTransform.RpcSnapTo(player.GetTruePosition());
            }
            catch (Exception ex)
            {
                ErrorReporter.Report(ex, HandlingId, "PlayerFollowerRoutine.GoTo: teleporting to player");
            }
        }

        public override void Run()
        {
            try
            {
                PlayerControl me = PlayerControl.LocalPlayer;
                if(target == null || me == null || me.MyPhysics == null || me.MyPhysics.body == null)
                {
                    if(target != null || Enabled)
                        MalumMenu.notifications.Send("Player Follower", "You are no longer following a player.", 10);
                    target = null;
                    Enabled = false;
                    return;
                }

                if(ShipStatus.Instance == null && LobbyBehaviour.Instance == null) return;
                if(MeetingHud.Instance != null || ExileController.Instance != null) return;
                if(me.inVent || !me.CanMove) return;
                if(target.Data == null || target.Data.Disconnected)
                {
                    MalumMenu.notifications.Send("Player Follower", "Follow target left.", 10);
                    target = null;
                    Enabled = false;
                    return;
                }

                Rigidbody2D body = me.MyPhysics.body;
                Vector2 mp = body.position;
                Vector2 tp = target.transform.position;
                Vector2 delta = tp - mp;
                float dist = delta.magnitude;
                if(dist <= 0.3f)
                {
                    body.velocity = Vector2.zero;
                    return;
                }

                Vector2 dir = delta / dist;
                float sp = me.MyPhysics.TrueSpeed;
                float speed = Mathf.Min(sp, dist * 4.5f + 0.4f);
                body.velocity = dir * speed;
                me.MyPhysics.FlipX = dir.x < 0f;
            }
            catch (Exception ex)
            {
                ErrorReporter.Report(ex, HandlingId, "PlayerFollowerRoutine.Run: following target");
            }
        }

        protected override void OnEnable()
        {
            try
            {
                if(PlayerControl.LocalPlayer == null)
                {
                    MalumMenu.notifications.Send("Player Follower", "Player Follower can only be used once the game has started.", 10);
                    Enabled = false;
                    return;
                }
            }
            catch (Exception ex)
            {
                ErrorReporter.Report(ex, HandlingId, "PlayerFollowerRoutine.OnEnable: validating game state");
            }
        }

        public override void OnDisconnect()
        {
            try
            {
                MalumMenu.notifications.Send("Player Follower", "Player Follower was disabled as you left the game.", 10);
                Enabled = false;
            }
            catch (Exception ex)
            {
                ErrorReporter.Report(ex, HandlingId, "PlayerFollowerRoutine.OnDisconnect: disabling routine");
            }
        }
    }
}
