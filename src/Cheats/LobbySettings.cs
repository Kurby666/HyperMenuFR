using System;
using System.Globalization;
using System.Text;
using AmongUs.GameOptions;
using Il2CppInterop.Runtime.InteropTypes;
using InnerNet;
using UnityEngine;

namespace MalumMenu.Cheats
{
	// Live lobby-options editor (host, lobby only): reads/writes the current
	// IGameOptions in place and re-syncs to the lobby. Ported from othermenu
	// Host/HyperLobbySettings.cs (En-only; Tick driven from
	// RoutineManager.Update instead of a MonoBehaviour Update; the SyncPass
	// guard wrapper has no src equivalent so SyncOptions runs plain).
	internal static class LobbySettings
	{
		private const int HandlingId = 20055;

		private static bool _dirty;
		private static float _syncAt;

		private static bool _ready;
		private static int _readyFrame = -1;

		internal static bool Ready()
		{
			try
			{
				int f = Time.frameCount;
				if(_readyFrame == f) return _ready;
				_readyFrame = f;

				if(AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost)
					return _ready = false;
				InnerNetClient c = AmongUsClient.Instance;
				if(c.GameState != InnerNetClient.GameStates.Joined || c.IsGameStarted)
					return _ready = false;
				return _ready = GameOptionsManager.Instance != null && GameOptionsManager.Instance.CurrentGameOptions != null;
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "LobbySettings.Ready: checking host lobby state"); return _ready = false; }
		}

		private static IGameOptions _opt;
		private static IRoleOptionsCollection _roles;
		private static int _optFrame = -1;

		private static IGameOptions O
		{
			get
			{
				int f = Time.frameCount;
				if(_optFrame == f && _opt != null) return _opt;
				_optFrame = f;
				_roles = null;
				_opt = GameOptionsManager.Instance.CurrentGameOptions;
				return _opt;
			}
		}

		private static IRoleOptionsCollection Roles => _roles ??= O.RoleOptions;

		internal static int Map() { try { return O.MapId; } catch { return 0; } }
		internal static int Players() { try { return O.MaxPlayers; } catch { return 10; } }
		internal static int Imps() { try { return O.NumImpostors; } catch { return 1; } }
		internal static float KillCd() => GetF(FloatOptionNames.KillCooldown);
		internal static float Speed() => GetF(FloatOptionNames.PlayerSpeedMod);
		internal static float CrewVis() => GetF(FloatOptionNames.CrewLightMod);
		internal static float ImpVis() => GetF(FloatOptionNames.ImpostorLightMod);
		internal static int Meetings() => GetI(Int32OptionNames.NumEmergencyMeetings);
		internal static int MeetingCd() => GetI(Int32OptionNames.EmergencyCooldown);
		internal static int Discuss() => GetI(Int32OptionNames.DiscussionTime);
		internal static int Voting() => GetI(Int32OptionNames.VotingTime);
		internal static int Common() => GetI(Int32OptionNames.NumCommonTasks);
		internal static int Long() => GetI(Int32OptionNames.NumLongTasks);
		internal static int Short() => GetI(Int32OptionNames.NumShortTasks);
		internal static bool Anon() => GetB(BoolOptionNames.AnonymousVotes);
		internal static bool Confirm() => GetB(BoolOptionNames.ConfirmImpostor);
		internal static bool Visual() => GetB(BoolOptionNames.VisualTasks);
		internal static int KillDist() => GetI(Int32OptionNames.KillDistance);
		internal static int TaskBar()
		{
			try
			{
				NormalGameOptionsV11 n = ((Il2CppObjectBase)O).TryCast<NormalGameOptionsV11>();
				return n != null ? (int)n.TaskBarMode : 0;
			}
			catch { return 0; }
		}

		internal static void SetMap(int v) { try { O.SetByte(ByteOptionNames.MapId, (byte)Mathf.Clamp(v, 0, 5)); Touch(); } catch { } }
		internal static void SetPlayers(int v) => SetI(Int32OptionNames.MaxPlayers, v);
		internal static void SetImps(int v) => SetI(Int32OptionNames.NumImpostors, v);
		internal static void SetKillCd(float v) => SetF(FloatOptionNames.KillCooldown, v);
		internal static void SetSpeed(float v) => SetF(FloatOptionNames.PlayerSpeedMod, v);
		internal static void SetCrewVis(float v) => SetF(FloatOptionNames.CrewLightMod, v);
		internal static void SetImpVis(float v) => SetF(FloatOptionNames.ImpostorLightMod, v);
		internal static void SetMeetings(int v) => SetI(Int32OptionNames.NumEmergencyMeetings, v);
		internal static void SetMeetingCd(int v) => SetI(Int32OptionNames.EmergencyCooldown, v);
		internal static void SetDiscuss(int v) => SetI(Int32OptionNames.DiscussionTime, v);
		internal static void SetVoting(int v) => SetI(Int32OptionNames.VotingTime, v);
		internal static void SetCommon(int v) => SetI(Int32OptionNames.NumCommonTasks, v);
		internal static void SetLong(int v) => SetI(Int32OptionNames.NumLongTasks, v);
		internal static void SetShort(int v) => SetI(Int32OptionNames.NumShortTasks, v);
		internal static void SetAnon(bool v) => SetB(BoolOptionNames.AnonymousVotes, v);
		internal static void SetConfirm(bool v) => SetB(BoolOptionNames.ConfirmImpostor, v);
		internal static void SetVisual(bool v) => SetB(BoolOptionNames.VisualTasks, v);
		internal static void SetKillDist(int v) => SetI(Int32OptionNames.KillDistance, Mathf.Clamp(v, 0, 2));
		internal static void SetTaskBar(int v)
		{
			try
			{
				NormalGameOptionsV11 n = ((Il2CppObjectBase)O).TryCast<NormalGameOptionsV11>();
				if(n != null)
				{
					n.TaskBarMode = (AmongUs.GameOptions.TaskBarMode)Mathf.Clamp(v, 0, 2);
					Touch();
				}
			}
			catch { }
		}

		internal static int RoleNum(RoleTypes r) { try { return Roles.GetNumPerGame(r); } catch { return 0; } }
		internal static int RoleChance(RoleTypes r) { try { return Roles.GetChancePerGame(r); } catch { return 0; } }
		internal static void SetRole(RoleTypes r, int num, int chance)
		{
			try { Roles.SetRoleRate(r, num, chance); Touch(); }
			catch { }
		}

		private static T RoleOpt<T>(RoleTypes r) where T : Il2CppObjectBase
		{
			try
			{
				RoleOptionsCollectionV11 col = ((Il2CppObjectBase)O.RoleOptions).TryCast<RoleOptionsCollectionV11>();
				if(col != null && col.TryGetRoleOptions<T>(r, out T o)) return o;
			}
			catch { }
			return null;
		}

		internal static float SciCd() { ScientistRoleOptionsV11 o = RoleOpt<ScientistRoleOptionsV11>(RoleTypes.Scientist); return o != null ? o.ScientistCooldown : 0f; }
		internal static void SetSciCd(float v) { ScientistRoleOptionsV11 o = RoleOpt<ScientistRoleOptionsV11>(RoleTypes.Scientist); if(o != null) { o.ScientistCooldown = v; Touch(); } }
		internal static float SciBat() { ScientistRoleOptionsV11 o = RoleOpt<ScientistRoleOptionsV11>(RoleTypes.Scientist); return o != null ? o.ScientistBatteryCharge : 0f; }
		internal static void SetSciBat(float v) { ScientistRoleOptionsV11 o = RoleOpt<ScientistRoleOptionsV11>(RoleTypes.Scientist); if(o != null) { o.ScientistBatteryCharge = v; Touch(); } }

		internal static float EngCd() { EngineerRoleOptionsV11 o = RoleOpt<EngineerRoleOptionsV11>(RoleTypes.Engineer); return o != null ? o.EngineerCooldown : 0f; }
		internal static void SetEngCd(float v) { EngineerRoleOptionsV11 o = RoleOpt<EngineerRoleOptionsV11>(RoleTypes.Engineer); if(o != null) { o.EngineerCooldown = v; Touch(); } }
		internal static float EngVent() { EngineerRoleOptionsV11 o = RoleOpt<EngineerRoleOptionsV11>(RoleTypes.Engineer); return o != null ? o.EngineerInVentMaxTime : 0f; }
		internal static void SetEngVent(float v) { EngineerRoleOptionsV11 o = RoleOpt<EngineerRoleOptionsV11>(RoleTypes.Engineer); if(o != null) { o.EngineerInVentMaxTime = v; Touch(); } }

		internal static float GaCd() { GuardianAngelRoleOptionsV11 o = RoleOpt<GuardianAngelRoleOptionsV11>(RoleTypes.GuardianAngel); return o != null ? o.GuardianAngelCooldown : 0f; }
		internal static void SetGaCd(float v) { GuardianAngelRoleOptionsV11 o = RoleOpt<GuardianAngelRoleOptionsV11>(RoleTypes.GuardianAngel); if(o != null) { o.GuardianAngelCooldown = v; Touch(); } }
		internal static float GaDur() { GuardianAngelRoleOptionsV11 o = RoleOpt<GuardianAngelRoleOptionsV11>(RoleTypes.GuardianAngel); return o != null ? o.ProtectionDurationSeconds : 0f; }
		internal static void SetGaDur(float v) { GuardianAngelRoleOptionsV11 o = RoleOpt<GuardianAngelRoleOptionsV11>(RoleTypes.GuardianAngel); if(o != null) { o.ProtectionDurationSeconds = v; Touch(); } }
		internal static bool GaImpSee() { GuardianAngelRoleOptionsV11 o = RoleOpt<GuardianAngelRoleOptionsV11>(RoleTypes.GuardianAngel); return o != null && o.ImpostorsCanSeeProtect; }
		internal static void SetGaImpSee(bool v) { GuardianAngelRoleOptionsV11 o = RoleOpt<GuardianAngelRoleOptionsV11>(RoleTypes.GuardianAngel); if(o != null) { o.ImpostorsCanSeeProtect = v; Touch(); } }

		internal static float TrCd() { TrackerRoleOptionsV11 o = RoleOpt<TrackerRoleOptionsV11>(RoleTypes.Tracker); return o != null ? o.TrackerCooldown : 0f; }
		internal static void SetTrCd(float v) { TrackerRoleOptionsV11 o = RoleOpt<TrackerRoleOptionsV11>(RoleTypes.Tracker); if(o != null) { o.TrackerCooldown = v; Touch(); } }
		internal static float TrDur() { TrackerRoleOptionsV11 o = RoleOpt<TrackerRoleOptionsV11>(RoleTypes.Tracker); return o != null ? o.TrackerDuration : 0f; }
		internal static void SetTrDur(float v) { TrackerRoleOptionsV11 o = RoleOpt<TrackerRoleOptionsV11>(RoleTypes.Tracker); if(o != null) { o.TrackerDuration = v; Touch(); } }
		internal static float TrDelay() { TrackerRoleOptionsV11 o = RoleOpt<TrackerRoleOptionsV11>(RoleTypes.Tracker); return o != null ? o.TrackerDelay : 0f; }
		internal static void SetTrDelay(float v) { TrackerRoleOptionsV11 o = RoleOpt<TrackerRoleOptionsV11>(RoleTypes.Tracker); if(o != null) { o.TrackerDelay = v; Touch(); } }

		internal static float NmDur() { NoisemakerRoleOptionsV11 o = RoleOpt<NoisemakerRoleOptionsV11>(RoleTypes.Noisemaker); return o != null ? o.NoisemakerAlertDuration : 0f; }
		internal static void SetNmDur(float v) { NoisemakerRoleOptionsV11 o = RoleOpt<NoisemakerRoleOptionsV11>(RoleTypes.Noisemaker); if(o != null) { o.NoisemakerAlertDuration = v; Touch(); } }
		internal static bool NmImpAlert() { NoisemakerRoleOptionsV11 o = RoleOpt<NoisemakerRoleOptionsV11>(RoleTypes.Noisemaker); return o != null && o.NoisemakerImpostorAlert; }
		internal static void SetNmImpAlert(bool v) { NoisemakerRoleOptionsV11 o = RoleOpt<NoisemakerRoleOptionsV11>(RoleTypes.Noisemaker); if(o != null) { o.NoisemakerImpostorAlert = v; Touch(); } }

		internal static float DetLimit() { DetectiveRoleOptionsV11 o = RoleOpt<DetectiveRoleOptionsV11>(RoleTypes.Detective); return o != null ? o.DetectiveSuspectLimit : 0f; }
		internal static void SetDetLimit(float v) { DetectiveRoleOptionsV11 o = RoleOpt<DetectiveRoleOptionsV11>(RoleTypes.Detective); if(o != null) { o.DetectiveSuspectLimit = v; Touch(); } }

		internal static float SsCd() { ShapeshifterRoleOptionsV11 o = RoleOpt<ShapeshifterRoleOptionsV11>(RoleTypes.Shapeshifter); return o != null ? o.ShapeshifterCooldown : 0f; }
		internal static void SetSsCd(float v) { ShapeshifterRoleOptionsV11 o = RoleOpt<ShapeshifterRoleOptionsV11>(RoleTypes.Shapeshifter); if(o != null) { o.ShapeshifterCooldown = v; Touch(); } }
		internal static float SsDur() { ShapeshifterRoleOptionsV11 o = RoleOpt<ShapeshifterRoleOptionsV11>(RoleTypes.Shapeshifter); return o != null ? o.ShapeshifterDuration : 0f; }
		internal static void SetSsDur(float v) { ShapeshifterRoleOptionsV11 o = RoleOpt<ShapeshifterRoleOptionsV11>(RoleTypes.Shapeshifter); if(o != null) { o.ShapeshifterDuration = v; Touch(); } }
		internal static bool SsSkin() { ShapeshifterRoleOptionsV11 o = RoleOpt<ShapeshifterRoleOptionsV11>(RoleTypes.Shapeshifter); return o != null && o.ShapeshifterLeaveSkin; }
		internal static void SetSsSkin(bool v) { ShapeshifterRoleOptionsV11 o = RoleOpt<ShapeshifterRoleOptionsV11>(RoleTypes.Shapeshifter); if(o != null) { o.ShapeshifterLeaveSkin = v; Touch(); } }

		internal static float PhCd() { PhantomRoleOptionsV11 o = RoleOpt<PhantomRoleOptionsV11>(RoleTypes.Phantom); return o != null ? o.PhantomCooldown : 0f; }
		internal static void SetPhCd(float v) { PhantomRoleOptionsV11 o = RoleOpt<PhantomRoleOptionsV11>(RoleTypes.Phantom); if(o != null) { o.PhantomCooldown = v; Touch(); } }
		internal static float PhDur() { PhantomRoleOptionsV11 o = RoleOpt<PhantomRoleOptionsV11>(RoleTypes.Phantom); return o != null ? o.PhantomDuration : 0f; }
		internal static void SetPhDur(float v) { PhantomRoleOptionsV11 o = RoleOpt<PhantomRoleOptionsV11>(RoleTypes.Phantom); if(o != null) { o.PhantomDuration = v; Touch(); } }

		internal static float VpDis() { ViperRoleOptionsV11 o = RoleOpt<ViperRoleOptionsV11>(RoleTypes.Viper); return o != null ? o.ViperDissolveTime : 0f; }
		internal static void SetVpDis(float v) { ViperRoleOptionsV11 o = RoleOpt<ViperRoleOptionsV11>(RoleTypes.Viper); if(o != null) { o.ViperDissolveTime = v; Touch(); } }

		internal static float JudgeTaskPct() { JudgeRoleOptionsV11 o = RoleOpt<JudgeRoleOptionsV11>(RoleTypes.Judge); return o != null ? o.JudgeTaskRequirementPercentage : 0f; }
		internal static void SetJudgeTaskPct(float v) { JudgeRoleOptionsV11 o = RoleOpt<JudgeRoleOptionsV11>(RoleTypes.Judge); if(o != null) { o.JudgeTaskRequirementPercentage = Mathf.RoundToInt(v); Touch(); } }

		private static int GetI(Int32OptionNames k) { try { return O.GetInt(k); } catch { return 0; } }
		private static float GetF(FloatOptionNames k) { try { return O.GetFloat(k); } catch { return 0f; } }
		private static bool GetB(BoolOptionNames k) { try { return O.GetBool(k); } catch { return false; } }
		private static void SetI(Int32OptionNames k, int v) { try { O.SetInt(k, v); Touch(); } catch { } }
		private static void SetF(FloatOptionNames k, float v) { try { O.SetFloat(k, v); Touch(); } catch { } }
		private static void SetB(BoolOptionNames k, bool v) { try { O.SetBool(k, v); Touch(); } catch { } }

		private static void Touch()
		{
			try
			{
				GameOptionsManager mgr = GameOptionsManager.Instance;
				if(mgr != null && mgr.CurrentGameOptions != null)
					mgr.GameHostOptions = mgr.CurrentGameOptions;
				_dirty = true;
				_syncAt = Time.unscaledTime + 0.35f;
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "LobbySettings.Touch: marking options dirty"); }
		}

		internal static void Tick()
		{
			try
			{
				if(!_dirty || Time.unscaledTime < _syncAt) return;
				_dirty = false;
				if(!Ready()) return;
				if(GameManager.Instance != null && GameManager.Instance.LogicOptions != null)
					GameManager.Instance.LogicOptions.SyncOptions();
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "LobbySettings.Tick: syncing options"); }
		}

		internal static readonly RoleTypes[] RateRoles =
		{
			RoleTypes.Scientist, RoleTypes.Engineer, RoleTypes.GuardianAngel, RoleTypes.Tracker,
			RoleTypes.Noisemaker, RoleTypes.Detective, RoleTypes.Shapeshifter, RoleTypes.Phantom, RoleTypes.Viper,
		};

		internal static string Capture()
		{
			StringBuilder sb = new StringBuilder("v1");
			void N(float v) => sb.Append(';').Append(v.ToString("0.###", CultureInfo.InvariantCulture));

			N(Map());
			N(Players());
			N(Imps());
			N(KillCd());
			N(Speed());
			N(CrewVis());
			N(ImpVis());
			N(KillDist());
			N(TaskBar());
			N(Meetings());
			N(MeetingCd());
			N(Discuss());
			N(Voting());
			N(Anon() ? 1 : 0);
			N(Confirm() ? 1 : 0);
			N(Common());
			N(Long());
			N(Short());
			N(Visual() ? 1 : 0);
			foreach(RoleTypes r in RateRoles)
			{
				N(RoleNum(r));
				N(RoleChance(r));
			}
			N(SciCd());
			N(SciBat());
			N(EngCd());
			N(EngVent());
			N(GaCd());
			N(GaDur());
			N(GaImpSee() ? 1 : 0);
			N(TrCd());
			N(TrDur());
			N(TrDelay());
			N(NmDur());
			N(NmImpAlert() ? 1 : 0);
			N(DetLimit());
			N(SsCd());
			N(SsDur());
			N(SsSkin() ? 1 : 0);
			N(PhCd());
			N(PhDur());
			N(VpDis());
			return sb.ToString();
		}

		internal static bool ApplyState(string s)
		{
			try
			{
				if(string.IsNullOrEmpty(s)) return false;
				string[] p = s.Split(';');
				if(p.Length < 2 || p[0] != "v1") return false;
				int i = 1;
				float Next()
				{
					if(i >= p.Length) return 0f;
					float.TryParse(p[i++], NumberStyles.Float, CultureInfo.InvariantCulture, out float v);
					return v;
				}

				SetMap((int)Next());
				SetPlayers((int)Next());
				SetImps((int)Next());
				SetKillCd(Next());
				SetSpeed(Next());
				SetCrewVis(Next());
				SetImpVis(Next());
				SetKillDist((int)Next());
				SetTaskBar((int)Next());
				SetMeetings((int)Next());
				SetMeetingCd((int)Next());
				SetDiscuss((int)Next());
				SetVoting((int)Next());
				SetAnon(Next() > 0.5f);
				SetConfirm(Next() > 0.5f);
				SetCommon((int)Next());
				SetLong((int)Next());
				SetShort((int)Next());
				SetVisual(Next() > 0.5f);
				foreach(RoleTypes r in RateRoles)
					SetRole(r, (int)Next(), (int)Next());
				SetSciCd(Next());
				SetSciBat(Next());
				SetEngCd(Next());
				SetEngVent(Next());
				SetGaCd(Next());
				SetGaDur(Next());
				SetGaImpSee(Next() > 0.5f);
				SetTrCd(Next());
				SetTrDur(Next());
				SetTrDelay(Next());
				SetNmDur(Next());
				SetNmImpAlert(Next() > 0.5f);
				SetDetLimit(Next());
				SetSsCd(Next());
				SetSsDur(Next());
				SetSsSkin(Next() > 0.5f);
				SetPhCd(Next());
				SetPhDur(Next());
				SetVpDis(Next());
				return true;
			}
			catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "LobbySettings.ApplyState: applying preset"); return false; }
		}
	}
}
