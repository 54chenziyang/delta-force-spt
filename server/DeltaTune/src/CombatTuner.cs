using System;
using System.Collections.Generic;
using SPTarkov.Common.Models.Logging;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Tables;

namespace DeltaTune;

/// <summary>
/// 战斗手感调整（对照三角洲）：
///   ① 腰射更散      -> 缩放武器模板 HipInnaccuracyGain（可选 RecolDispersion）
///   ② 远距离掉伤更多 -> 上调 globals.Ballistic.GlobalDamageDegradationCoefficient
///   ③ 血量略增      -> 缩放 globals 各部位 Default/Maximum
///   ④ 弹道伤害略降  -> 缩放全部弹药模板的 Damage
/// 全部为「乘倍率」的幂等操作（配置倍率 1.0 即不改动）。
/// </summary>
public static class CombatTuner
{
    /// <summary>弹药父类模板 id（547... 之下的 5485a8684bdc2da71d8b4567）。</summary>
    private const string AmmoParentId = "5485a8684bdc2da71d8b4567";

    public static void Apply(GlobalTable globals, TemplateTable tables, ModConfig cfg, ISptLogger<DeltaTuneMod> logger)
    {
        CombatConfig? combat = cfg?.Combat;
        if (combat == null)
        {
            return;
        }

        ApplyAmmoDamage(tables, combat, logger);
        ApplyWeaponDispersion(tables, combat, logger);
        ApplyBallistic(globals, combat, logger);
        ApplyHealth(globals, combat, logger);
    }

    // ---------------------------------------------------------------- ④ 弹药伤害

    private static void ApplyAmmoDamage(TemplateTable tables, CombatConfig combat, ISptLogger<DeltaTuneMod> logger)
    {
        double mult = combat.AmmoDamageMultiplier;
        if (tables?.Items == null || Math.Abs(mult - 1.0) < 1e-9)
        {
            return;
        }

        int count = 0;
        foreach (KeyValuePair<SPTarkov.Server.Core.Models.Common.MongoId, TemplateItem> kv in tables.Items)
        {
            TemplateItem? tmpl = kv.Value;
            if (tmpl?.Properties == null || tmpl.Parent == null)
            {
                continue;
            }

            if (tmpl.Parent.ToString() != AmmoParentId)
            {
                continue;
            }

            if (tmpl.Properties.Damage is double d && d > 0.0)
            {
                tmpl.Properties.Damage = d * mult;
                count++;
            }
        }

        logger?.Info($"[DeltaTune] 弹道伤害倍率 {mult:0.###}：已调整 {count} 种弹药");
    }

    // ---------------------------------------------------------------- ① 腰射/散布

    private static void ApplyWeaponDispersion(TemplateTable tables, CombatConfig combat, ISptLogger<DeltaTuneMod> logger)
    {
        double hip = combat.HipInnaccuracyMultiplier;
        double dispersion = combat.RecoilDispersionMultiplier;
        bool doHip = Math.Abs(hip - 1.0) > 1e-9;
        bool doDispersion = Math.Abs(dispersion - 1.0) > 1e-9;

        if (tables?.Items == null || (!doHip && !doDispersion))
        {
            return;
        }

        int hipCount = 0;
        int dispCount = 0;
        foreach (KeyValuePair<SPTarkov.Server.Core.Models.Common.MongoId, TemplateItem> kv in tables.Items)
        {
            TemplateItem? tmpl = kv.Value;
            if (tmpl?.Properties == null)
            {
                continue;
            }

            if (doHip && tmpl.Properties.HipInnaccuracyGain is double h && h > 0.0)
            {
                tmpl.Properties.HipInnaccuracyGain = h * hip;
                hipCount++;
            }

            if (doDispersion && tmpl.Properties.RecolDispersion is double r && r > 0.0)
            {
                tmpl.Properties.RecolDispersion = r * dispersion;
                dispCount++;
            }
        }

        logger?.Info($"[DeltaTune] 腰射失准倍率 {hip:0.###}：{hipCount} 件武器；散布倍率 {dispersion:0.###}：{dispCount} 件武器");
    }

    // ---------------------------------------------------------------- ② 距离衰减

    private static void ApplyBallistic(GlobalTable globals, CombatConfig combat, ISptLogger<DeltaTuneMod> logger)
    {
        Ballistic? ballistic = globals?.Configuration?.Ballistic;
        if (ballistic == null)
        {
            logger?.Warning("[DeltaTune] 未找到 globals.Ballistic，跳过距离衰减调整");
            return;
        }

        ballistic.GlobalDamageDegradationCoefficient = combat.GlobalDamageDegradationCoefficient;
        logger?.Info($"[DeltaTune] 全局距离衰减系数 = {combat.GlobalDamageDegradationCoefficient:0.###}（原版 0.5）");
    }

    // ---------------------------------------------------------------- ③ 血量

    private static void ApplyHealth(GlobalTable globals, CombatConfig combat, ISptLogger<DeltaTuneMod> logger)
    {
        double mult = combat.PlayerHealthMultiplier;
        if (Math.Abs(mult - 1.0) < 1e-9)
        {
            return;
        }

        BodyPartsSettings? parts = globals?.Configuration?.Health?.ProfileHealthSettings?.BodyPartsSettings;
        if (parts == null)
        {
            logger?.Warning("[DeltaTune] 未找到 globals.Health.ProfileHealthSettings.BodyPartsSettings，跳过血量调整");
            return;
        }

        Scale(parts.Chest);
        Scale(parts.Head);
        Scale(parts.LeftArm);
        Scale(parts.LeftLeg);
        Scale(parts.RightArm);
        Scale(parts.RightLeg);
        Scale(parts.Stomach);

        logger?.Info($"[DeltaTune] 玩家部位生命倍率 {mult:0.###} 已套用（胸 {parts.Chest?.Maximum:0.#} / 头 {parts.Head?.Maximum:0.#}）");

        void Scale(BodyPartsSetting? part)
        {
            if (part == null)
            {
                return;
            }

            part.Default *= mult;
            part.Maximum *= mult;
        }
    }
}
