using System;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using InnerNet;
using TMPro;
using UnityEngine;

namespace MalumMenu.Cheats
{
	// Ported from othermenu/Patches/LobbyBrowserPatches.cs.
	// Expands the in-game lobby browser to 24 scrollable rows instead of the vanilla
	// handful, shows the real total lobby count, and filters the visible rows by host name.
	//
	// The per-row host/code/platform/age decoration is intentionally NOT ported: src already
	// does exactly that in Patches/OtherPatches.cs (GameContainer.SetupGameInfo postfix under
	// CheatToggles.seeLobbyInfo), and a second postfix would read the already-decorated text and
	// wrap it a second time.
	internal static class LobbyBrowser
	{
		internal const int HandlingId = 20069;

		private const string ExtendedLobbyScrollerName = "HyperExtendedLobbyScroller";
		private const int ExtendedLobbyRowTarget = 24;

		// Host-name filter. src has no string-valued config entries, so this follows the
		// established pattern for text (LobbyPresets.PresetName, CloneFont text) and is
		// edited from the menu with a TextField.
		internal static string SearchHost = string.Empty;

		private static int extendedLobbyScreenId;
		private static Scroller extendedLobbyScroller;

		internal static void RefreshLobbyTotal(FindAGameManager screen, HttpMatchmakerManager.FindGamesListFilteredResponse response)
		{
			if (!CheatToggles.richLobbyRows)
			{
				return;
			}

			((TMP_Text)screen.TotalText).text = response.Metadata.AllGamesCount.ToString();
		}

		internal static void EnsureBigBrowser(FindAGameManager screen)
		{
			if (!CheatToggles.richLobbyRows || screen == null || screen.gameContainers == null)
			{
				return;
			}

			int screenId = screen.GetInstanceID();
			if (extendedLobbyScreenId == screenId && extendedLobbyScroller != null)
			{
				return;
			}

			try
			{
				Il2CppReferenceArray<GameContainer> existingContainers = screen.gameContainers;
				int existingCount = existingContainers.Length;
				if (existingCount <= 0 || existingCount >= ExtendedLobbyRowTarget)
				{
					extendedLobbyScreenId = screenId;
					return;
				}

				GameContainer template = existingContainers[0];
				if (template == null)
				{
					return;
				}

				Transform rowParent = ((Component)template).transform.parent;
				if (rowParent == null)
				{
					return;
				}

				Transform oldScroller = rowParent.FindChild(ExtendedLobbyScrollerName);
				if (oldScroller != null && ((Component)oldScroller).GetComponent<Scroller>() != null)
				{
					extendedLobbyScroller = ((Component)oldScroller).GetComponent<Scroller>();
					extendedLobbyScreenId = screenId;
					return;
				}

				float rowSpacing = DetectLobbyRowSpacing(existingContainers);
				GameObject scrollerObject = new GameObject(ExtendedLobbyScrollerName);
				scrollerObject.transform.SetParent(rowParent, false);
				scrollerObject.transform.localPosition = Vector3.zero;
				scrollerObject.transform.localScale = Vector3.one;

				Scroller scroller = scrollerObject.AddComponent<Scroller>();
				scroller.Inner = scrollerObject.transform;
				scroller.MouseMustBeOverToScroll = true;
				scroller.allowY = true;
				scroller.ScrollWheelSpeed = 0.38f;
				scroller.SetYBoundsMin(0f);
				scroller.SetYBoundsMax(Mathf.Max(0f, (ExtendedLobbyRowTarget - existingCount) * rowSpacing));

				BoxCollider2D clickMask = ((Component)rowParent).GetComponent<BoxCollider2D>();
				if (clickMask == null)
				{
					clickMask = ((Component)rowParent).gameObject.AddComponent<BoxCollider2D>();
				}

				clickMask.size = new Vector2(16f, 12f);
				((PassiveUiElement)scroller).ClickMask = clickMask;

				GameContainer[] expanded = new GameContainer[ExtendedLobbyRowTarget];
				Vector3 firstLocalPosition = ((Component)template).transform.localPosition;
				for (int i = 0; i < existingCount; i++)
				{
					GameContainer container = existingContainers[i];
					if (container == null)
					{
						continue;
					}

					Transform transform = ((Component)container).transform;
					transform.SetParent(scrollerObject.transform, true);
					Vector3 position = transform.localPosition;
					position.z = 25f;
					transform.localPosition = position;
					expanded[i] = container;
				}

				for (int i = existingCount; i < expanded.Length; i++)
				{
					GameContainer clone = UnityEngine.Object.Instantiate(template, scrollerObject.transform);
					Transform cloneTransform = ((Component)clone).transform;
					cloneTransform.localPosition = new Vector3(firstLocalPosition.x, firstLocalPosition.y - (rowSpacing * i), 25f);
					cloneTransform.localScale = ((Component)template).transform.localScale;
					expanded[i] = clone;
				}

				screen.gameContainers = new Il2CppReferenceArray<GameContainer>(expanded);
				extendedLobbyScroller = scroller;
				extendedLobbyScreenId = screenId;
			}
			catch (Exception ex)
			{
				ErrorReporter.Report(ex, HandlingId, "LobbyBrowser.EnsureBigBrowser: extended lobby browser setup failed");
			}
		}

		internal static void ResetExtendedLobbyBrowserScroll()
		{
			try
			{
				if (extendedLobbyScroller != null)
				{
					extendedLobbyScroller.ScrollRelative(new Vector2(0f, -100f));
				}
			}
			catch { }
		}

		internal static void ApplyHostFilter(FindAGameManager screen)
		{
			string q = (SearchHost ?? string.Empty).Trim();
			if (q.Length == 0 || screen == null || screen.gameContainers == null)
			{
				return;
			}

			try
			{
				float spacing = DetectLobbyRowSpacing(screen.gameContainers);
				float baseY = ((Component)screen.gameContainers[0]).transform.localPosition.y;
				int shown = 0;

				for (int i = 0; i < screen.gameContainers.Length; i++)
				{
					GameContainer row = screen.gameContainers[i];
					if (row == null)
					{
						continue;
					}

					GameObject go = ((Component)row).gameObject;
					if (!go.activeSelf)
					{
						continue;
					}

					GameListing listing = row.gameListing;
					string host = listing != null ? (listing.TrueHostName ?? string.Empty) : string.Empty;
					if (host.IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0)
					{
						go.SetActive(false);
						continue;
					}

					Transform t = ((Component)row).transform;
					Vector3 p = t.localPosition;
					t.localPosition = new Vector3(p.x, baseY - shown * spacing, p.z);
					shown++;
				}
			}
			catch { }
		}

		private static float DetectLobbyRowSpacing(Il2CppReferenceArray<GameContainer> containers)
		{
			try
			{
				if (containers != null && containers.Length > 1 && containers[0] != null && containers[1] != null)
				{
					float firstY = ((Component)containers[0]).transform.localPosition.y;
					float secondY = ((Component)containers[1]).transform.localPosition.y;
					float detected = Mathf.Abs(firstY - secondY);
					if (detected > 0.05f)
					{
						return detected;
					}
				}
			}
			catch { }

			return 0.75f;
		}
	}

	[HarmonyPatch(typeof(FindAGameManager), nameof(FindAGameManager.Start))]
	internal static class LobbyBrowser_StartPatch
	{
		public static void Prefix(FindAGameManager __instance)
		{
			try
			{
				LobbyBrowser.EnsureBigBrowser(__instance);
			}
			catch (Exception ex) { ErrorReporter.Report(ex, LobbyBrowser.HandlingId, "LobbyBrowser_StartPatch.Prefix: extended lobby browser"); }
		}
	}

	[HarmonyPatch(typeof(FindAGameManager), nameof(FindAGameManager.HandleList))]
	internal static class LobbyBrowser_HandleListPatch
	{
		public static void Postfix(HttpMatchmakerManager.FindGamesListFilteredResponse response, FindAGameManager __instance)
		{
			try
			{
				LobbyBrowser.RefreshLobbyTotal(__instance, response);
				LobbyBrowser.ApplyHostFilter(__instance);
			}
			catch (Exception ex) { ErrorReporter.Report(ex, LobbyBrowser.HandlingId, "LobbyBrowser_HandleListPatch.Postfix: lobby total + host filter"); }
		}
	}

	[HarmonyPatch(typeof(FindAGameManager), nameof(FindAGameManager.RefreshList))]
	internal static class LobbyBrowser_RefreshListPatch
	{
		public static void Postfix()
		{
			LobbyBrowser.ResetExtendedLobbyBrowserScroll();
		}
	}
}
