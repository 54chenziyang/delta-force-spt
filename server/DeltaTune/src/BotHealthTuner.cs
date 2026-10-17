using System;
using System.Collections.Generic;
using SPTarkov.Common.Models.Logging;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Tables;

namespace DeltaTune;

/// <summary>
/// Bot 血量分档（对照三角洲）：把每个 bot 的七部位血量按「目标总血量」等比缩放。
///   首领 800（封顶） / 精英 620 / 精锐 480 / 常规 340 / 游荡 220 / 感染 120。
/// 缩放因子 = 目标总量 ÷ 该难度档原始 max 总量，同时套用到 Min/Max，天然幂等（已达标时因子=1）。
/// </summary>
public static class BotHealthTuner
{
    public static void Apply(BotTable bots, BotHealthConfig? cfg, ISptLogger<DeltaTuneMod> logger)
    {
        if (bots?.Types == null || cfg?.Tiers == null || cfg.Tiers.Count == 0)
        {
            return;
        }

        // 建索引：忽略大小写（types/*.json 文件名全小写，config 里可能写成 camelCase）。
        Dictionary<string, BotType> index = new Dictionary<string, BotType>(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, BotType> kv in bots.Types)
        {
            if (kv.Key == null || kv.Value == null)
            {
                continue;
            }

            index[kv.Key] = kv.Value;
        }

        int scaled = 0;
        foreach (BotHealthTierConfig tier in cfg.Tiers)
        {
            if (tier?.BotTypes == null || tier.TotalHealth <= 0.0)
            {
                continue;
            }

            int tierCount = 0;
            foreach (string typeName in tier.BotTypes)
            {
                if (string.IsNullOrWhiteSpace(typeName))
                {
                    continue;
                }

                if (!index.TryGetValue(typeName.Trim(), out BotType? botType))
                {
                    continue;
                }

                if (ScaleToTotal(botType, tier.TotalHealth))
                {
                    tierCount++;
                }
            }

            scaled += tierCount;
            logger?.Info($"[DeltaTune] Bot 血量档「{tier.Name}」目标总量 {tier.TotalHealth:0}：已调整 {tierCount} 种");
        }

        logger?.Info($"[DeltaTune] Bot 血量分档完成（共 {cfg.Tiers.Count} 档 / {scaled} 个类型）");
    }

    /// <summary>把单个 bot 的每个难度档血量等比缩放到目标总量，返回是否有改动。</summary>
    private static bool ScaleToTotal(BotType botType, double targetTotal)
    {
        BotTypeHealth? health = botType?.BotHealth;
        if (health?.BodyParts == null)
        {
            return false;
        }

        bool any = false;
        foreach (BodyPart part in health.BodyParts)
        {
            if (part == null)
            {
                continue;
            }

            double original = Sum(part);
            if (original <= 0.0)
            {
                continue;
            }

            any = true;
            double factor = targetTotal / original;
            if (Math.Abs(factor - 1.0) < 1e-9)
            {
                continue;
            }

            Scale(part.Chest, factor);
            Scale(part.Head, factor);
            Scale(part.LeftArm, factor);
            Scale(part.LeftLeg, factor);
            Scale(part.RightArm, factor);
            Scale(part.RightLeg, factor);
            Scale(part.Stomach, factor);
        }

        return any;
    }

    private static double Sum(BodyPart part) =>
        Max(part.Chest) + Max(part.Head) + Max(part.LeftArm) + Max(part.LeftLeg) +
        Max(part.RightArm) + Max(part.RightLeg) + Max(part.Stomach);

    private static double Max(MinMax<double>? mm) => mm?.Max ?? 0.0;

    private static void Scale(MinMax<double>? mm, double factor)
    {
        if (mm == null)
        {
            return;
        }

        mm.Min *= factor;
        mm.Max *= factor;
    }
}
