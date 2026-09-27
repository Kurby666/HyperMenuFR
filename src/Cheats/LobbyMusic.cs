using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MalumMenu.Cheats
{
	// Ported from othermenu/Patches/LobbyMusicPatches.cs (LobbyMusicMutePatch).
	// Silences the lobby music while leaving ambient/SFX alone: every AudioSource whose
	// name, clip name or parent-chain contains a music-ish keyword is muted and volume-zeroed,
	// and the original mute/volume is restored the moment the toggle goes off or the ship starts.
	internal static class LobbyMusicMute
	{
		internal const int HandlingId = 20070;

		private sealed class AudioState
		{
			public AudioSource Source;
			public float Volume;
			public bool Mute;
		}

		private const float ScanIntervalSeconds = 0.15f;
		private static readonly Dictionary<int, AudioState> mutedSources = new Dictionary<int, AudioState>();
		private static float nextScanAt;

		internal static void RefreshNow(LobbyBehaviour lobby, bool force = false)
		{
			try
			{
				if (lobby == null || !CheatToggles.muteLobbyMusic || !IsLobbyScene())
				{
					RestoreAll();
					return;
				}

				float now = Time.realtimeSinceStartup;
				if (!force && now < nextScanAt)
				{
					ReapplyMute();
					return;
				}

				nextScanAt = now + ScanIntervalSeconds;
				AudioSource[] sources = Object.FindObjectsOfType<AudioSource>(includeInactive: true);
				if (sources == null || sources.Length == 0)
				{
					return;
				}

				HashSet<int> activeIds = new HashSet<int>();
				for (int i = 0; i < sources.Length; i++)
				{
					AudioSource source = sources[i];
					if (!ShouldMuteLobbyAudio(source))
					{
						continue;
					}

					int instanceId = ((Object)source).GetInstanceID();
					activeIds.Add(instanceId);
					if (!mutedSources.ContainsKey(instanceId))
					{
						mutedSources[instanceId] = new AudioState
						{
							Source = source,
							Volume = source.volume,
							Mute = source.mute,
						};
					}

					ApplyMute(source);
				}

				TrimMutedSources(activeIds);
			}
			catch (Exception ex)
			{
				ErrorReporter.Report(ex, HandlingId, "LobbyMusicMute.RefreshNow: lobby music mute failed");
			}
		}

		internal static void RestoreAll()
		{
			if (mutedSources.Count == 0)
			{
				nextScanAt = 0f;
				return;
			}

			foreach (KeyValuePair<int, AudioState> pair in mutedSources)
			{
				AudioState state = pair.Value;
				AudioSource source = state?.Source;
				if (source == null)
				{
					continue;
				}

				source.mute = state.Mute;
				source.volume = state.Volume;
			}

			mutedSources.Clear();
			nextScanAt = 0f;
		}

		private static bool IsLobbyScene()
		{
			return LobbyBehaviour.Instance != null && ShipStatus.Instance == null;
		}

		private static void ReapplyMute()
		{
			if (mutedSources.Count == 0)
			{
				return;
			}

			foreach (KeyValuePair<int, AudioState> pair in mutedSources)
			{
				AudioSource source = pair.Value?.Source;
				if (source != null)
				{
					ApplyMute(source);
				}
			}
		}

		private static void ApplyMute(AudioSource source)
		{
			if (source == null)
			{
				return;
			}

			source.mute = true;
			source.volume = 0f;
		}

		private static bool ShouldMuteLobbyAudio(AudioSource source)
		{
			if (source == null)
			{
				return false;
			}

			string identity = AudioId(source);
			if (identity.Contains("ambient") || identity.Contains("ambience"))
			{
				return false;
			}

			return identity.Contains("music")
				|| identity.Contains("theme")
				|| identity.Contains("bgm")
				|| identity.Contains("song")
				|| identity.Contains("soundtrack")
				|| identity.Contains("title");
		}

		private static string AudioId(AudioSource source)
		{
			string sourceName = (((Object)source).name ?? string.Empty).ToLowerInvariant();
			string clipName = source.clip != null ? (((Object)source.clip).name ?? string.Empty).ToLowerInvariant() : string.Empty;
			string path = string.Empty;
			Transform transform = ((Component)source).transform;
			int depth = 0;
			while (transform != null && depth < 4)
			{
				string name = ((Object)transform).name;
				if (!string.IsNullOrWhiteSpace(name))
				{
					path += " " + name.ToLowerInvariant();
				}

				transform = transform.parent;
				depth++;
			}

			return sourceName + " " + clipName + path;
		}

		private static void TrimMutedSources(HashSet<int> activeIds)
		{
			if (mutedSources.Count == 0)
			{
				return;
			}

			List<int> staleIds = null;
			foreach (KeyValuePair<int, AudioState> pair in mutedSources)
			{
				AudioState state = pair.Value;
				AudioSource source = state?.Source;
				bool stillActive = activeIds != null && activeIds.Contains(pair.Key);
				if (source != null && stillActive)
				{
					continue;
				}

				if (source != null)
				{
					source.mute = state.Mute;
					source.volume = state.Volume;
				}

				staleIds ??= new List<int>();
				staleIds.Add(pair.Key);
			}

			if (staleIds == null)
			{
				return;
			}

			for (int i = 0; i < staleIds.Count; i++)
			{
				mutedSources.Remove(staleIds[i]);
			}
		}
	}

	[HarmonyPatch(typeof(LobbyBehaviour), nameof(LobbyBehaviour.Update))]
	internal static class LobbyMusic_UpdatePatch
	{
		public static void Postfix(LobbyBehaviour __instance)
		{
			LobbyMusicMute.RefreshNow(__instance);
		}
	}

	[HarmonyPatch(typeof(LobbyBehaviour), nameof(LobbyBehaviour.Start))]
	internal static class LobbyMusic_StartPatch
	{
		public static void Postfix(LobbyBehaviour __instance)
		{
			LobbyMusicMute.RefreshNow(__instance, true);
		}
	}

	[HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.Start))]
	internal static class LobbyMusic_ShipStartPatch
	{
		public static void Postfix()
		{
			LobbyMusicMute.RestoreAll();
		}
	}
}
