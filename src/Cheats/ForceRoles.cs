using System;
using System.Collections.Generic;
using AmongUs.GameOptions;
using HarmonyLib;

namespace MalumMenu.Cheats
{
	// Per-player forced roles (host): stored picks are applied live via RpcSetRole
	// and re-applied at game start by replacing the role draft. Ported from
	// othermenu Host/NocturneForceRoles.cs (En-only; Utils.Host replaced with a
	// local host check).
	internal static class ForceRoles
	{
		private const int HandlingId = 20056;

		internal static readonly (string Name, RoleTypes Role)[] Roles =
		{
			("No force", (RoleTypes)255),
			("Crewmate", RoleTypes.Crewmate),
			("Impostor", RoleTypes.Impostor),
			("Scientist", RoleTypes.Scientist),
			("Engineer", RoleTypes.Engineer),
			("Guardian Angel", RoleTypes.GuardianAngel),
			("Shapeshifter", RoleTypes.Shapeshifter),
			("Phantom", RoleTypes.Phantom),
			("Tracker", RoleTypes.Tracker),
			("Noisemaker", RoleTypes.Noisemaker),
			("Detective", RoleTypes.Detective),
			("Judge", RoleTypes.Judge),
			("Viper", RoleTypes.Viper),
		};

		private static readonly Dictionary<byte, RoleTypes> _forced = new Dictionary<byte, RoleTypes>();

		internal static int Count => _forced.Count;

		internal static bool IsHost()
		{
			try { return AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost; }
			catch { return false; }
		}

		internal static string Name(int idx)
		{
			if(idx < 0 || idx >= Roles.Length) idx = 0;
			return Roles[idx].Name;
		}

		internal static int IndexOf(byte pid)
		{
			try
			{
				if(_forced.TryGetValue(pid, out RoleTypes r))
					for(int i = 1; i < Roles.Length; i++)
						if(Roles[i].Role == r) return i;
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "ForceRoles.IndexOf: looking up forced role"); }
			return 0;
		}

		internal static void Set(byte pid, int idx)
		{
			try
			{
				if(idx <= 0 || idx >= Roles.Length) _forced.Remove(pid);
				else _forced[pid] = Roles[idx].Role;
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "ForceRoles.Set: storing forced role"); }
		}

		internal static void Cycle(byte pid)
		{
			try
			{
				int next = (IndexOf(pid) + 1) % Roles.Length;
				if(next == 0) _forced.Remove(pid);
				else _forced[pid] = Roles[next].Role;
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "ForceRoles.Cycle: cycling forced role"); }
		}

		internal static void Clear()
		{
			try { _forced.Clear(); }
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "ForceRoles.Clear: clearing forced roles"); }
		}

		internal static bool ImpTeam(RoleTypes r) =>
			r == RoleTypes.Impostor || r == RoleTypes.Shapeshifter || r == RoleTypes.Phantom || r == RoleTypes.Viper;

		internal static void Assign(PlayerControl pc, RoleTypes role)
		{
			try { pc.RpcSetRole(role, true); }
			catch { try { pc.RpcSetRole(role); } catch { } }
		}

		internal static PlayerControl ById(byte pid)
		{
			foreach(PlayerControl p in PlayerControl.AllPlayerControls)
				if(p != null && p.PlayerId == pid) return p;
			return null;
		}

		internal static string ForceNow(byte pid)
		{
			try
			{
				if(!IsHost() || LobbyBehaviour.Instance == null) return "Host in lobby only.";
				PlayerControl pc = ById(pid);
				if(pc == null || pc.Data == null) return "No target.";
				RoleTypes role = _forced.TryGetValue(pid, out RoleTypes r) ? r : RoleTypes.Crewmate;
				EnsureRate(role);
				Assign(pc, role);
				Prime(pc);
				return "Forced " + role + ".";
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "ForceRoles.ForceNow: forcing role live"); return "Failed."; }
		}

		internal static bool Distribute()
		{
			try
			{
				if(_forced.Count == 0 || !IsHost()) return true;

				GameOptionsManager gom = GameOptionsManager.Instance;
				if(gom == null || gom.CurrentGameOptions == null) return true;
				GameManager gm = GameManager.Instance;
				if(gm == null || gm.LogicRoleSelection == null) return true;
				try { if(gm.IsHideAndSeek()) return true; }
				catch { return true; }

				IGameOptions opt = gom.CurrentGameOptions;
				LogicRoleSelection logic = gm.LogicRoleSelection;

				List<PlayerControl> players = new List<PlayerControl>();
				try
				{
					foreach(PlayerControl p in PlayerControl.AllPlayerControls)
						if(p != null && p.Data != null && !p.Data.Disconnected && !p.Data.IsDead && p.PlayerId < 100)
							players.Add(p);
				}
				catch { return true; }
				if(players.Count == 0) return true;

				List<PlayerControl> imps = new List<PlayerControl>();
				foreach(PlayerControl p in players)
					if(_forced.TryGetValue(p.PlayerId, out RoleTypes r) && ImpTeam(r)) imps.Add(p);

				int num;
				try { num = opt.GetInt(Int32OptionNames.NumImpostors); }
				catch { num = 1; }
				if(imps.Count > 0) num = imps.Count;
				else if(num >= players.Count) num = players.Count - 1;
				if(num < 1) num = 1;

				System.Random rng = new System.Random();
				while(imps.Count < num)
				{
					List<PlayerControl> pool = new List<PlayerControl>();
					foreach(PlayerControl p in players)
						if(!imps.Contains(p)) pool.Add(p);
					if(pool.Count == 0) break;
					imps.Add(pool[rng.Next(pool.Count)]);
				}

				Il2CppSystem.Collections.Generic.List<NetworkedPlayerInfo> impInfo = new Il2CppSystem.Collections.Generic.List<NetworkedPlayerInfo>();
				Il2CppSystem.Collections.Generic.List<NetworkedPlayerInfo> crewInfo = new Il2CppSystem.Collections.Generic.List<NetworkedPlayerInfo>();
				foreach(PlayerControl p in players)
				{
					if(imps.Contains(p)) impInfo.Add(p.Data);
					else crewInfo.Add(p.Data);
				}

				try
				{
					logic.AssignRolesForTeam(impInfo, opt, (RoleTeamTypes)1, int.MaxValue, new Il2CppSystem.Nullable<RoleTypes>());
					logic.AssignRolesForTeam(crewInfo, opt, (RoleTeamTypes)0, int.MaxValue, new Il2CppSystem.Nullable<RoleTypes>(RoleTypes.Crewmate));
				}
				catch { return true; }

				foreach(PlayerControl p in players)
				{
					if(!_forced.TryGetValue(p.PlayerId, out RoleTypes role)) continue;
					if(role == RoleTypes.Crewmate || role == RoleTypes.Impostor || (int)role == 255) continue;
					try { RoleManager.Instance.SetRole(p, role); } catch { }
					try { p.RpcSetRole(role, false); } catch { }
				}

				foreach(PlayerControl p in players)
					Refresh(p);
				return false;
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "ForceRoles.Distribute: distributing forced roles"); return true; }
		}

		private static void Refresh(PlayerControl p)
		{
			try
			{
				if(p == null || p.Data == null) return;
				if(p.Data.Role != null) p.Data.Role.Initialize(p);
				if(ImpTeam(p.Data.RoleType)) p.SetKillTimer(0f);
			}
			catch { }
		}

		internal static void EnsureRate(RoleTypes role)
		{
			try
			{
				if(role == RoleTypes.Crewmate || role == RoleTypes.Impostor || (int)role == 255) return;
				if(!LobbySettings.Ready() || LobbySettings.RoleNum(role) > 0) return;
				LobbySettings.SetRole(role, 1, 100);
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "ForceRoles.EnsureRate: ensuring role rate"); }
		}

		private static void Prime(PlayerControl pc)
		{
			try
			{
				if(pc == null || pc.Data == null || pc.Data.Role == null) return;
				pc.Data.Role.SetCooldown();
			}
			catch { }
		}
	}

	[HarmonyPatch(typeof(RoleManager), nameof(RoleManager.SelectRoles))]
	internal static class ForceRolesSelectPatch
	{
		public static bool Prefix()
		{
			try { return ForceRoles.Distribute(); }
			catch { return true; }
		}
	}

	[HarmonyPatch(typeof(LobbyBehaviour), nameof(LobbyBehaviour.Start))]
	internal static class ForceRolesResetPatch
	{
		public static void Postfix() => ForceRoles.Clear();
	}
}
