using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace MalumMenu.Cheats
{
	internal enum AnimLoop
	{
		ClimbUp, ClimbDown, EnterVent, ExitVent, Jump, Spawn
	}

	// Animation loops + one-shot effects (local-only fun).
	// Ported from othermenu Cheats/NocturneAnimations.cs.
	internal static class AnimLoops
	{
		private const int HandlingId = 20026;

		private static AudioClip _slam;
		private static AudioClip _eject;
		private static MushroomMixupScreenTint _tint;

		private static readonly Dictionary<AnimLoop, float> _loops = new Dictionary<AnimLoop, float>();
		private static readonly List<AnimLoop> _due = new List<AnimLoop>(6);

		private static PlayerControl Me => PlayerControl.LocalPlayer;

		private static PlayerAnimations Anim
		{
			get
			{
				PlayerControl me = Me;
				return me != null && me.MyPhysics != null ? me.MyPhysics.Animations : null;
			}
		}

		private static bool Flip
		{
			get
			{
				PlayerControl me = Me;
				return me != null && me.cosmetics != null && me.cosmetics.FlipX;
			}
		}

		internal static bool Active(AnimLoop a) => _loops.ContainsKey(a);
		internal static bool ClimbHeld => _loops.ContainsKey(AnimLoop.ClimbUp) || _loops.ContainsKey(AnimLoop.ClimbDown);
		internal static bool ClimbDownHeld => _loops.ContainsKey(AnimLoop.ClimbDown);

		internal static void Toggle(AnimLoop a)
		{
			try
			{
				if(_loops.Remove(a))
				{
					if(_loops.Count == 0)
						RestoreIdle();
					return;
				}
				if(a == AnimLoop.ClimbUp)
					_loops.Remove(AnimLoop.ClimbDown);
				else if(a == AnimLoop.ClimbDown)
					_loops.Remove(AnimLoop.ClimbUp);
				_loops[a] = 0f;
				Play(a);
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "AnimLoops.Toggle: toggling animation loop"); }
		}

		internal static void ResetAll()
		{
			try
			{
				_loops.Clear();
				RestoreIdle();
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "AnimLoops.ResetAll: resetting animation loops"); }
		}

		internal static void Tick()
		{
			try
			{
				if(_loops.Count == 0)
					return;
				float now = Time.time;
				_due.Clear();
				foreach(KeyValuePair<AnimLoop, float> kv in _loops)
					if(now >= kv.Value)
						_due.Add(kv.Key);
				for(int i = 0; i < _due.Count; i++)
				{
					AnimLoop a = _due[i];
					if(!_loops.ContainsKey(a)) continue;
					Play(a);
					_loops[a] = now + Interval(a);
				}
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "AnimLoops.Tick: ticking animation loops"); }
		}

		internal static void ForceClimb(PlayerAnimations a)
		{
			if(a == null)
				return;
			try
			{
				if(!a.IsPlayingClimbAnimation()) a.PlayClimbAnimation(ClimbDownHeld);
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "AnimLoops.ForceClimb: forcing climb animation"); }
		}

		private static float Interval(AnimLoop a)
		{
			switch(a)
			{
				case AnimLoop.Jump:
					return 1f;
				case AnimLoop.EnterVent:
				case AnimLoop.ExitVent:
				case AnimLoop.Spawn:
					return 1.5f;
				default:
					return 2f;
			}
		}

		private static void Play(AnimLoop a)
		{
			try
			{
				switch(a)
				{
					case AnimLoop.ClimbUp:
						RawClimb(false);
						break;
					case AnimLoop.ClimbDown:
						RawClimb(true);
						break;
					case AnimLoop.EnterVent:
						RawVent(true);
						break;
					case AnimLoop.ExitVent:
						RawVent(false);
						break;
					case AnimLoop.Jump:
						RawJump();
						break;
					case AnimLoop.Spawn:
						RawSpawn();
						break;
				}
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "AnimLoops.Play: playing animation"); }
		}

		private static void RestoreIdle()
		{
			PlayerAnimations a = Anim;
			if(a != null)
				try
				{
					a.PlayIdleAnimation();
				}
				catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "AnimLoops.RestoreIdle: restoring idle animation"); }
		}

		private static void RawClimb(bool down)
		{
			PlayerAnimations a = Anim;
			if(a != null)
				try
				{
					a.PlayClimbAnimation(down);
				}
				catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "AnimLoops.RawClimb: playing climb animation"); }
		}

		private static void RawVent(bool enter)
		{
			PlayerAnimations a = Anim;
			if(a != null)
				Run(enter ? a.CoPlayEnterVentAnimation(0) : a.CoPlayExitVentAnimation());
		}

		private static void RawJump()
		{
			PlayerAnimations a = Anim;
			if(a != null)
				Run(a.CoPlayJumpAnimation());
		}

		private static void RawSpawn()
		{
			PlayerAnimations a = Anim;
			if(a != null)
				Run(a.CoPlaySpawnAnimation(Flip));
		}

		private static void Run(Il2CppSystem.Collections.IEnumerator co)
		{
			PlayerControl me = Me;
			if(me == null || me.MyPhysics == null || co == null)
				return;
			try
			{
				me.MyPhysics.StartCoroutine(co);
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "AnimLoops.Run: running animation coroutine"); }
		}

		internal static void AlertFlash()
		{
			try
			{
				HudManager h = HudManager.Instance;
				if(h != null && h.AlertFlash != null && h.AlertFlash.animator != null)
					h.AlertFlash.animator.SetTrigger("OnFlash");
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "AnimLoops.AlertFlash: triggering alert flash"); }
		}

		internal static void MushroomIn()
		{
			MushroomMixupScreenTint t = Tint();
			if(t != null)
				try
				{
					t.Activate();
				}
				catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "AnimLoops.MushroomIn: activating mushroom tint"); }
		}

		internal static void MushroomOut()
		{
			MushroomMixupScreenTint t = Tint();
			if(t != null)
				try
				{
					t.Deactivate();
				}
				catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "AnimLoops.MushroomOut: deactivating mushroom tint"); }
		}

		internal static void MeetingSting()
		{
			AudioClip c = Slam();
			SoundManager s = SoundManager.Instance;
			if(c != null && s != null)
				try
				{
					s.PlaySound(c, false, 0.7f, (UnityEngine.Audio.AudioMixerGroup)null);
				}
				catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "AnimLoops.MeetingSting: playing meeting sting"); }
		}

		internal static void EjectSfx()
		{
			AudioClip c = Eject();
			SoundManager s = SoundManager.Instance;
			if(c != null && s != null)
				try
				{
					s.PlaySoundImmediate(c, false, 0.8f, 1f, (UnityEngine.Audio.AudioMixerGroup)null);
				}
				catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "AnimLoops.EjectSfx: playing eject sound"); }
		}

		private static MushroomMixupScreenTint Tint()
		{
			if(_tint != null) return _tint;
			try
			{
				HudManager h = HudManager.Instance;
				if(h != null)
				{
					MushroomMixupScreenTint t = h.GetComponentInChildren<MushroomMixupScreenTint>(true);
					if(t != null) return _tint = t;
				}
				var all = Resources.FindObjectsOfTypeAll(Il2CppType.Of<MushroomMixupScreenTint>());
				if(all != null)
					for(int i = 0; i < all.Length; i++)
					{
						MushroomMixupScreenTint t = all[i] != null ? all[i].TryCast<MushroomMixupScreenTint>() : null;
						if(t != null)
							return _tint = t;
					}
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "AnimLoops.Tint: finding mushroom tint"); }
			return null;
		}

		private static AudioClip Slam()
		{
			if(_slam != null)
				return _slam;
			try
			{
				var all = Resources.FindObjectsOfTypeAll(Il2CppType.Of<MeetingIntroAnimation>());
				if(all != null)
					for(int i = 0; i < all.Length; i++)
					{
						MeetingIntroAnimation m = all[i] != null ? all[i].TryCast<MeetingIntroAnimation>() : null;
						if(m != null && m.PlayerDeadSound != null)
							return _slam = m.PlayerDeadSound;
					}
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "AnimLoops.Slam: finding meeting sting"); }
			return null;
		}

		private static AudioClip Eject()
		{
			if(_eject != null) return _eject;
			try
			{
				var all = Resources.FindObjectsOfTypeAll(Il2CppType.Of<ExileController>());
				if(all != null)
					for(int i = 0; i < all.Length; i++)
					{
						ExileController x = all[i] != null ? all[i].TryCast<ExileController>() : null;
						if(x != null && x.TextSound != null)
							return _eject = x.TextSound;
					}
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "AnimLoops.Eject: finding eject sound"); }
			return null;
		}

		[HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
		internal static class AnimTickPatch
		{
			static void Postfix()
			{
				try
				{
					Tick();
				}
				catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "AnimTickPatch.Postfix: ticking animation loops"); }
			}
		}

		[HarmonyPatch(typeof(PlayerPhysics), nameof(PlayerPhysics.HandleAnimation))]
		internal static class AnimForcePatch
		{
			static void Postfix(PlayerPhysics __instance)
			{
				try
				{
					if(!ClimbHeld) return;
					if(__instance == null || __instance.myPlayer == null || __instance.myPlayer != PlayerControl.LocalPlayer)
						return;
					ForceClimb(__instance.Animations);
				}
				catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "AnimForcePatch.Postfix: forcing climb animation"); }
			}
		}
	}
}
