using System.Collections.Generic;

namespace DeltaTune;

public class ModConfig
{
    public KnifeConfig Knife { get; set; } = new KnifeConfig();

    public LootConfig Loot { get; set; } = new LootConfig();

    public CombatConfig Combat { get; set; } = new CombatConfig();

    public BotHealthConfig BotHealth { get; set; } = new BotHealthConfig();

    public BotScatterConfig BotScatter { get; set; } = new BotScatterConfig();
}

/// <summary>Bot 弹道散射（对照三角洲：降低人机命中率，不再激光枪）。</summary>
public class BotScatterConfig
{
    public bool Enabled { get; set; } = true;

    /// <summary>散射倍率（>1 = 人机更不准；1.0 = 不改动）。</summary>
    public double Multiplier { get; set; } = 2.0;

    /// <summary>参与缩放的散射字段名（留空 = 使用内置「弹道散射」字段集）。仅接受「越大越散」的字段。</summary>
    public List<string> Fields { get; set; } = new List<string>();

    /// <summary>只作用于这些 bot 类型（留空 = 全部；对应 bots/types/*.json 文件名，忽略大小写）。</summary>
    public List<string> BotTypes { get; set; } = new List<string>();
}

/// <summary>Bot 血量分档（对照三角洲：常规封顶 500，仅肉坦首领 800，其余按战力依次下降，档间差距拉大）。</summary>
public class BotHealthConfig
{
    public bool Enabled { get; set; } = true;

    /// <summary>血量档位表（每档给出目标总血量 + 该档包含的 bot 类型）。</summary>
    public List<BotHealthTierConfig> Tiers { get; set; } = new List<BotHealthTierConfig>();
}

public class BotHealthTierConfig
{
    public string Name { get; set; } = "";

    /// <summary>本档每个 bot 的目标「总血量」（七部位 max 之和）。引擎会按此等比缩放各部位。</summary>
    public double TotalHealth { get; set; }

    /// <summary>本档包含的 bot 类型（对应 bots/types/*.json 的文件名，忽略大小写）。</summary>
    public List<string> BotTypes { get; set; } = new List<string>();
}

/// <summary>战斗手感调整（对照三角洲：腰射更散、远距离掉伤更多、血量略增、弹道伤害略降）。</summary>
public class CombatConfig
{
    public bool Enabled { get; set; } = true;

    /// <summary>腰射失准增益倍率（原版武器均为 0.16；>1 = 腰射更散）。</summary>
    public double HipInnaccuracyMultiplier { get; set; } = 2.0;

    /// <summary>每枪基础散布倍率（原版 RecolDispersion；1.0 = 不改动，>1 = 连射更散）。</summary>
    public double RecoilDispersionMultiplier { get; set; } = 1.0;

    /// <summary>全局弹道距离衰减系数（原版 0.5；值越大 = 远距离掉伤越多）。</summary>
    public double GlobalDamageDegradationCoefficient { get; set; } = 0.7;

    /// <summary>玩家各部位最大生命倍率（原版胸 85 / 头 35 / 臂 60 / 腿 65 / 腹 70）。</summary>
    public double PlayerHealthMultiplier { get; set; } = 1.15;

    /// <summary>弹药基础伤害倍率（对所有弹药模板的 Damage 生效）。</summary>
    public double AmmoDamageMultiplier { get; set; } = 0.9;
}

public class KnifeConfig
{
    public bool Enabled { get; set; } = true;

    /// <summary>价值品质档位表（伤害 + 轻/重击攻速倍率），共 6 档（白/绿/蓝/紫/金/红），数值对照三角洲各近战武器。</summary>
    public List<KnifeTierConfig> Tiers { get; set; } = new List<KnifeTierConfig>();

    /// <summary>档位名 -> 该档位包含的刀具模板 id 列表。</summary>
    public Dictionary<string, List<string>> Assignments { get; set; } = new Dictionary<string, List<string>>();

    /// <summary>单把刀的强制覆盖（模板 id -> 数值），优先级最高。</summary>
    public Dictionary<string, KnifeOverride> Overrides { get; set; } = new Dictionary<string, KnifeOverride>();
}

public class KnifeTierConfig
{
    public string Name { get; set; } = "";

    public string Rarity { get; set; } = "";

    /// <summary>三角洲参考刀（仅用于日志/说明）。</summary>
    public string Reference { get; set; } = "";

    public int SlashDamage { get; set; }

    public int StabDamage { get; set; }

    /// <summary>挥砍破甲穿透力（原版刀具普遍只有 1，打有甲目标几乎全被吸收）。</summary>
    public int SlashPenetration { get; set; }

    /// <summary>突刺破甲穿透力。</summary>
    public int StabPenetration { get; set; }

    /// <summary>轻击（挥砍）攻速倍率：1.0 为基准，>1 更快，<1 更慢。</summary>
    public double SlashSpeedMultiplier { get; set; } = 1.0;

    /// <summary>重击（突刺）攻速倍率：1.0 为基准，>1 更快，<1 更慢。</summary>
    public double StabSpeedMultiplier { get; set; } = 1.0;

    /// <summary>轻击（挥砍）攻击距离（米），写入模板 PrimaryDistance。</summary>
    public double SlashRange { get; set; } = 0.55;

    /// <summary>重击（突刺）攻击距离（米），写入模板 SecondryDistance。</summary>
    public double StabRange { get; set; } = 0.65;

    /// <summary>近战命中判定半径（米），写入模板 KnifeHitRadius。</summary>
    public double HitRadius { get; set; } = 0.50;
}

public class KnifeOverride
{
    public int? SlashDamage { get; set; }

    public int? StabDamage { get; set; }

    public int? SlashPenetration { get; set; }

    public int? StabPenetration { get; set; }

    public double? SlashSpeedMultiplier { get; set; }

    public double? StabSpeedMultiplier { get; set; }

    public double? SlashRange { get; set; }

    public double? StabRange { get; set; }

    public double? HitRadius { get; set; }
}

public class LootConfig
{
    public bool Enabled { get; set; } = true;

    /// <summary>局内按战备值定档（普通/机密/绝密）。</summary>
    public List<GearTierConfig> GearTiers { get; set; } = new List<GearTierConfig>();

    /// <summary>物品价格 >= 此值视为「金」档藏品。</summary>
    public double GoldPriceThreshold { get; set; } = 150000;

    /// <summary>物品价格 >= 此值视为「红」档藏品。</summary>
    public double RedPriceThreshold { get; set; } = 500000;

    /// <summary>稀有物品权重倍率上限（防止爆率失控）。</summary>
    public double MaxRareWeightFactor { get; set; } = 6.0;

    /// <summary>幸运值对整体爆率倍率的影响系数。</summary>
    public double LooseLuckFactor { get; set; } = 0.15;

    /// <summary>幸运值对金/红藏品权重的影响系数。</summary>
    public double RareLuckFactor { get; set; } = 0.5;

    /// <summary>每击杀 1 人累计的幸运值。</summary>
    public double KillLuck { get; set; } = 0.10;

    /// <summary>每损失 100 万卢布战备累计的幸运值。</summary>
    public double LossLuckPerMillion { get; set; } = 0.5;

    /// <summary>幸运值上限。</summary>
    public double LuckCap { get; set; } = 3.0;

    /// <summary>每局结束后幸运值衰减比例（0.2 = 每局先衰减 20%）。</summary>
    public double LuckDecayPerRaid { get; set; } = 0.2;
}

public class GearTierConfig
{
    public string Name { get; set; } = "";

    /// <summary>达到该战备值即进入本档（取满足条件中最高的档）。</summary>
    public double MinGearValue { get; set; }

    /// <summary>整体爆率倍率（散落点数量 + 容器内件数）。</summary>
    public double Multiplier { get; set; } = 1.0;

    /// <summary>本档对「金」藏品权重的加成。</summary>
    public double GoldBonus { get; set; }

    /// <summary>本档对「红」藏品权重的加成。</summary>
    public double RedBonus { get; set; }
}
