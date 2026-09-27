using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace MalumMenu.Cheats
{
    // Ports othermenu Player/NocturneColorSnipe.cs (lobby color snipe),
    // Player/NocturneColorAll.cs (host force-one-color),
    // Player/NocturneColorReservations.cs (host FriendCode -> color file),
    // Player/NocturneNameColor.cs (local animated colored name).
    public static class ColorTools
    {
        private const int HandlingId = 20049;

        private static float _snipeNext;
        private static int _snipeLastTry = -1;
        private static float _allNext;
        private static float _resNext;

        public static void Tick()
        {
            try
            {
                SnipeTick();
                ColorAllTick();
                ReservationTick();
            }
            catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "ColorTools.Tick: running color ticks"); }
        }

        // ---- Color snipe (self, lobby only) ----

        public static int MaxColor()
        {
            try
            {
                if (Palette.PlayerColors != null) return Mathf.Max(0, Palette.PlayerColors.Length - 1);
            }
            catch { }
            return 17;
        }

        private static bool Taken(int id, PlayerControl self)
        {
            try
            {
                var e = PlayerControl.AllPlayerControls.GetEnumerator();
                while (e.MoveNext())
                {
                    PlayerControl p = e.Current;
                    if (p == null || p == self || p.Data == null || p.Data.Disconnected) continue;
                    if (p.Data.DefaultOutfit != null && p.Data.DefaultOutfit.ColorId == id) return true;
                }
            }
            catch { }
            return false;
        }

        private static void SnipeTick()
        {
            if (!CheatToggles.snipeColor || LobbyBehaviour.Instance == null)
            {
                _snipeLastTry = -1;
                return;
            }

            PlayerControl me = PlayerControl.LocalPlayer;
            if (me == null || me.Data == null || me.Data.DefaultOutfit == null) return;
            if (Time.time < _snipeNext) return;
            _snipeNext = Time.time + 0.25f;

            int want = Mathf.Clamp(CheatToggles.snipeColorId, 0, MaxColor());
            if (me.Data.DefaultOutfit.ColorId == want)
            {
                _snipeLastTry = -1;
                return;
            }
            if (Taken(want, me))
            {
                _snipeLastTry = -1;
                return;
            }
            if (_snipeLastTry == want) return;

            _snipeLastTry = want;
            try { me.CmdCheckColor((byte)want); }
            catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "ColorTools.SnipeTick: requesting sniped color"); }
        }

        // ---- Force one color on everyone (host) ----

        private static void ColorAllTick()
        {
            if (!CheatToggles.colorAll) return;
            if (AmongUsClient.Instance == null) return;
            try { if (!AmongUsClient.Instance.AmHost) return; }
            catch { return; }
            if (LobbyBehaviour.Instance == null && ShipStatus.Instance == null) return;

            float now = Time.unscaledTime;
            if (now - _allNext < 0.5f) return;
            _allNext = now;

            byte col = (byte)Mathf.Clamp(CheatToggles.colorAllId, 0, MaxColor());
            try
            {
                var e = PlayerControl.AllPlayerControls.GetEnumerator();
                while (e.MoveNext())
                {
                    PlayerControl p = e.Current;
                    if (p == null || p.Data == null || p.Data.Disconnected || p.Data.DefaultOutfit == null) continue;
                    if (p.Data.DefaultOutfit.ColorId == col) continue;
                    try { p.RpcSetColor(col); }
                    catch { }
                }
            }
            catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "ColorTools.ColorAllTick: forcing color on all"); }
        }

        // ---- Color reservations (host, FriendCode -> color file) ----

        public sealed class Reservation
        {
            public readonly string Fc;
            public readonly int ColorId;
            public readonly string Name;
            public Reservation(string fc, int colorId, string name)
            {
                Fc = fc ?? string.Empty;
                ColorId = colorId;
                Name = name ?? fc ?? string.Empty;
            }
        }

        private static readonly List<Reservation> ReservationCache = new List<Reservation>();
        private static bool _reservationsLoaded;

        private static string ReservationPath
        {
            get
            {
                try { return Path.Combine(Paths.GameRootPath, "HyperMenu", "ColorReservations.txt"); }
                catch { return "ColorReservations.txt"; }
            }
        }

        public static IReadOnlyList<Reservation> Reservations()
        {
            EnsureReservationsLoaded();
            return ReservationCache;
        }

        private static string NormFc(string fc) => fc == null ? string.Empty : fc.Trim().ToLowerInvariant();

        public static string FcOf(PlayerControl p)
        {
            return p != null && p.Data != null ? (p.Data.FriendCode ?? string.Empty).Trim().ToLowerInvariant() : null;
        }

        private static void EnsureReservationsLoaded()
        {
            if (_reservationsLoaded) return;
            _reservationsLoaded = true;
            LoadReservations();
        }

        private static void LoadReservations()
        {
            ReservationCache.Clear();
            try
            {
                if (!File.Exists(ReservationPath)) return;
                foreach (string raw in File.ReadAllLines(ReservationPath, Encoding.UTF8))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line[0] == '#') continue;
                    string[] p = line.Split('|');
                    if (p.Length < 2) continue;
                    string fc = p[0].Trim();
                    if (fc.Length == 0 || !int.TryParse(p[1].Trim(), out int col)) continue;
                    string name = p.Length >= 3 ? p[2].Trim() : fc;
                    ReservationCache.Add(new Reservation(fc, col, name.Length > 0 ? name : fc));
                }
            }
            catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "ColorTools.LoadReservations: reading reservation file"); }
        }

        private static void SaveReservations()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ReservationPath));
                var sb = new StringBuilder();
                sb.Append("# HyperMenu - FriendCode | ColorId | Name\n");
                foreach (Reservation e in ReservationCache)
                    sb.Append(e.Fc).Append('|').Append(e.ColorId).Append('|').Append(e.Name).Append('\n');
                File.WriteAllText(ReservationPath, sb.ToString(), Encoding.UTF8);
            }
            catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "ColorTools.SaveReservations: writing reservation file"); }
        }

        public static string ReserveTarget(PlayerControl target)
        {
            try
            {
                if (!Utils.isHost || target == null || target.Data == null || target.Data.DefaultOutfit == null)
                    return "This is a host-only cheat.";
                string fc = FcOf(target);
                if (string.IsNullOrWhiteSpace(fc)) return "No FriendCode for that player.";
                EnsureReservationsLoaded();
                string n = NormFc(fc);
                ReservationCache.RemoveAll(e => NormFc(e.Fc) == n);
                ReservationCache.Add(new Reservation(fc.Trim(), target.Data.DefaultOutfit.ColorId, target.Data.PlayerName));
                SaveReservations();
                return $"Reserved color {target.Data.DefaultOutfit.ColorId} for {target.Data.PlayerName}.";
            }
            catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "ColorTools.ReserveTarget: adding reservation"); return "Failed to reserve color."; }
        }

        public static string Unreserve(string fc)
        {
            try
            {
                EnsureReservationsLoaded();
                string n = NormFc(fc);
                if (ReservationCache.RemoveAll(e => NormFc(e.Fc) == n) > 0)
                {
                    SaveReservations();
                    return "Reservation removed.";
                }
                return "Reservation not found.";
            }
            catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "ColorTools.Unreserve: removing reservation"); return "Failed to remove reservation."; }
        }

        private static int FreeColor(int exclude)
        {
            int max = MaxColor();
            var used = new HashSet<int>();
            try
            {
                var e = PlayerControl.AllPlayerControls.GetEnumerator();
                while (e.MoveNext())
                {
                    PlayerControl p = e.Current;
                    if (p != null && p.Data != null && p.Data.DefaultOutfit != null)
                        used.Add(p.Data.DefaultOutfit.ColorId);
                }
            }
            catch { }
            for (int i = 0; i <= max; i++)
                if (i != exclude && !used.Contains(i)) return i;
            return -1;
        }

        private static bool TryApplyOnJoin(PlayerControl player)
        {
            if (!CheatToggles.colorReservations) return false;
            if (player == null || player.Data == null || player.Data.DefaultOutfit == null) return false;
            if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost) return false;

            string fc = FcOf(player);
            if (string.IsNullOrWhiteSpace(fc)) return false;
            EnsureReservationsLoaded();
            Reservation res = null;
            for (int i = 0; i < ReservationCache.Count; i++)
                if (NormFc(ReservationCache[i].Fc) == NormFc(fc)) { res = ReservationCache[i]; break; }
            if (res == null) return false;

            int target = Mathf.Clamp(res.ColorId, 0, MaxColor());
            if (player.Data.DefaultOutfit.ColorId == target) return false;

            try
            {
                var e = PlayerControl.AllPlayerControls.GetEnumerator();
                while (e.MoveNext())
                {
                    PlayerControl other = e.Current;
                    if (other == null || other.Data == null || other.Data.DefaultOutfit == null) continue;
                    if (other.PlayerId == player.PlayerId) continue;
                    if (other.Data.DefaultOutfit.ColorId != target) continue;

                    string otherFc = FcOf(other);
                    if (!string.IsNullOrWhiteSpace(otherFc))
                    {
                        bool otherReserved = false;
                        for (int i = 0; i < ReservationCache.Count; i++)
                            if (NormFc(ReservationCache[i].Fc) == NormFc(otherFc) && ReservationCache[i].ColorId == target) { otherReserved = true; break; }
                        if (otherReserved) return false;
                    }

                    int free = FreeColor(target);
                    if (free >= 0) other.RpcSetColor((byte)free);
                    break;
                }

                player.RpcSetColor((byte)target);
                return true;
            }
            catch { return false; }
        }

        private static void ReservationTick()
        {
            if (!CheatToggles.colorReservations) return;
            if (AmongUsClient.Instance == null) return;
            try { if (!AmongUsClient.Instance.AmHost) return; }
            catch { return; }
            if (LobbyBehaviour.Instance == null && ShipStatus.Instance == null) return;

            float now = Time.unscaledTime;
            if (now - _resNext < 1f) return;
            _resNext = now;

            try
            {
                var e = PlayerControl.AllPlayerControls.GetEnumerator();
                while (e.MoveNext())
                {
                    PlayerControl p = e.Current;
                    if (p == null || p.Data == null || p.Data.Disconnected) continue;
                    TryApplyOnJoin(p);
                }
            }
            catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "ColorTools.ReservationTick: applying reservations"); }
        }

        // ---- Colored name (local) ----

        private enum NameKind
        {
            Gradient, Rgb, Pulse, Wave, Sweep, Alt, Fade, Glitch, Typing, Flame, Hue, Blink
        }

        private struct NamePreset
        {
            public readonly string Name;
            public readonly NameKind Anim;
            public readonly Color A;
            public readonly Color B;
            public NamePreset(string name, NameKind anim, Color a, Color b)
            {
                Name = name;
                Anim = anim;
                A = a;
                B = b;
            }
        }

        private static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out Color c) ? c : Color.white;

        private static readonly NamePreset[] NamePresets =
        {
            new NamePreset("Aqua-Violet", NameKind.Gradient, new Color(0.27f, 1f, 0.85f), new Color(0.61f, 0.36f, 1f)),
            new NamePreset("Sunset", NameKind.Gradient, new Color(1f, 0.60f, 0.24f), new Color(1f, 0.24f, 0.47f)),
            new NamePreset("Fire", NameKind.Gradient, new Color(1f, 0.89f, 0.35f), new Color(1f, 0.32f, 0.18f)),
            new NamePreset("Ice", NameKind.Gradient, new Color(0.66f, 0.93f, 1f), new Color(0.16f, 0.42f, 1f)),
            new NamePreset("Toxic", NameKind.Gradient, new Color(0.78f, 1f, 0.30f), new Color(0.12f, 0.64f, 0.29f)),
            new NamePreset("Gold", NameKind.Gradient, new Color(1f, 0.91f, 0.66f), new Color(0.79f, 0.59f, 0.11f)),
            new NamePreset("Ocean", NameKind.Gradient, new Color(0f, 0.78f, 1f), new Color(0f, 0.45f, 1f)),
            new NamePreset("Galaxy", NameKind.Gradient, new Color(0.50f, 0f, 1f), new Color(0.88f, 0f, 1f)),
            new NamePreset("Neon", NameKind.Gradient, new Color(0.22f, 1f, 0.08f), new Color(0f, 0.90f, 1f)),
            new NamePreset("Emerald", NameKind.Gradient, new Color(0.26f, 0.91f, 0.48f), new Color(0.22f, 0.98f, 0.84f)),
            new NamePreset("Rainbow", NameKind.Rgb, Color.white, Color.white),
            new NamePreset("Fire pulse", NameKind.Pulse, new Color(1f, 0.70f, 0.28f), new Color(1f, 0.13f, 0.13f)),
            new NamePreset("Aqua pulse", NameKind.Pulse, new Color(0.27f, 1f, 0.85f), new Color(0.07f, 0.44f, 1f)),
            new NamePreset("Shimmer", NameKind.Wave, new Color(0.27f, 1f, 0.85f), new Color(0.61f, 0.36f, 1f)),
            new NamePreset("Comet", NameKind.Sweep, new Color(0.42f, 0.51f, 0.98f), new Color(0.99f, 0.36f, 0.49f)),
            new NamePreset("White", NameKind.Gradient, Color.white, Color.white),
            new NamePreset("Cyberpunk", NameKind.Gradient, new Color(0f, 0.94f, 1f), new Color(1f, 0.18f, 0.73f)),
            new NamePreset("Blood", NameKind.Gradient, new Color(1f, 0.18f, 0.18f), new Color(0.42f, 0f, 0f)),
            new NamePreset("Dusk", NameKind.Gradient, new Color(0.42f, 0.48f, 1f), new Color(0.17f, 0.11f, 0.30f)),
            new NamePreset("Sakura", NameKind.Gradient, new Color(1f, 0.84f, 0.91f), new Color(1f, 0.37f, 0.64f)),
            new NamePreset("Chrome", NameKind.Gradient, new Color(0.95f, 0.97f, 1f), new Color(0.43f, 0.49f, 0.57f)),
            new NamePreset("Aurora", NameKind.Wave, new Color(0.49f, 1f, 0.70f), new Color(0.42f, 0.36f, 1f)),
            new NamePreset("Venom pulse", NameKind.Pulse, new Color(0.71f, 1f, 0.24f), new Color(0.11f, 0.48f, 0.18f)),
            new NamePreset("Lightning", NameKind.Sweep, Color.white, new Color(0.31f, 0.76f, 1f)),
            new NamePreset("Zebra", NameKind.Alt, new Color(1f, 0.23f, 0.23f), Color.white),
            new NamePreset("Ghost", NameKind.Fade, new Color(0.86f, 0.90f, 1f), new Color(0.48f, 0.55f, 0.71f)),
            new NamePreset("Rust", NameKind.Gradient, new Color(1f, 0.63f, 0.30f), new Color(0.48f, 0.18f, 0.07f)),
            new NamePreset("Ultraviolet", NameKind.Gradient, new Color(0.69f, 0.29f, 1f), new Color(0.16f, 0.04f, 0.37f)),
            new NamePreset("Storm", NameKind.Gradient, new Color(0.78f, 0.82f, 1f), new Color(0.17f, 0.20f, 0.31f)),
            new NamePreset("Heat wave", NameKind.Wave, new Color(1f, 0.82f, 0.40f), new Color(1f, 0.30f, 0.30f)),
            new NamePreset("Meteor", NameKind.Sweep, new Color(1f, 0.91f, 0.66f), new Color(1f, 0.42f, 0f)),
            new NamePreset("Ice pulse", NameKind.Pulse, new Color(0.87f, 0.96f, 1f), new Color(0.31f, 0.66f, 1f)),
            new NamePreset("Stripes", NameKind.Alt, new Color(1f, 0.82f, 0.40f), new Color(0.48f, 0.29f, 1f)),
            new NamePreset("Glitch", NameKind.Glitch, new Color(0f, 1f, 0.78f), new Color(1f, 0f, 0.33f)),
            new NamePreset("Matrix", NameKind.Typing, new Color(0.21f, 1f, 0.42f), new Color(0.04f, 0.30f, 0.12f)),
            new NamePreset("Flame", NameKind.Flame, new Color(1f, 0.88f, 0.40f), new Color(1f, 0.24f, 0f)),
            new NamePreset("Chameleon", NameKind.Hue, Color.white, Color.white),
            new NamePreset("Siren", NameKind.Blink, new Color(0.18f, 0.48f, 1f), new Color(1f, 0.18f, 0.18f)),
        };

        public static int NamePresetCount => NamePresets.Length;
        public static int ClampStyle(int i) => i < 0 ? 0 : (i >= NamePresets.Length ? NamePresets.Length - 1 : i);
        public static string StyleName(int i) => NamePresets[ClampStyle(i)].Name;

        private static readonly StringBuilder NameBuilder = new StringBuilder(256);
        private static readonly StringBuilder StripBuilder = new StringBuilder(64);

        public static string StripTags(string value)
        {
            if (string.IsNullOrEmpty(value) || (value.IndexOf('<') < 0 && value.IndexOf('&') < 0))
                return value ?? string.Empty;

            StripBuilder.Length = 0;
            int i = 0;
            while (i < value.Length)
            {
                char ch = value[i];
                if (ch == '<')
                {
                    int close = value.IndexOf('>', i + 1);
                    if (close >= 0)
                    {
                        i = close + 1;
                        continue;
                    }
                }
                StripBuilder.Append(ch);
                i++;
            }
            StripBuilder.Replace("&lt;", "<").Replace("&gt;", ">").Replace("&amp;", "&");
            return StripBuilder.ToString();
        }

        public static string ApplyNameStyle(string rawName, int styleIndex, bool animated)
        {
            if (string.IsNullOrEmpty(rawName)) return rawName ?? string.Empty;
            rawName = StripTags(rawName);
            if (rawName.Length == 0) return string.Empty;

            NamePreset preset = NamePresets[ClampStyle(styleIndex)];
            float time = animated ? Time.unscaledTime : 0f;
            NameBuilder.Length = 0;
            int n = rawName.Length;

            switch (preset.Anim)
            {
                case NameKind.Rgb:
                    for (int i = 0; i < n; i++)
                    {
                        float phase = i * 0.5f + time * 2.5f;
                        AppendNameChar(rawName[i], Mathf.Sin(phase) * 0.5f + 0.5f, Mathf.Sin(phase + 4f) * 0.5f + 0.5f, Mathf.Sin(phase + 2f) * 0.5f + 0.5f);
                    }
                    break;
                case NameKind.Pulse:
                {
                    float t = animated ? Mathf.Sin(time * 2.2f) * 0.5f + 0.5f : 0.5f;
                    Color c = CloseColors(preset.A, preset.B) ? preset.A : LerpOkLab(preset.A, preset.B, t);
                    AppendNameSolid(rawName, c);
                    break;
                }
                case NameKind.Wave:
                {
                    float hi = Mathf.Repeat(time * 8f, n + 4f) - 2f;
                    for (int i = 0; i < n; i++)
                    {
                        float pos = n == 1 ? 0.5f : (float)i / (n - 1);
                        Color c = LerpOkLab(preset.A, preset.B, pos);
                        if (animated)
                        {
                            float glow = Mathf.Clamp01(1f - Mathf.Abs(i - hi) / 1.6f);
                            if (glow > 0f) c = Color.Lerp(c, Color.white, glow * 0.6f);
                        }
                        AppendNameChar(rawName[i], c.r, c.g, c.b);
                    }
                    break;
                }
                case NameKind.Sweep:
                {
                    float sweep = Mathf.Repeat(time * 10f, n + 6f) - 3f;
                    for (int i = 0; i < n; i++)
                    {
                        Color c;
                        if (animated)
                        {
                            float band = Mathf.Clamp01(1f - Mathf.Abs(i - sweep) / 2.2f);
                            c = band > 0f ? LerpOkLab(preset.A, preset.B, band) : preset.A;
                        }
                        else
                        {
                            float pos = n == 1 ? 0.5f : (float)i / (n - 1);
                            c = LerpOkLab(preset.A, preset.B, pos);
                        }
                        AppendNameChar(rawName[i], c.r, c.g, c.b);
                    }
                    break;
                }
                case NameKind.Alt:
                {
                    int shift = animated ? Mathf.FloorToInt(time * 4f) : 0;
                    for (int i = 0; i < n; i++)
                    {
                        Color c = ((i + shift) & 1) == 0 ? preset.A : preset.B;
                        AppendNameChar(rawName[i], c.r, c.g, c.b);
                    }
                    break;
                }
                case NameKind.Fade:
                {
                    float t = animated ? Mathf.Sin(time * 2.6f) * 0.5f + 0.5f : 1f;
                    Color c = LerpOkLab(preset.A, preset.B, t);
                    c.a = Mathf.Lerp(0.4f, 1f, t);
                    AppendNameSolid(rawName, c);
                    break;
                }
                case NameKind.Glitch:
                {
                    int step = animated ? Mathf.FloorToInt(time * 12f) : 0;
                    for (int i = 0; i < n; i++)
                    {
                        int h = (i * 73856093) ^ (step * 19349663);
                        h ^= h >> 13;
                        bool hit = (h & 7) == 0;
                        Color c = hit ? (((h >> 3) & 1) == 0 ? Color.white : preset.B) : preset.A;
                        AppendNameChar(rawName[i], c.r, c.g, c.b);
                    }
                    break;
                }
                case NameKind.Typing:
                {
                    float head = animated ? Mathf.Repeat(time * 6f, n + 5f) : n;
                    for (int i = 0; i < n; i++)
                    {
                        float d = head - i;
                        Color c;
                        if (d < 0f) c = preset.B;
                        else if (d < 1f) c = Color.white;
                        else c = LerpOkLab(preset.A, preset.B, Mathf.Clamp01((d - 1f) / 6f));
                        AppendNameChar(rawName[i], c.r, c.g, c.b);
                    }
                    break;
                }
                case NameKind.Flame:
                {
                    for (int i = 0; i < n; i++)
                    {
                        float t = animated
                            ? (Mathf.Sin(i * 1.7f + time * 5f) + Mathf.Sin(i * 0.9f - time * 3.3f)) * 0.25f + 0.5f
                            : (n == 1 ? 0.5f : (float)i / (n - 1));
                        Color c = LerpOkLab(preset.A, preset.B, t);
                        AppendNameChar(rawName[i], c.r, c.g, c.b);
                    }
                    break;
                }
                case NameKind.Hue:
                {
                    float h = animated ? Mathf.Repeat(time * 0.12f, 1f) : 0.62f;
                    AppendNameSolid(rawName, Color.Lerp(HueRgb(h), Color.white, 0.18f));
                    break;
                }
                case NameKind.Blink:
                {
                    bool second = animated && Mathf.Repeat(time * 3f, 2f) >= 1f;
                    AppendNameSolid(rawName, second ? preset.B : preset.A);
                    break;
                }
                default:
                {
                    bool solid = CloseColors(preset.A, preset.B);
                    if (solid || n == 1)
                    {
                        AppendNameSolid(rawName, solid ? preset.A : LerpOkLab(preset.A, preset.B, 0.5f));
                        break;
                    }
                    float flow = time * 5.4f;
                    float span = 2f * (n - 1);
                    for (int i = 0; i < n; i++)
                    {
                        float cycle = Mathf.Repeat(i + flow, span);
                        float t = cycle <= (n - 1) ? cycle / (n - 1) : (span - cycle) / (n - 1);
                        Color c = LerpOkLab(preset.A, preset.B, t);
                        AppendNameChar(rawName[i], c.r, c.g, c.b);
                    }
                    break;
                }
            }

            return NameBuilder.ToString();
        }

        private static void AppendNameSolid(string text, Color c)
        {
            AppendNameOpen(c);
            for (int i = 0; i < text.Length; i++) AppendNameEscaped(text[i]);
            NameBuilder.Append("</color>");
        }

        private static void AppendNameChar(char ch, float r, float g, float b)
        {
            AppendNameOpen(new Color(r, g, b, 1f));
            AppendNameEscaped(ch);
            NameBuilder.Append("</color>");
        }

        private static void AppendNameOpen(Color c)
        {
            int r = Mathf.Clamp(Mathf.RoundToInt(c.r * 255f), 0, 255);
            int g = Mathf.Clamp(Mathf.RoundToInt(c.g * 255f), 0, 255);
            int b = Mathf.Clamp(Mathf.RoundToInt(c.b * 255f), 0, 255);
            const string hex = "0123456789ABCDEF";
            NameBuilder.Append("<color=#");
            NameBuilder.Append(hex[(r >> 4) & 0xF]).Append(hex[r & 0xF]);
            NameBuilder.Append(hex[(g >> 4) & 0xF]).Append(hex[g & 0xF]);
            NameBuilder.Append(hex[(b >> 4) & 0xF]).Append(hex[b & 0xF]);
            if (c.a < 0.995f)
            {
                int a = Mathf.Clamp(Mathf.RoundToInt(c.a * 255f), 0, 255);
                NameBuilder.Append(hex[(a >> 4) & 0xF]).Append(hex[a & 0xF]);
            }
            NameBuilder.Append('>');
        }

        private static void AppendNameEscaped(char ch)
        {
            switch (ch)
            {
                case '<': NameBuilder.Append("&lt;"); break;
                case '>': NameBuilder.Append("&gt;"); break;
                case '&': NameBuilder.Append("&amp;"); break;
                default: NameBuilder.Append(ch); break;
            }
        }

        private static Color HueRgb(float h)
        {
            float x = Mathf.Repeat(h, 1f) * 6f;
            float f = x - Mathf.Floor(x);
            switch ((int)x)
            {
                case 0: return new Color(1f, f, 0f, 1f);
                case 1: return new Color(1f - f, 1f, 0f, 1f);
                case 2: return new Color(0f, 1f, f, 1f);
                case 3: return new Color(0f, 1f - f, 1f, 1f);
                case 4: return new Color(f, 0f, 1f, 1f);
                default: return new Color(1f, 0f, 1f - f, 1f);
            }
        }

        private static bool CloseColors(Color a, Color b) =>
            Mathf.Abs(a.r - b.r) < 0.004f && Mathf.Abs(a.g - b.g) < 0.004f && Mathf.Abs(a.b - b.b) < 0.004f;

        private static Color LerpOkLab(Color a, Color b, float t)
        {
            RgbToOkLab(a, out float l1, out float a1, out float b1);
            RgbToOkLab(b, out float l2, out float a2, out float b2);
            return OkLabToRgb(Mathf.Lerp(l1, l2, t), Mathf.Lerp(a1, a2, t), Mathf.Lerp(b1, b2, t));
        }

        private static float ToLinear(float c) => c <= 0.04045f ? c / 12.92f : Mathf.Pow((c + 0.055f) / 1.055f, 2.4f);
        private static float ToSrgb(float c)
        {
            if (c < 0f) c = 0f;
            return c <= 0.0031308f ? 12.92f * c : 1.055f * Mathf.Pow(c, 1f / 2.4f) - 0.055f;
        }
        private static float Cbrt(float x) => x <= 0f ? 0f : Mathf.Pow(x, 1f / 3f);

        private static void RgbToOkLab(Color c, out float L, out float A, out float B)
        {
            float r = ToLinear(c.r), g = ToLinear(c.g), bl = ToLinear(c.b);
            float l = 0.4122214708f * r + 0.5363325363f * g + 0.0514459929f * bl;
            float m = 0.2119034982f * r + 0.6806995451f * g + 0.1073969566f * bl;
            float s = 0.0883024619f * r + 0.2817188376f * g + 0.6299787005f * bl;
            float l_ = Cbrt(l), m_ = Cbrt(m), s_ = Cbrt(s);
            L = 0.2104542553f * l_ + 0.7936177850f * m_ - 0.0040720468f * s_;
            A = 1.9779984951f * l_ - 2.4285922050f * m_ + 0.4505937099f * s_;
            B = 0.0259040371f * l_ + 0.7827717662f * m_ - 0.8086757660f * s_;
        }

        private static Color OkLabToRgb(float L, float A, float B)
        {
            float l_ = L + 0.3963377774f * A + 0.2158037573f * B;
            float m_ = L - 0.1055613458f * A - 0.0638541728f * B;
            float s_ = L - 0.0894841775f * A - 1.2914855480f * B;
            float l = l_ * l_ * l_, m = m_ * m_ * m_, s = s_ * s_ * s_;
            float r = 4.0767416621f * l - 3.3077115913f * m + 0.2309699292f * s;
            float g = -1.2684380046f * l + 2.6097574011f * m - 0.3413193965f * s;
            float b = -0.0041960863f * l - 0.7034186147f * m + 1.7076147010f * s;
            return new Color(Mathf.Clamp01(ToSrgb(r)), Mathf.Clamp01(ToSrgb(g)), Mathf.Clamp01(ToSrgb(b)), 1f);
        }

        // Runs after MalumESP.PlayerNametags (same LateUpdate, Priority.Last) so the
        // styled name is not wiped by ESP's per-frame SetName. Skipped while ESP info
        // nametags own the name, mirroring othermenu's VisualAssist.OwnsName guard.
        private static string _nameRaw;
        private static int _nameStyle = -1;
        private static bool _nameAnim;
        private static string _nameCached = string.Empty;

        public static void ApplyFor(PlayerPhysics pp)
        {
            try
            {
                if (!CheatToggles.nameColor) return;
                if (CheatToggles.seeRoles || CheatToggles.seePlayerInfo) return;
                PlayerControl me = PlayerControl.LocalPlayer;
                if (me == null || pp == null || pp.myPlayer != me) return;
                if (me.cosmetics == null || me.cosmetics.nameText == null) return;

                string raw = me.CurrentOutfit != null ? me.CurrentOutfit.PlayerName : (me.Data != null ? me.Data.PlayerName : null);
                raw = StripTags(raw);
                if (string.IsNullOrEmpty(raw)) return;

                int style = CheatToggles.nameColorStyle;
                bool anim = CheatToggles.nameColorAnimated;
                if (anim || raw != _nameRaw || style != _nameStyle || anim != _nameAnim)
                {
                    _nameRaw = raw;
                    _nameStyle = style;
                    _nameAnim = anim;
                    _nameCached = ApplyNameStyle(raw, style, anim);
                }
                me.cosmetics.nameText.text = _nameCached;
            }
            catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "ColorTools.ApplyFor: styling local name"); }
        }

        [HarmonyPatch(typeof(PlayerPhysics), nameof(PlayerPhysics.LateUpdate))]
        [HarmonyPriority(Priority.Last)]
        public static class ColoredNamePatch
        {
            public static void Postfix(PlayerPhysics __instance)
            {
                ApplyFor(__instance);
            }
        }
    }
}
