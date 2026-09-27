using System;
using UnityEngine;

namespace MalumMenu.Cheats
{
	// Task drain (Hide and Seek): resend task completions in bursts
	// to drain the crew timer.
	// Ported from othermenu Cheats/TaskDrain.cs.
	internal static class TaskDrain
	{
		private const int HandlingId = 20033;
		private const float BurstTime = 2f;
		private const float RestTime = 1f;

		private static float _nextSend;
		private static float _phaseUntil;
		private static bool _resting = true;

		internal static bool Running => CheatToggles.taskDrain && Ready();

		internal static void Tick()
		{
			try
			{
				if(!CheatToggles.taskDrain || !Ready())
				{
					_resting = true;
					_phaseUntil = 0f;
					return;
				}

				float now = Time.unscaledTime;
				if(now >= _phaseUntil)
				{
					_resting = !_resting;
					_phaseUntil = now + (_resting ? RestTime : BurstTime);
				}
				if(_resting || now < _nextSend) return;

				_nextSend = now + CheatToggles.taskDrainStep;
				Send();
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "TaskDrain.Tick: draining crew timer"); }
		}

		private static bool Ready()
		{
			try
			{
				if(ShipStatus.Instance == null) return false;
				if(GameManager.Instance == null || !GameManager.Instance.IsHideAndSeek()) return false;

				PlayerControl me = PlayerControl.LocalPlayer;
				return me != null && me.Data != null;
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "TaskDrain.Ready: checking drain readiness"); return false; }
		}

		private static void Send()
		{
			PlayerControl me = PlayerControl.LocalPlayer;
			if(me == null || me.myTasks == null) return;

			try
			{
				for(int i = 0; i < me.myTasks.Count; i++)
				{
					PlayerTask t = me.myTasks[i];
					if(t != null) me.RpcCompleteTask(t.Id);
				}
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "TaskDrain.Send: resending task completions"); }
		}
	}
}
