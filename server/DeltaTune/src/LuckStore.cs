using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace DeltaTune;

/// <summary>
/// 跨局持久化的「幸运值」存储（按 profile/sessionId 分键）。
/// 战损、击杀会累积幸运值，作用于后续局的爆率。
/// </summary>
public static class LuckStore
{
    private static readonly object Gate = new object();
    private static Dictionary<string, double>? _data;
    private static bool _dirty;

    private static string FilePath =>
        Path.Combine(
            Path.GetDirectoryName(typeof(LuckStore).Assembly.Location) ?? AppContext.BaseDirectory,
            "deltatune_luck.json");

    private static void EnsureLoaded()
    {
        if (_data != null)
        {
            return;
        }

        lock (Gate)
        {
            if (_data != null)
            {
                return;
            }

            try
            {
                if (File.Exists(FilePath))
                {
                    string json = File.ReadAllText(FilePath);
                    _data = JsonSerializer.Deserialize<Dictionary<string, double>>(json)
                        ?? new Dictionary<string, double>();
                }
                else
                {
                    _data = new Dictionary<string, double>();
                }
            }
            catch
            {
                _data = new Dictionary<string, double>();
            }
        }
    }

    public static double Get(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return 0.0;
        }

        EnsureLoaded();
        lock (Gate)
        {
            return _data!.TryGetValue(key, out double v) ? v : 0.0;
        }
    }

    public static void Set(string key, double value)
    {
        if (string.IsNullOrEmpty(key))
        {
            return;
        }

        EnsureLoaded();
        lock (Gate)
        {
            _data![key] = value;
            _dirty = true;
        }
    }

    public static void Save()
    {
        EnsureLoaded();
        lock (Gate)
        {
            if (!_dirty)
            {
                return;
            }

            try
            {
                string json = JsonSerializer.Serialize(_data, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(FilePath, json);
                _dirty = false;
            }
            catch
            {
                // 持久化失败不影响本局运行。
            }
        }
    }
}
