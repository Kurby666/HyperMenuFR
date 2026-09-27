using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace MalumMenu.Cheats
{
	// Missing role buffs: kill aura, morph-into-dead, endless phantom invis,
	// judge overrule without tasks, detective no cooldown.
	// Ported from othermenu Cheats/HyperRoleBuffs.cs (BuffShiftPatch subset).
	internal static class RoleBuffs
	{
		private const int HandlingId = 20036;

		private static float _auraAt;
		private static float _phantomFull;
		private static readonly List<byte> _flippedDead = new List<byte>();

		internal static bool Alive(PlayerControl p) => p != null && p.Data != null && !p.Data.IsDead;

		internal static PlayerControl NearestKill(float max, bool anyone)
		{
			PlayerControl me = PlayerControl.LocalPlayer;
			if(!Alive(me)) return null;
			Vector2 p = me.GetTruePosition();
			PlayerControl best = null;
			float bd = max;
			try
			{
				foreach(PlayerControl t in PlayerControl.AllPlayerControls)
				{
					if(t == null || t == me || !Alive(t) || t.inVent || t.Data.Disconnected)
						continue;
					RoleBehaviour tr = t.Data.Role;
					if(!anyone && tr != null && (int)tr.TeamType == 1)
						continue;
					float d = Vector2.Distance(p, t.GetTruePosition());
					if(d < bd)
					{
						bd = d;
						best = t;
					}
				}
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "RoleBuffs.NearestKill: finding nearest kill target"); }
			return best;
		}

		internal static void AuraTick()
		{
			try
			{
				if(!CheatToggles.killAura) return;
				PlayerControl me = PlayerControl.LocalPlayer;
				if(me == null || ShipStatus.Instance == null) return;
				if(!Alive(me) || me.inVent || MeetingHud.Instance != null) return;
				RoleBehaviour r = me.Data.Role;
				if(r == null || !r.CanUseKillButton)
					return;
				if(Time.time < _auraAt) return;
				bool noCd = CheatToggles.noKillCd && Utils.isHost;
				if(me.killTimer > 0.05f && !noCd)
					return;

				PlayerControl t = NearestKill(Mathf.Max(0.5f, CheatToggles.killAuraDist), CheatToggles.killAnyone);
				if(t == null) return;
				_auraAt = Time.time + 0.15f;
				me.CmdCheckMurder(t);
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "RoleBuffs.AuraTick: running kill aura"); }
		}

		private static void RestoreFlippedDead()
		{
			try
			{
				if(_flippedDead.Count == 0)
					return;
				if(GameData.Instance != null && GameData.Instance.AllPlayers != null)
				{
					for(int i = 0; i < GameData.Instance.AllPlayers.Count; i++)
					{
						NetworkedPlayerInfo info = GameData.Instance.AllPlayers[i];
						if(info != null && _flippedDead.Contains(info.PlayerId))
							info.IsDead = true;
					}
				}
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "RoleBuffs.RestoreFlippedDead: restoring dead flags"); }
			_flippedDead.Clear();
		}

		[HarmonyPatch(typeof(ShapeshifterMinigame), nameof(ShapeshifterMinigame.Begin))]
		internal static class MorphDeadPatch
		{
			static void Prefix()
			{
				try
				{
					RestoreFlippedDead();
					if(!CheatToggles.morphDead || GameData.Instance == null)
						return;
					var all = GameData.Instance.AllPlayers;
					if(all == null) return;
					for(int i = 0; i < all.Count; i++)
					{
						NetworkedPlayerInfo info = all[i];
						if(info == null || info.Disconnected || !info.IsDead)
							continue;
						info.IsDead = false;
						_flippedDead.Add(info.PlayerId);
					}
				}
				catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "MorphDeadPatch.Prefix: exposing dead morph targets"); }
			}

			static void Postfix()
			{
				try { RestoreFlippedDead(); }
				catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "MorphDeadPatch.Postfix: restoring dead flags"); }
			}
		}

		[HarmonyPatch(typeof(PhantomRole), nameof(PhantomRole.FixedUpdate))]
		internal static class EndlessInvisPatch
		{
			static void Postfix(PhantomRole __instance)
			{
				try
				{
					if(__instance == null || __instance.Player != PlayerControl.LocalPlayer)
						return;
					if(__instance.isInvisible)
					{
						float left = __instance.durationSecondsRemaining;
						if(left > _phantomFull)
							_phantomFull = left;
						if(CheatToggles.endlessInvis && _phantomFull > 0f && left < _phantomFull)
							__instance.durationSecondsRemaining = _phantomFull;
					}
					else
						_phantomFull = 0f;
				}
				catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "EndlessInvisPatch.Postfix: extending phantom invisibility"); }
			}
		}

		[HarmonyPatch(typeof(JudgeRole), nameof(JudgeRole.IsBlockedByTasks))]
		internal static class JudgeNoTasksPatch
		{
			static void Postfix(JudgeRole __instance, ref bool __result)
			{
				try
				{
					if(!CheatToggles.judgeNoTasks || __instance == null)
						return;
					if(__instance.Player != PlayerControl.LocalPlayer)
						return;
					__result = false;
				}
				catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "JudgeNoTasksPatch.Postfix: unblocking judge overrule"); }
			}
		}

		[HarmonyPatch(typeof(DetectiveRole), nameof(DetectiveRole.FixedUpdate))]
		internal static class DetectiveNoCdPatch
		{
			static void Postfix(DetectiveRole __instance)
			{
				try
				{
					if(__instance == null || __instance.Player != PlayerControl.LocalPlayer)
						return;
					if(!CheatToggles.detNoCd || __instance.cooldownSecondsRemaining <= 0f)
						return;
					__instance.cooldownSecondsRemaining = 0f;
					HudManager hud = HudManager.Instance;
					if(hud != null && hud.AbilityButton != null)
						hud.AbilityButton.SetCooldownFill(0f);
				}
				catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "DetectiveNoCdPatch.Postfix: clearing detective cooldown"); }
			}
		}
	}
}
