using System;
using System.Collections.Generic;
using System.Reflection;
using SPTarkov.Common.Models.Logging;
using SPTarkov.Server.Core.Models.Eft.Bot.GlobalSettings;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Tables;

namespace DeltaTune;

/// <summary>
/// Bot 弹道散射（对照三角洲：人机不再激光枪）。
/// 把每个 bot 各难度档的「散射」字段统一放大，倍率 >1 = 更难打中玩家。
/// 只作用于「值越大 = 散布越大」的字段；收敛速度 / 角速度等「值越大 = 越准」的字段一律不动。
/// </summary>
public static class BotScatterTuner
{
    /// <summary>内置的弹道散射字段集（均为「越大越散」）。</summary>
    private static readonly string[] DefaultFields =
    {
        "MinScatter", "WorkingScatter", "MaxScatter",
        "AmplitudeFactor", "FromShot", "LayFactor", "PoseChangeCoef",
    };

    private static readonly Dictionary<string, PropertyInfo?> PropertyCache =
        new Dictionary<string, PropertyInfo?>(StringComparer.OrdinalIgnoreCase);

    public static void Apply(BotTable bots, BotScatterConfig? cfg, ISptLogger<DeltaTuneMod> logger)
    {
        if (bots?.Types == null || cfg == null || !cfg.Enabled)
        {
            return;
        }

        double mult = cfg.Multiplier;
        if (mult <= 0.0 || Math.Abs(mult - 1.0) < 1e-9)
        {
            return;
        }

        string[] fields = (cfg.Fields != null && cfg.Fields.Count > 0)
            ? cfg.Fields.ToArray()
            : DefaultFields;

        HashSet<string>? typeFilter = null;
        if (cfg.BotTypes != null && cfg.BotTypes.Count > 0)
        {
            typeFilter = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string t in cfg.BotTypes)
            {
                if (!string.IsNullOrWhiteSpace(t))
                {
                    typeFilter.Add(t.Trim());
                }
            }
        }

        int botCount = 0;
        int fieldCount = 0;
        foreach (KeyValuePair<string, BotType> kv in bots.Types)
        {
            if (kv.Key == null || kv.Value == null)
            {
                continue;
            }

            if (typeFilter != null && !typeFilter.Contains(kv.Key))
            {
                continue;
            }

            if (ScaleBot(kv.Value, fields, mult, ref fieldCount))
            {
                botCount++;
            }
        }

        logger?.Info($"[DeltaTune] Bot 弹道散射已调整：{botCount} 种人机 × 倍率 {mult:0.##}（共 {fieldCount} 处字段；字段：{string.Join(", ", fields)}）");
    }

    /// <summary>把单个 bot 的所有难度档的散射字段放大，返回是否有改动。</summary>
    private static bool ScaleBot(BotType botType, string[] fields, double mult, ref int fieldCount)
    {
        Dictionary<string, DifficultyCategories>? difficulties = botType.BotDifficulty;
        if (difficulties == null)
        {
            return false;
        }

        bool any = false;
        foreach (KeyValuePair<string, DifficultyCategories> kv in difficulties)
        {
            BotGlobalsScatteringSettings? scatter = kv.Value?.Scattering;
            if (scatter == null)
            {
                continue;
            }

            foreach (string field in fields)
            {
                if (ScaleField(scatter, field, mult))
                {
                    fieldCount++;
                    any = true;
                }
            }
        }

        return any;
    }

    private static bool ScaleField(BotGlobalsScatteringSettings scatter, string name, double mult)
    {
        PropertyInfo? prop = ResolveProperty(name);
        if (prop == null)
        {
            return false;
        }

        if (prop.GetValue(scatter) is not float current)
        {
            return false;
        }

        prop.SetValue(scatter, (float)(current * mult));
        return true;
    }

    private static PropertyInfo? ResolveProperty(string name)
    {
        if (PropertyCache.TryGetValue(name, out PropertyInfo? cached))
        {
            return cached;
        }

        PropertyInfo? prop = typeof(BotGlobalsScatteringSettings)
            .GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

        if (prop != null && (!prop.CanRead || !prop.CanWrite || prop.PropertyType != typeof(float?)))
        {
            prop = null;
        }

        PropertyCache[name] = prop;
        return prop;
    }
}
