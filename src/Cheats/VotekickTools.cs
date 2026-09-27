using System;
using System.Collections.Generic;
using HarmonyLib;
using InnerNet;
using UnityEngine;

namespace MalumMenu;

public static class VotekickTools
{
    private const int HandlingId = 20016;

    private enum Phase
    {
        Off, Room, Voted, Left, Rejoin, Final
    }

    private const float Settle = 0.4f;
    private const float LeaveMin = 1.1f;
    private const float LeaveMax = 1.5f;
    private const float StableHold = 0.5f;
    private const float RejoinDelay = 1.5f;
    private const float RejoinTimeout = 22f;
    private const float ManualTimeout = 180f;
    private const float FinalDelay = 1.5f;
    private const float RapidStep = 0.12f;
    private const float PulseStep = 0.3f;
    private const int SweepPasses = 3;
    private const int Cycles = 2;
    private const float AutoInterval = 3f;

    private static Phase _phase = Phase.Off;
    private static int _code;
    private static int _cyclesDone;
    private static float _at;
    private static float _pulseAt;
    private static float _votedStart;
    private static int _votedCount;
    private static float _votedStableAt;
    private static bool _swept;

    private static readonly List<byte> _queue = new();
    private static float _rapidAt;
    private static int _passesLeft;

    private static readonly HashSet<byte> _targets = new();
    private static bool _autoOn;
    private static float _autoAt;
    private static float _trackAt;

    internal static bool Armed => _phase != Phase.Off;
    internal static int TargetCount => _targets.Count;
    internal static bool AutoTargeting => _autoOn;
    internal static bool IsTarget(byte id) => _targets.Contains(id);

    internal static void ToggleTarget(byte id)
    {
        try
        {
            if (!_targets.Remove(id)) _targets.Add(id);
            if (_targets.Count == 0) _autoOn = false;
        }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "VotekickTools.ToggleTarget: toggle vote target"); }
    }

    internal static void ClearTargets()
    {
        try
        {
            _targets.Clear();
            _autoOn = false;
            MalumMenu.notifications.Send("Votekick", "Target list cleared.", 3);
        }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "VotekickTools.ClearTargets: clear vote targets"); }
    }

    internal static void ToggleHostTarget()
    {
        try
        {
            PlayerControl host = HostPlayer();
            if (host == null)
            {
                MalumMenu.notifications.Send("Votekick", "Host not found.", 3);
                return;
            }
            if (host == PlayerControl.LocalPlayer)
            {
                MalumMenu.notifications.Send("Votekick", "You are the host.", 3);
                return;
            }
            ToggleTarget(host.PlayerId);
            string name = host.Data != null && !string.IsNullOrEmpty(host.Data.PlayerName) ? host.Data.PlayerName : "?";
            MalumMenu.notifications.Send("Votekick", _targets.Contains(host.PlayerId) ? $"Marked as target: {name}" : $"Unmarked: {name}", 3);
        }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "VotekickTools.ToggleHostTarget: mark host as target"); }
    }

    internal static void ToggleTargetAuto()
    {
        try
        {
            if (_autoOn)
            {
                _autoOn = false;
                MalumMenu.notifications.Send("Votekick", "Auto-targets off.", 3);
                return;
            }
            if (_targets.Count == 0)
            {
                MalumMenu.notifications.Send("Votekick", "Mark targets first.", 3);
                return;
            }
            _autoOn = true;
            _autoAt = Time.unscaledTime;
            MalumMenu.notifications.Send("Votekick", $"Voting marked targets: {_targets.Count}", 3);
        }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "VotekickTools.ToggleTargetAuto: toggle auto vote loop"); }
    }

    internal static void ToggleAuto()
    {
        try
        {
            if (_phase != Phase.Off)
            {
                Stop("Auto-votekick off.");
                return;
            }
            _cyclesDone = 0;
            _phase = Phase.Room;
            _at = Time.unscaledTime + Settle;
            MalumMenu.notifications.Send("Votekick", "Armed: voting, will auto-rejoin if enabled.", 3);
        }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "VotekickTools.ToggleAuto: arm auto-votekick cycle"); }
    }

    internal static void VoteAllStay()
    {
        try
        {
            int sent = VoteAll(false);
            MalumMenu.notifications.Send("Votekick", sent > 0 ? $"Votes sent: {sent}. Staying." : "No targets or vote system not ready.", 3);
        }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "VotekickTools.VoteAllStay: vote all targets"); }
    }

    internal static void RapidAll()
    {
        try
        {
            StartRapid();
            int count = _queue.Count;
            MalumMenu.notifications.Send("Votekick", count > 0 ? $"Voting each in turn x{SweepPasses}: {count} targets." : "No targets.", 3);
        }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "VotekickTools.RapidAll: sweep votes player by player"); }
    }

    internal static void VoteHost()
    {
        try
        {
            PlayerControl host = HostPlayer();
            if (host == null || host == PlayerControl.LocalPlayer || host.Data == null)
            {
                MalumMenu.notifications.Send("Votekick", "Host not found or it's you.", 3);
                return;
            }
            int sent = 0;
            for (int i = 0; i < 3; i++)
                if (SendVote(host.Data.ClientId)) sent++;
            string name = !string.IsNullOrEmpty(host.Data.PlayerName) ? host.Data.PlayerName : "?";
            MalumMenu.notifications.Send("Votekick", sent > 0 ? $"Votes on {name} sent." : "Failed.", 3);
        }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "VotekickTools.VoteHost: vote the host"); }
    }

    internal static void VoteOne(PlayerControl player)
    {
        try
        {
            if (player == null || player.Data == null) return;
            if (SendVote(player.Data.ClientId))
            {
                string name = string.IsNullOrEmpty(player.Data.PlayerName) ? "?" : player.Data.PlayerName;
                MalumMenu.notifications.Send("Votekick", $"Vote sent on {name}.", 3);
            }
        }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "VotekickTools.VoteOne: vote single player"); }
    }

    internal static void RejoinLast()
    {
        try
        {
            if (InRoom())
            {
                MalumMenu.notifications.Send("Votekick", "You're already in a game.", 3);
                return;
            }
            if (_code == 0)
            {
                MalumMenu.notifications.Send("Votekick", "No saved lobby code.", 3);
                return;
            }
            Rejoin(_code);
            MalumMenu.notifications.Send("Votekick", $"Joining {GameCode.IntToGameName(_code)}", 3);
        }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "VotekickTools.RejoinLast: rejoin last lobby"); }
    }

    internal static void Tick()
    {
        try
        {
            TrackCode();
            TickAuto();
            TickRapid();
            if (_phase == Phase.Off) return;

            switch (_phase)
            {
                case Phase.Room: TickRoom(); break;
                case Phase.Voted: TickVoted(); break;
                case Phase.Left: TickLeft(); break;
                case Phase.Rejoin: TickRejoin(); break;
                case Phase.Final: TickFinal(); break;
            }
        }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "VotekickTools.Tick: run votekick state machine"); }
    }

    private static void TickAuto()
    {
        if (!_autoOn) return;
        if (_targets.Count == 0)
        {
            _autoOn = false;
            return;
        }
        if (Time.unscaledTime < _autoAt) return;
        _autoAt = Time.unscaledTime + AutoInterval;
        VoteTargets();
    }

    private static void TickRoom()
    {
        if (!InRoom()) return;
        if (Time.unscaledTime < _at) return;

        bool auto = CheatToggles.votekickAutoRejoin;
        SaveCode(!auto);

        if (_cyclesDone >= Cycles)
        {
            _swept = false;
            _phase = Phase.Final;
            _at = Time.unscaledTime + FinalDelay;
            MalumMenu.notifications.Send("Votekick", "Final sweep incoming.", 3);
            return;
        }

        int sent = VoteAll(false);
        MalumMenu.notifications.Send("Votekick", $"Round {_cyclesDone + 1}: votes sent {sent}. Leaving.", 3);
        float now = Time.unscaledTime;
        _phase = Phase.Voted;
        _votedStart = now;
        _pulseAt = now + PulseStep;
        _votedCount = -1;
        _votedStableAt = now + StableHold;
    }

    private static void TickVoted()
    {
        float now = Time.unscaledTime;
        if (now >= _pulseAt)
        {
            _pulseAt = now + PulseStep;
            VoteAll(true);
        }

        int count = CountTargets();
        if (count != _votedCount)
        {
            _votedCount = count;
            _votedStableAt = now + StableHold;
        }

        float since = now - _votedStart;
        bool ready = since >= LeaveMin && now >= _votedStableAt;
        if (!ready && since < LeaveMax) return;

        Leave();
        _phase = Phase.Left;
        _at = now + RejoinDelay;
    }

    private static void TickLeft()
    {
        if (InRoom()) return;
        if (Time.unscaledTime < _at) return;

        if (CheatToggles.votekickAutoRejoin)
        {
            Rejoin(_code);
            _at = Time.unscaledTime + RejoinTimeout;
        }
        else
        {
            _at = Time.unscaledTime + ManualTimeout;
            MalumMenu.notifications.Send("Votekick", "Paste the code and rejoin, I'll continue.", 5);
        }
        _phase = Phase.Rejoin;
    }

    private static void TickRejoin()
    {
        if (InRoom())
        {
            _cyclesDone++;
            _phase = Phase.Room;
            _at = Time.unscaledTime + Settle;
            MalumMenu.notifications.Send("Votekick", $"Rejoined, round {_cyclesDone + 1}.", 3);
            return;
        }
        if (Time.unscaledTime >= _at)
        {
            SaveCode(true);
            Stop(CheatToggles.votekickAutoRejoin ? "Auto-join failed, code copied, rejoin manually." : "No rejoin, cancelled.");
        }
    }

    private static void TickFinal()
    {
        if (_swept) return;
        if (Time.unscaledTime < _at) return;
        StartRapid();
        _swept = true;
    }

    private static void TickRapid()
    {
        if (_queue.Count == 0)
        {
            if (_passesLeft > 0)
            {
                _passesLeft--;
                FillQueue();
                return;
            }
            if (_phase == Phase.Final && _swept)
                Stop("Votekick sweep done.");
            return;
        }
        if (Time.unscaledTime < _rapidAt) return;
        _rapidAt = Time.unscaledTime + RapidStep;

        byte id = _queue[0];
        _queue.RemoveAt(0);
        PlayerControl player = ById(id);
        if (player != null && player.Data != null) SendVote(player.Data.ClientId);
    }

    private static void Stop(string why)
    {
        _phase = Phase.Off;
        _swept = false;
        _passesLeft = 0;
        _queue.Clear();
        MalumMenu.notifications.Send("Votekick", why, 3);
    }

    private static void StartRapid()
    {
        _passesLeft = SweepPasses - 1;
        FillQueue();
        _rapidAt = Time.unscaledTime;
    }

    private static void FillQueue()
    {
        _queue.Clear();
        try
        {
            foreach (PlayerControl player in PlayerControl.AllPlayerControls)
                if (player != null && !player.AmOwner && player.Data != null && !player.Data.Disconnected && IsSel(player))
                    _queue.Add(player.PlayerId);
        }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "VotekickTools.FillQueue: build sweep queue"); }
    }

    private static bool IsSel(PlayerControl player) => _targets.Count == 0 || _targets.Contains(player.PlayerId);

    private static int VoteAll(bool once)
    {
        if (VoteBanSystem.Instance == null || PlayerControl.AllPlayerControls == null) return 0;
        int sent = 0;
        try
        {
            foreach (PlayerControl player in PlayerControl.AllPlayerControls)
            {
                if (player == null || player.AmOwner || player.Data == null || player.Data.Disconnected || !IsSel(player)) continue;
                int reps = once ? 1 : 3;
                for (int i = 0; i < reps; i++)
                    if (SendVote(player.Data.ClientId)) sent++;
            }
        }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "VotekickTools.VoteAll: vote all selected"); }
        return sent;
    }

    private static int VoteTargets()
    {
        if (VoteBanSystem.Instance == null || _targets.Count == 0) return 0;
        int sent = 0;
        try
        {
            foreach (byte id in _targets)
            {
                PlayerControl player = ById(id);
                if (player == null || player.AmOwner || player.Data == null || player.Data.Disconnected) continue;
                if (SendVote(player.Data.ClientId)) sent++;
            }
        }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "VotekickTools.VoteTargets: vote marked targets"); }
        return sent;
    }

    private static int CountTargets()
    {
        int count = 0;
        try
        {
            foreach (PlayerControl player in PlayerControl.AllPlayerControls)
                if (player != null && !player.AmOwner && player.Data != null && !player.Data.Disconnected && IsSel(player))
                    count++;
        }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "VotekickTools.CountTargets: count remaining targets"); }
        return count;
    }

    private static bool SendVote(int clientId)
    {
        if (clientId < 0 || VoteBanSystem.Instance == null) return false;
        try
        {
            if (Utils.isHost)
                VoteBanSystem.Instance.AddVote(AmongUsClient.Instance.ClientId, clientId);
            else
                VoteBanSystem.Instance.CmdAddVote(clientId);
            return true;
        }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "VotekickTools.SendVote: send kick vote"); return false; }
    }

    private static PlayerControl ById(byte id)
    {
        foreach (PlayerControl player in PlayerControl.AllPlayerControls)
            if (player != null && player.PlayerId == id) return player;
        return null;
    }

    private static PlayerControl HostPlayer()
    {
        try
        {
            AmongUsClient client = AmongUsClient.Instance;
            if (client == null) return null;
            int hostId = client.HostId;
            foreach (PlayerControl player in PlayerControl.AllPlayerControls)
                if (player != null && player.Data != null && !player.Data.Disconnected && player.OwnerId == hostId) return player;
        }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "VotekickTools.HostPlayer: find host player"); }
        return null;
    }

    private static void TrackCode()
    {
        if (Time.unscaledTime < _trackAt) return;
        _trackAt = Time.unscaledTime + 1f;
        try
        {
            if (!InRoom() || AmongUsClient.Instance == null) return;
            int code = AmongUsClient.Instance.GameId;
            if (code != 0) _code = code;
        }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "VotekickTools.TrackCode: remember lobby code"); }
    }

    private static void SaveCode(bool copyAlways = false)
    {
        try
        {
            int code = AmongUsClient.Instance.GameId;
            if (code != 0) _code = code;
            if ((copyAlways || CheatToggles.votekickCopyCode) && _code != 0)
                GUIUtility.systemCopyBuffer = GameCode.IntToGameName(_code);
        }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "VotekickTools.SaveCode: save and copy lobby code"); }
    }

    internal static void Rejoin(int code)
    {
        try
        {
            AmongUsClient client = AmongUsClient.Instance;
            if (client == null || code == 0) return;
            client.GameId = code;
            var enumerator = client.CoJoinOnlineGameFromCode(code);
            if (enumerator != null) client.StartCoroutine(enumerator);
        }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "VotekickTools.Rejoin: rejoin lobby by code"); }
    }

    private static void Leave()
    {
        try
        {
            if (AmongUsClient.Instance != null) AmongUsClient.Instance.ExitGame(DisconnectReasons.ExitGame);
        }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "VotekickTools.Leave: leave lobby"); }
    }

    private static bool InRoom() => LobbyBehaviour.Instance != null || ShipStatus.Instance != null;
}
