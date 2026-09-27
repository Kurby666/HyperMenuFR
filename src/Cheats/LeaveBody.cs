using System;
using AmongUs.GameOptions;
using UnityEngine;

namespace MalumMenu.Cheats
{
	// Leave a body: murder yourself (host) to drop a real body where you stand,
	// then revive 0.4s later with your previous role restored.
	// Ported from othermenu Cheats/Corpses.cs.
	internal static class LeaveBody
	{
		private const int HandlingId = 20047;
		private const float ReviveGap = 0.4f;

		private static float _wakeAt;
		private static RoleTypes _prev;
		private static bool _hasPrev;

		internal static bool Busy => _wakeAt > 0f;

		internal static string Drop()
		{
			try
			{
				if(Busy) return "Already dropping a body.";
				if(!Utils.isHost) return "Host only.";
				if(Utils.isLobby || ShipStatus.Instance == null) return "In match only.";
				if(MeetingHud.Instance != null || ExileController.Instance != null) return "Not during a meeting.";

				PlayerControl me = PlayerControl.LocalPlayer;
				if(me == null || me.Data == null || me.Data.Disconnected || me.Data.IsDead) return "You must be alive.";

				_prev = me.Data.Role != null ? me.Data.Role.Role : RoleTypes.Crewmate;
				_hasPrev = true;

				me.RpcMurderPlayer(me, true);
				_wakeAt = Time.unscaledTime + ReviveGap;
				return null;
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "LeaveBody.Drop: dropping own body"); return "Failed."; }
		}

		internal static void Tick()
		{
			try
			{
				if(_wakeAt <= 0f || Time.unscaledTime < _wakeAt) return;
				_wakeAt = 0f;

				PlayerControl me = PlayerControl.LocalPlayer;
				if(me == null || me.Data == null) return;

				try
				{
					me.Revive();
				}
				catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "LeaveBody.Tick: reviving self"); }

				try
				{
					if(me.Data.IsDead)
					{
						me.Data.IsDead = false;
						me.Data.MarkDirty();
					}
				}
				catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "LeaveBody.Tick: clearing death flag"); }

				try
				{
					if(_hasPrev && (me.Data.Role == null || me.Data.Role.Role != _prev))
						me.RpcSetRole(_prev);
					_hasPrev = false;
				}
				catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "LeaveBody.Tick: restoring previous role"); }

				try
				{
					me.moveable = true;
					if(me.MyPhysics != null)
					{
						me.MyPhysics.ResetMoveState(true);
						me.MyPhysics.ResetAnimState();
					}
				}
				catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "LeaveBody.Tick: resetting move state"); }
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "LeaveBody.Tick: revive sequence"); }
		}
	}
}
