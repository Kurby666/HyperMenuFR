using System;
using HarmonyLib;

namespace MalumMenu.Cheats
{
	// Outfit tools: steal anyone's look, keep 4 favorite outfits,
	// classic look, and force your look onto others (host).
	// Ported from othermenu Cheats/NocturneOutfits.cs using only
	// live-verified src APIs (Utilities.CopyPlayer batch pattern).
	internal static class OutfitTools
	{
		private const int HandlingId = 20051;

		internal class FavOutfit
		{
			public string Name = "";
			public int Color;
			public string Hat = "";
			public string Skin = "";
			public string Visor = "";
			public string Pet = "";
			public string Plate = "";
			public bool Captured;
		}

		internal static readonly FavOutfit[] Favorites = new FavOutfit[4]
		{
			new FavOutfit(), new FavOutfit(), new FavOutfit(), new FavOutfit()
		};

		private static void ApplyOutfit(PlayerControl dest, string name, int color, string hat, string skin, string visor, string pet, string plate, bool includeName, bool includeColor)
		{
			try
			{
				NetworkedPlayerInfo.PlayerOutfit outfit = dest.CurrentOutfit;
				bool hasAnticheat = Utilities.IsAnticheatPresent();

				Network.BatchedMessage batch = new Network.BatchedMessage();

				if(includeName && !hasAnticheat)
				{
					batch.QueueSetName(dest, name);
				}

				if(includeColor && (!hasAnticheat || AmongUsClient.Instance.AmHost))
				{
					batch.QueueSetColor(dest, (byte)color);
				}

				batch.QueueSetNameplateStr(dest, plate, ++outfit.NamePlateSequenceId);
				batch.QueueSetHatStr(dest, hat, ++outfit.HatSequenceId);
				batch.QueueSetVisorStr(dest, visor, ++outfit.VisorSequenceId);
				batch.QueueSetSkinStr(dest, skin, ++outfit.SkinSequenceId);
				batch.QueueSetPetStr(dest, pet, ++outfit.PetSequenceId);

				batch.FinishBatch();
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "OutfitTools.ApplyOutfit: applying outfit"); }
		}

		internal static string StealOutfit(PlayerControl victim)
		{
			try
			{
				if(victim == null || victim.Data == null) return "No target.";
				PlayerControl me = PlayerControl.LocalPlayer;
				if(me == null || me.Data == null) return "Not in game.";

				NetworkedPlayerInfo.PlayerOutfit o = victim.CurrentOutfit;
				if(o == null) return "No outfit to steal.";

				ApplyOutfit(me, o.PlayerName, o.ColorId, o.HatId, o.SkinId, o.VisorId, o.PetId, o.NamePlateId, true, true);
				return $"Stole {victim.Data.PlayerName}'s outfit.";
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "OutfitTools.StealOutfit: stealing outfit"); return "Failed."; }
		}

		internal static string ClassicLook()
		{
			try
			{
				PlayerControl me = PlayerControl.LocalPlayer;
				if(me == null || me.Data == null) return "Not in game.";

				NetworkedPlayerInfo.PlayerOutfit o = me.CurrentOutfit;
				ApplyOutfit(me, o.PlayerName, 0, "", "", "", "", o.NamePlateId, false, true);
				return "Classic look applied.";
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "OutfitTools.ClassicLook: applying classic look"); return "Failed."; }
		}

		internal static string SetOutfitOnTarget(PlayerControl target)
		{
			try
			{
				if(target == null || target.Data == null) return "No target.";
				if(!AmongUsClient.Instance.AmHost) return "Host only.";
				PlayerControl me = PlayerControl.LocalPlayer;
				if(me == null || me.Data == null) return "Not in game.";

				NetworkedPlayerInfo.PlayerOutfit o = me.CurrentOutfit;
				if(o == null) return "No outfit to force.";

				ApplyOutfit(target, o.PlayerName, o.ColorId, o.HatId, o.SkinId, o.VisorId, o.PetId, o.NamePlateId, true, true);
				return $"Forced your outfit on {target.Data.PlayerName}.";
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "OutfitTools.SetOutfitOnTarget: forcing outfit"); return "Failed."; }
		}

		internal static string CaptureFavorite(int slot)
		{
			try
			{
				if(slot < 0 || slot >= Favorites.Length) return "Bad slot.";
				PlayerControl me = PlayerControl.LocalPlayer;
				if(me == null || me.Data == null) return "Not in game.";

				NetworkedPlayerInfo.PlayerOutfit o = me.CurrentOutfit;
				if(o == null) return "No outfit to save.";

				FavOutfit f = Favorites[slot];
				f.Name = o.PlayerName;
				f.Color = o.ColorId;
				f.Hat = o.HatId;
				f.Skin = o.SkinId;
				f.Visor = o.VisorId;
				f.Pet = o.PetId;
				f.Plate = o.NamePlateId;
				f.Captured = true;
				return $"Saved outfit to slot {slot + 1}.";
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "OutfitTools.CaptureFavorite: saving favorite"); return "Failed."; }
		}

		internal static string ApplyFavorite(int slot)
		{
			try
			{
				if(slot < 0 || slot >= Favorites.Length) return "Bad slot.";
				FavOutfit f = Favorites[slot];
				if(!f.Captured) return $"Slot {slot + 1} is empty.";
				PlayerControl me = PlayerControl.LocalPlayer;
				if(me == null || me.Data == null) return "Not in game.";

				ApplyOutfit(me, f.Name, f.Color, f.Hat, f.Skin, f.Visor, f.Pet, f.Plate, true, true);
				return $"Applied outfit slot {slot + 1}.";
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "OutfitTools.ApplyFavorite: applying favorite"); return "Failed."; }
		}

		[HarmonyPatch(typeof(LobbyBehaviour), nameof(LobbyBehaviour.Start))]
		internal static class OutfitLobbyResetPatch
		{
			static void Postfix()
			{
				try
				{
					if(CheatToggles.outfitResetLobby)
						ApplyFavorite(CheatToggles.outfitFavSlot);
				}
				catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "OutfitLobbyResetPatch.Postfix: resetting outfit on lobby"); }
			}
		}

		[HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.Start))]
		internal static class OutfitMatchResetPatch
		{
			static void Postfix()
			{
				try
				{
					if(CheatToggles.outfitResetMatch)
						ApplyFavorite(CheatToggles.outfitFavSlot);
				}
				catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "OutfitMatchResetPatch.Postfix: resetting outfit on match"); }
			}
		}
	}
}
