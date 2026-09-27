using System;
using System.Collections.Generic;
using HarmonyLib;
using Hazel;
using InnerNet;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MalumMenu.Cheats;

// othermenu/Lobby/HyperTwins.cs — host-only networked clones everyone sees.
// Queued spawns of frozen PlayerControl twins (owner -2), click to spawn/remove,
// formations + text-from-clones via LocalClones.FormationPos + CloneFont.
public sealed class NetworkedClones : MonoBehaviour
{
    private const int HandlingId = 20064;
    private const int Max = 9999;
    private const int Owner = -2;

    private sealed class Twin
    {
        public PlayerControl Pc; public Vector2 At; public int Batch;
    }
    private struct Job
    {
        public byte Src; public Vector2 At; public bool Slow; public int Batch;
    }

    private static readonly List<Twin> _live = new List<Twin>();
    private static readonly Queue<Job> _pend = new Queue<Job>();
    private static readonly HashSet<uint> _netIds = new HashSet<uint>();
    private static readonly List<int> _batches = new List<int>();
    private static int _nextBatch;
    private float _spawnGate;

    internal static NetworkedClones Instance { get; private set; }
    public void Awake() => Instance = this;
    public void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    internal static int Count => _live.Count;
    internal static int Queued => _pend.Count;
    internal static int Figures => _batches.Count;

    internal static void Prune()
    {
        try
        {
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                Twin t = _live[i];
                bool dead = t == null || t.Pc == null || t.Pc.transform == null;
                if (dead)
                    _live.RemoveAt(i);
            }

            _netIds.Clear();
            _batches.Clear();
            for (int i = 0; i < _live.Count; i++)
            {
                Twin t = _live[i];
                try
                {
                    _netIds.Add(((InnerNetObject)(object)t.Pc).NetId);
                }
                catch { }
                if (!_batches.Contains(t.Batch))
                    _batches.Add(t.Batch);
            }
        }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "NetworkedClones.Prune: prune"); }
    }

    internal static bool IsClone(PlayerControl pc)
    {
        if (pc == null)
            return false;
        try
        {
            if (_netIds.Contains(((InnerNetObject)(object)pc).NetId)) return true;
        }
        catch { }
        try
        {
            return ((InnerNetObject)(object)pc).OwnerId == Owner;
        }
        catch { }
        return false;
    }

    private static int NewBatch()
    {
        int id = ++_nextBatch;
        _batches.Add(id);
        return id;
    }

    public void Update()
    {
        float now = Time.unscaledTime;

        try
        {
            Clicks();
        }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "NetworkedClones.Update: clicks"); }

        try
        {
            if (_pend.Count > 0 && now >= _spawnGate && Ready())
            {
                Job j = _pend.Dequeue();
                _spawnGate = now + (j.Slow ? 0.28f : 0.14f);
                if (_live.Count < Max)
                {
                    PlayerControl src = ById(j.Src);
                    if (src != null)
                    {
                        try
                        {
                            Make(src, j.At, j.Batch);
                        }
                        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "NetworkedClones.Update: make"); }
                    }
                }
            }
        }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "NetworkedClones.Update: queue"); }
    }

    internal static string CloneOf(PlayerControl src)
    {
        if (Instance == null || !Ready())
            return "Host only.";
        if (src == null || src.transform == null)
            return "No player.";
        if (_live.Count + _pend.Count >= Max)
            return "Clone limit.";
        Vector3 p = src.transform.position + Vector3.left * 0.6f;
        _pend.Enqueue(new Job { Src = src.PlayerId, At = new Vector2(p.x, p.y), Batch = NewBatch() });
        return "Clone queued";
    }

    internal static string Formation(int idx, int count, PlayerControl src = null)
    {
        if (Instance == null || !Ready())
            return "Host only.";
        PlayerControl me = PlayerControl.LocalPlayer;
        if (me == null || me.transform == null) return "No player.";
        PlayerControl who = src != null ? src : me;
        int n = Mathf.Clamp(count, 1, Max - _live.Count - _pend.Count);
        if (n <= 0)
            return "Clone limit.";
        Vector3 c = me.transform.position;
        int b = NewBatch();
        float scale = LocalClones.FormScale();
        for (int i = 0; i < n; i++)
        {
            Vector3 fp = c + (LocalClones.FormationPos(idx, i, n, c) - c) * scale;
            _pend.Enqueue(new Job { Src = who.PlayerId, At = new Vector2(fp.x, fp.y), Batch = b });
        }
        return "Clones queued: " + n;
    }

    internal static string Text(string text, PlayerControl src = null)
    {
        if (Instance == null || !Ready())
            return "Host only.";
        PlayerControl me = PlayerControl.LocalPlayer;
        if (me == null || me.transform == null)
            return "No player.";
        PlayerControl who = src != null ? src : me;
        if (string.IsNullOrWhiteSpace(text))
            return "Empty text.";

        text = text.ToUpperInvariant();
        float px = 0.38f;
        float need = CloneFont.GetTextWidth(text, px);
        if (need > 15f)
            px = px * 15f / need;
        List<Vector3> offs = CloneFont.GetPositions(text, px);
        if (offs.Count == 0)
            return "Empty.";

        Vector3 c = me.transform.position;
        int room = Max - _live.Count - _pend.Count;
        int made = 0;
        int b = NewBatch();
        for (int i = 0; i < offs.Count && made < room; i++)
        {
            _pend.Enqueue(new Job { Src = who.PlayerId, At = new Vector2(c.x + offs[i].x, c.y + offs[i].y), Slow = true, Batch = b });
            made++;
        }
        return "Clones queued: " + made;
    }

    internal static void ClearAll()
    {
        try
        {
            _pend.Clear();
            _batches.Clear();
            Twin[] arr = _live.ToArray();
            _live.Clear();
            foreach (Twin t in arr)
                Kill(t?.Pc);
            _netIds.Clear();
        }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "NetworkedClones.ClearAll: clear"); }
    }

    internal static string DropLast()
    {
        try
        {
            if (_batches.Count == 0)
                return "No figures.";
            int id = _batches[_batches.Count - 1];
            _batches.RemoveAt(_batches.Count - 1);

            if (_pend.Count > 0)
            {
                var keep = new Queue<Job>();
                while (_pend.Count > 0)
                {
                    Job j = _pend.Dequeue();
                    if (j.Batch != id)
                        keep.Enqueue(j);
                }
                while (keep.Count > 0)
                    _pend.Enqueue(keep.Dequeue());
            }

            int n = 0;
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                if (_live[i].Batch != id)
                    continue;
                PlayerControl pc = _live[i].Pc;
                _live.RemoveAt(i);
                Kill(pc);
                n++;
            }
            return "Removed: " + n;
        }
        catch (Exception ex)
        {
            ErrorReporter.Report(ex, HandlingId, "NetworkedClones.DropLast: drop");
            return "Error.";
        }
    }

    internal static bool Ready()
    {
        try
        {
            if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost)
                return false;
            if (PlayerControl.LocalPlayer == null || GameData.Instance == null) return false;
            InnerNetClient.GameStates gs = ((InnerNetClient)AmongUsClient.Instance).GameState;
            return gs == InnerNetClient.GameStates.Joined || gs == InnerNetClient.GameStates.Started;
        }
        catch
        {
            return false;
        }
    }

    private void Clicks()
    {
        if (!CheatToggles.netCloneMode) return;
        if (!Ready() || MenuUI.isGUIActive || MalumMenu.isPanicked || Camera.main == null)
            return;
        try
        {
            if (HudManager.InstanceExists && HudManager.Instance.Chat != null && HudManager.Instance.Chat.IsOpenOrOpening)
                return;
        }
        catch { }
        PlayerControl me = PlayerControl.LocalPlayer;
        if (me == null) return;

        Vector3 w = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Vector2 at = new Vector2(w.x, w.y);

        if (Input.GetMouseButtonDown(0))
        {
            if (_live.Count + _pend.Count < Max)
                _pend.Enqueue(new Job { Src = me.PlayerId, At = at, Batch = NewBatch() });
        }
        else if (Input.GetMouseButtonDown(1))
        {
            Nearest(at);
        }
    }

    private static void Nearest(Vector2 at)
    {
        int best = -1;
        float bd = 1.6f;
        for (int i = 0; i < _live.Count; i++)
        {
            Twin t = _live[i];
            if (t?.Pc == null) continue;
            float d = Vector2.Distance(t.At, at);
            if (d < bd)
            {
                bd = d;
                best = i;
            }
        }
        if (best < 0)
            return;
        PlayerControl pc = _live[best].Pc;
        _live.RemoveAt(best);
        Kill(pc);
    }

    private static PlayerControl ById(byte pid)
    {
        try
        {
            foreach (PlayerControl p in PlayerControl.AllPlayerControls)
                if (p != null && p.PlayerId == pid && !IsClone(p))
                    return p;
        }
        catch { }
        return null;
    }

    private void Make(PlayerControl src, Vector2 at, int batch)
    {
        if (src == null || src.Data == null)
            return;
        AmongUsClient net = AmongUsClient.Instance;
        PlayerControl prefab = net != null ? net.PlayerPrefab : null;
        if (prefab == null)
            return;

        Vector3 pos = new Vector3(at.x, at.y, src.transform.position.z);
        bool was = prefab.gameObject.activeSelf;
        prefab.gameObject.SetActive(false);

        PlayerControl tw = null;
        try
        {
            tw = Object.Instantiate(prefab);
            tw.PlayerId = src.PlayerId;
            tw.isNew = false;
            tw.notRealPlayer = true;
            ((Component)tw).transform.position = pos;
            tw.gameObject.SetActive(true);
        }
        finally { prefab.gameObject.SetActive(was); }

        if (tw == null) return;

        ((InnerNetClient)net).Spawn((InnerNetObject)(object)tw, Owner, (SpawnFlags)0);
        ((Component)tw).transform.position = pos;
        Freeze(tw);
        try
        {
            _netIds.Add(((InnerNetObject)(object)tw).NetId);
        }
        catch { }
        _live.Add(new Twin { Pc = tw, At = at, Batch = batch });
    }

    private static void Freeze(PlayerControl pc)
    {
        try
        {
            if (pc.Collider != null) ((Behaviour)pc.Collider).enabled = false;
        }
        catch { }
        try
        {
            if (pc.MyPhysics != null)
                ((Behaviour)pc.MyPhysics).enabled = false;
        }
        catch { }
        try
        {
            if (pc.NetTransform != null)
                ((Behaviour)pc.NetTransform).enabled = false;
        }
        catch { }
    }

    private static void Kill(PlayerControl pc)
    {
        if (pc == null) return;
        try
        {
            _netIds.Remove(((InnerNetObject)(object)pc).NetId);
        }
        catch { }
        try
        {
            pc.PlayerId = 253;
        }
        catch { }
        try
        {
            AmongUsClient net = AmongUsClient.Instance;
            MessageWriter w = MessageWriter.Get((SendOption)1);
            w.StartMessage(5);
            w.Write(((InnerNetClient)net).GameId);
            w.StartMessage(5);
            w.WritePacked(((InnerNetObject)(object)pc).NetId);
            w.EndMessage();
            w.EndMessage();
            ((InnerNetClient)net).SendOrDisconnect(w);
            w.Recycle();
            ((InnerNetClient)net).RemoveNetObject((InnerNetObject)(object)pc);
        }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "NetworkedClones.Kill: despawn"); }
        try
        {
            Object.Destroy(pc.gameObject);
        }
        catch { }
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.GetTruePosition))]
internal static class NetworkedClonesPosGuard
{
    public static bool Prefix(PlayerControl __instance, ref Vector2 __result)
    {
        if (__instance != null && __instance.transform != null) return true;
        __result = Vector2.zero;
        return false;
    }
}

[HarmonyPatch(typeof(LobbyBehaviour), nameof(LobbyBehaviour.Start))]
internal static class NetworkedClonesResetPatch
{
    public static void Postfix() => Cheats.NetworkedClones.Prune();
}

[HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.Start))]
internal static class NetworkedClonesMatchClearPatch
{
    public static void Postfix() => Cheats.NetworkedClones.ClearAll();
}
