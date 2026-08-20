using System;
using System.Diagnostics;

public static class Logger
{
    private static string Tag = "App";

    public static void SetTag(string tag)
        => Tag = tag;

    public static void Log(string message)
        => Debug.WriteLine($"[{Tag}] {message}");

    public static void Log(string message, params object[] args)
        => Debug.WriteLine($"[{Tag}] " + string.Format(message, args));

    public static void Warn(string message)
        => Debug.WriteLine($"[{Tag}] [WARN] {message}");

    public static void Warn(string message, params object[] args)
        => Debug.WriteLine($"[{Tag}] [WARN] " + string.Format(message, args));

    public static void Error(string message)
        => Debug.WriteLine($"[{Tag}] [ERROR] {message}");

    public static void Error(string message, params object[] args)
        => Debug.WriteLine($"[{Tag}] [ERROR] " + string.Format(message, args));
}