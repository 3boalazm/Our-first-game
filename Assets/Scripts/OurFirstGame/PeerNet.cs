using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// Thin C# wrapper around PeerBridge.jslib (WebSocket relay, see /relay).
/// In the Unity Editor / non-WebGL builds there is no network, so everything is a stub.
/// </summary>
public static class PeerNet
{
    public enum Status
    {
        Idle = 0, Loading = 1, Hosting = 2, Connecting = 3, Connected = 4,
        Closed = 5, Error = 6, IdTaken = 7, NotFound = 8,
        Reconnecting = 9 // host lost the signalling server (e.g. page was in the background) and is re-registering
    }

    // Namespace so our room codes never clash with other apps on the public PeerJS server.
    public const string Prefix = "ourfirstgame-mustafa-doha-v1-";

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern void PB_Start(string prefix, int isHost, string code);
    [DllImport("__Internal")] private static extern int PB_GetStatus();
    [DllImport("__Internal")] private static extern string PB_GetError();
    [DllImport("__Internal")] private static extern void PB_Send(string msg);
    [DllImport("__Internal")] private static extern string PB_Poll();
    [DllImport("__Internal")] private static extern void PB_Close();
    [DllImport("__Internal")] private static extern string PB_GetUrlParam(string name);
    [DllImport("__Internal")] private static extern string PB_PageUrl();
    [DllImport("__Internal")] private static extern int PB_CopyText(string text);
    [DllImport("__Internal")] private static extern int PB_Share(string text, string url);

    public static void Start(bool host, string code) { PB_Start(Prefix, host ? 1 : 0, code); }
    public static Status Current { get { return (Status)PB_GetStatus(); } }
    public static string Error { get { return PB_GetError(); } }
    public static void Send(string msg) { PB_Send(msg); }
    public static string Poll() { string s = PB_Poll(); return string.IsNullOrEmpty(s) ? null : s; }
    public static void Close() { PB_Close(); }
    public static string UrlParam(string name) { return PB_GetUrlParam(name); }
    public static string PageUrl() { return PB_PageUrl(); }
    public static bool CopyText(string text) { return PB_CopyText(text) == 1; }
    public static bool Share(string text, string url) { return PB_Share(text, url) == 1; }
#else
    public static void Start(bool host, string code) { }
    public static Status Current { get { return Status.Error; } }
    public static string Error { get { return "online play needs the web build"; } }
    public static void Send(string msg) { }
    public static string Poll() { return null; }
    public static void Close() { }
    public static string UrlParam(string name) { return ""; }
    public static string PageUrl() { return "http://localhost/"; }
    public static bool CopyText(string text) { GUIUtility.systemCopyBuffer = text; return true; }
    public static bool Share(string text, string url) { return false; }
#endif
}
