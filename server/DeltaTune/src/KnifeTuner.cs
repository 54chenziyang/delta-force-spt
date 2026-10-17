using System.Collections.Generic;
using System.Linq;
using SPTarkov.Common.Models.Logging;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Spt.Tables;

namespace DeltaTune;

/// <summary>
/// 把稀有度档位的伤害/攻速写进物品模板。
/// 伤害直接生效；攻速以 KnifeHitSlashRate/KnifeHitStabRate 作为数据通道，
/// 由客户端 DeltaMelee 补丁读取后缩放挥砍动画速度。
/// </summary>
public static class KnifeTuner
{
    public static void Apply(TemplateTable table, ModConfig cfg, ISptLogger<DeltaTuneMod> logger)
    {
        if (table?.Items == null)
        {
            logger?.Warning("[DeltaTune] 模板表为空，跳过刀具调整");
            return;
        }

        Dictionary<string, KnifeTierConfig> tierByName = cfg.Knife.Tiers
            .Where(t => !string.IsNullOrWhiteSpace(t.Name))
            .GroupBy(t => t.Name)
            .ToDictionary(g => g.Key, g => g.First());

        int applied = 0;
        int missing = 0;

        foreach (KeyValuePair<string, List<string>> pair in cfg.Knife.Assignments)
        {
            if (!tierByName.TryGetValue(pair.Key, out KnifeTierConfig tier))
            {
                logger?.Warning($"[DeltaTune] 档位 {pair.Key} 在 Tiers 里找不到定义，跳过");
                continue;
            }

            if (pair.Value == null)
            {
                continue;
            }

            foreach (string id in pair.Value)
            {
                if (!TryGetTemplate(table, id, out var tmpl))
                {
                    missing++;
                    continue;
                }

                WriteTemplate(tmpl, tier.SlashDamage, tier.StabDamage, tier.SlashPenetration, tier.StabPenetration, tier.SlashSpeedMultiplier, tier.StabSpeedMultiplier, tier.SlashRange, tier.StabRange, tier.HitRadius);
                applied++;
            }
        }

        // 单把刀覆盖优先级最高。
        foreach (KeyValuePair<string, KnifeOverride> pair in cfg.Knife.Overrides)
        {
            if (pair.Value == null || !TryGetTemplate(table, pair.Key, out var tmpl))
            {
                continue;
            }

            int slash = pair.Value.SlashDamage ?? tmpl.Properties?.KnifeHitSlashDam ?? 0;
            int stab = pair.Value.StabDamage ?? tmpl.Properties?.KnifeHitStabDam ?? 0;
            int slashPen = pair.Value.SlashPenetration ?? tmpl.Properties?.SlashPenetration ?? 1;
            int stabPen = pair.Value.StabPenetration ?? tmpl.Properties?.StabPenetration ?? 1;
            double slashSpeed = pair.Value.SlashSpeedMultiplier ?? tmpl.Properties?.KnifeHitSlashRate ?? 1.0;
            double stabSpeed = pair.Value.StabSpeedMultiplier ?? tmpl.Properties?.KnifeHitStabRate ?? 1.0;
            double slashRange = pair.Value.SlashRange ?? tmpl.Properties?.PrimaryDistance ?? 0.55;
            double stabRange = pair.Value.StabRange ?? tmpl.Properties?.SecondryDistance ?? 0.65;
            double hitRadius = pair.Value.HitRadius ?? tmpl.Properties?.KnifeHitRadius ?? 0.50;
            WriteTemplate(tmpl, slash, stab, slashPen, stabPen, slashSpeed, stabSpeed, slashRange, stabRange, hitRadius);
        }

        logger?.Info($"[DeltaTune] 刀具调整完成：{applied} 把已套用档位，{missing} 把未找到模板");
    }

    private static bool TryGetTemplate(TemplateTable table, string id, out SPTarkov.Server.Core.Models.Eft.Common.Tables.TemplateItem tmpl)
    {
        tmpl = null;
        if (string.IsNullOrWhiteSpace(id) || !MongoId.IsValidMongoId(id))
        {
            return false;
        }

        return table.Items.TryGetValue(new MongoId(id), out tmpl) && tmpl?.Properties != null;
    }

    private static void WriteTemplate(SPTarkov.Server.Core.Models.Eft.Common.Tables.TemplateItem tmpl, int slash, int stab, int slashPen, int stabPen, double slashSpeed, double stabSpeed, double slashRange, double stabRange, double hitRadius)
    {
        tmpl.Properties.KnifeHitSlashDam = slash;
        tmpl.Properties.KnifeHitStabDam = stab;
        tmpl.Properties.SlashPenetration = slashPen;
        tmpl.Properties.StabPenetration = stabPen;
        tmpl.Properties.KnifeHitSlashRate = slashSpeed;
        tmpl.Properties.KnifeHitStabRate = stabSpeed;
        tmpl.Properties.PrimaryDistance = slashRange;
        tmpl.Properties.SecondryDistance = stabRange;
        tmpl.Properties.KnifeHitRadius = hitRadius;
    }
}
