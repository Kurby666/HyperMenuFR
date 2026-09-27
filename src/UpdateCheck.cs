using System;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Il2CppInterop.Runtime.Attributes;
using UnityEngine;

namespace MalumMenu;

internal enum UpdateState
{
    Idle,
    Checking,
    Found,
    Loading,
    Done,
    Fail
}

public class UpdateCheck : MonoBehaviour
{
    private const int HandlingId = 10012;
    private const string VersionUrl = "https://raw.githubusercontent.com/The-HyperMenu-Team/HyperMenu/main/version.json";
    private const string DownloadUrl = "https://github.com/The-HyperMenu-Team/HyperMenu/releases/latest/download/HyperMenu.dll";

    private static readonly HttpClient Http = MakeClient();

    internal static UpdateState State { get; private set; } = UpdateState.Idle;
    internal static string Latest { get; private set; } = "";
    internal static string Error { get; private set; } = "";

    private Task<string> _check;
    private Task<byte[]> _load;
    private bool _started;
    private bool _announced;
    private float _at;

    internal static UpdateCheck Instance { get; private set; }
    public void Awake() => Instance = this;
    public void Start() => _at = Time.unscaledTime + 10f;

    private static HttpClient MakeClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        client.DefaultRequestHeaders.Add("User-Agent", "HyperMenu");
        return client;
    }

    public void Update()
    {
        try
        {
            if (!_started && Time.unscaledTime >= _at)
            {
                _started = true;
                BeginCheck();
            }

            if (_check != null && _check.IsCompleted)
            {
                Task<string> t = _check;
                _check = null;
                if (t.IsFaulted || t.IsCanceled)
                    Fail(ErrorOf(t));
                else
                    Apply(t.Result);
            }

            if (_load != null && _load.IsCompleted)
            {
                Task<byte[]> t = _load;
                _load = null;
                if (t.IsFaulted || t.IsCanceled)
                {
                    Fail(ErrorOf(t));
                    MalumMenu.notifications.Send("Update", "Download failed: " + Error, 10);
                }
                else
                    Install(t.Result);
            }

            if (!_announced && State == UpdateState.Found)
            {
                _announced = true;
                MalumMenu.notifications.Send("Update available", "HyperMenu v" + Latest + " is out — grab it from the Config tab.", 10);
            }
        }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "UpdateCheck.Update: poll check/download tasks"); }
    }

    internal static void Recheck()
    {
        if (Instance == null || State == UpdateState.Checking || State == UpdateState.Loading) return;
        Instance._announced = false;
        Instance.BeginCheck();
    }

    private void BeginCheck()
    {
        Error = "";
        State = UpdateState.Checking;
        try
        {
            _check = Task.Run(() => Fetch());
        }
        catch (Exception ex)
        {
            ErrorReporter.Report(ex, HandlingId, "UpdateCheck.BeginCheck: start version fetch");
            Fail(ex.Message);
            _check = null;
        }
    }

    internal static void Download()
    {
        if (Instance == null || State != UpdateState.Found || Instance._load != null) return;
        Error = "";
        State = UpdateState.Loading;
        try
        {
            Instance._load = Http.GetByteArrayAsync(DownloadUrl);
        }
        catch (Exception ex)
        {
            ErrorReporter.Report(ex, HandlingId, "UpdateCheck.Download: start dll download");
            Fail(ex.Message);
            Instance._load = null;
        }
    }

    internal static void Restart()
    {
        try
        {
            Application.Quit();
        }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "UpdateCheck.Restart: quit game"); }
    }

    private static void Fail(string message)
    {
        Error = message ?? "";
        State = UpdateState.Fail;
    }

    private static string ErrorOf(Task task)
    {
        Exception ex = task.Exception != null ? task.Exception.GetBaseException() : null;
        if (ex == null) return task.IsCanceled ? "canceled" : "unknown";
        return ex.GetType().Name + ": " + ex.Message;
    }

    [HideFromIl2Cpp]
    private static string Fetch()
    {
        string bust = VersionUrl + "?_=" + DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return Http.GetStringAsync(bust).GetAwaiter().GetResult();
    }

    [HideFromIl2Cpp]
    private static void Apply(string json)
    {
        try
        {
            string ver = Grab(json, "\"version\"");
            if (string.IsNullOrEmpty(ver))
            {
                State = UpdateState.Idle;
                return;
            }

            Latest = ver.TrimStart('v', 'V').Trim();
            State = Newer(Latest, MalumMenu.hyperVersion) ? UpdateState.Found : UpdateState.Idle;
        }
        catch (Exception ex)
        {
            ErrorReporter.Report(ex, HandlingId, "UpdateCheck.Apply: parse version.json");
            Fail(ex.Message);
        }
    }

    [HideFromIl2Cpp]
    private static void Install(byte[] data)
    {
        if (data == null || data.Length < 1024)
        {
            Fail("empty file");
            return;
        }
        try
        {
            string pluginDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string target = Path.Combine(pluginDir, "HyperMenu.dll");
            string legacy = Path.Combine(pluginDir, "MalumMenu.dll");
            string tmp = target + ".new";

            File.WriteAllBytes(tmp, data);
            if (File.Exists(target)) File.Delete(target);
            File.Move(tmp, target);
            if (File.Exists(legacy)) File.Delete(legacy);

            State = UpdateState.Done;
            MalumMenu.notifications.Send("Update", "Installed. Restart the game.", 10);
        }
        catch (Exception ex)
        {
            ErrorReporter.Report(ex, HandlingId, "UpdateCheck.Install: replace plugin dll");
            Fail(ex.GetType().Name + ": " + ex.Message);
            MalumMenu.notifications.Send("Update", "Install failed: " + Error, 10);
        }
    }

    [HideFromIl2Cpp]
    private static string Grab(string json, string key)
    {
        int i = json.IndexOf(key, StringComparison.Ordinal);
        if (i < 0) return null;
        i = json.IndexOf(':', i);
        if (i < 0) return null;
        int q1 = json.IndexOf('"', i + 1);
        if (q1 < 0) return null;
        int q2 = json.IndexOf('"', q1 + 1);
        return q2 < 0 ? null : json.Substring(q1 + 1, q2 - q1 - 1);
    }

    [HideFromIl2Cpp]
    private static bool Newer(string latest, string current)
    {
        try
        {
            int[] a = Parts(latest), b = Parts(current);
            for (int i = 0; i < 3; i++)
            {
                if (a[i] > b[i]) return true;
                if (a[i] < b[i]) return false;
            }
        }
        catch (Exception ex) { ErrorReporter.Report(ex, HandlingId, "UpdateCheck.Newer: compare versions"); }
        return false;
    }

    [HideFromIl2Cpp]
    private static int[] Parts(string version)
    {
        var result = new int[3];
        string[] split = version.Trim().Split('.', '-', '+');
        for (int i = 0; i < 3 && i < split.Length; i++)
        {
            var digits = new StringBuilder();
            foreach (char c in split[i])
            {
                if (!char.IsDigit(c)) break;
                digits.Append(c);
            }
            int.TryParse(digits.ToString(), out result[i]);
        }
        return result;
    }
}
