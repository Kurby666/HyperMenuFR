using System;
using UnityEngine;

namespace MalumMenu.Cheats
{
	// Auto-tasks: finish own tasks one by one in the background.
	// Ported from othermenu Cheats/NocturneAutoTasks.cs.
	internal static class AutoTasks
	{
		private const int HandlingId = 20034;

		private static float _next;

		private static bool Ready()
		{
			try
			{
				if(ShipStatus.Instance == null) return false;
				if(MeetingHud.Instance != null || ExileController.Instance != null) return false;
				PlayerControl me = PlayerControl.LocalPlayer;
				if(me == null || me.Data == null || me.Data.IsDead || me.Data.Disconnected) return false;
				if(me.Data.Role != null && me.Data.Role.IsImpostor) return false;
				return me.myTasks != null;
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "AutoTasks.Ready: checking auto-task readiness"); return false; }
		}

		internal static int Left()
		{
			int n = 0;
			try
			{
				PlayerControl me = PlayerControl.LocalPlayer;
				if(me == null || me.myTasks == null) return 0;
				for(int i = 0; i < me.myTasks.Count; i++)
				{
					PlayerTask t = me.myTasks[i];
					if(t != null && !t.IsComplete)
						n++;
				}
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "AutoTasks.Left: counting incomplete tasks"); }
			return n;
		}

		internal static void Tick()
		{
			try
			{
				if(!CheatToggles.autoTasks || !Ready()) return;
				if(Time.time < _next) return;

				float gap = Mathf.Max(0.8f, CheatToggles.autoTasksDelay);
				_next = Time.time + gap + UnityEngine.Random.Range(0f, gap * 0.35f);

				PlayerControl me = PlayerControl.LocalPlayer;
				for(int i = 0; i < me.myTasks.Count; i++)
				{
					PlayerTask t = me.myTasks[i];
					if(t == null || t.IsComplete) continue;
					me.RpcCompleteTask(t.Id);
					return;
				}
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "AutoTasks.Tick: auto-completing task"); }
		}
	}
}
