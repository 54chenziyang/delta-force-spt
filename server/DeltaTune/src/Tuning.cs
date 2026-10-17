using System.Collections.Generic;
using System.Linq;

namespace DeltaTune;

/// <summary>
/// 默认数值表：刀具价值品质 6 档（普通/白、精良/绿、稀有/蓝、史诗/紫、传说/金、神话/红，对照三角洲近战武器） + 局内动态爆率档位。
/// config.json 里留空的项会用这里的默认值补齐。
/// </summary>
public static class Tuning
{
    public static List<KnifeTierConfig> DefaultTiers() => new List<KnifeTierConfig>
    {
        new KnifeTierConfig { Name = "T6", Rarity = "神话/红", Reference = "挽歌 S10",   SlashDamage = 70, StabDamage = 96, SlashPenetration = 44, StabPenetration = 50, SlashSpeedMultiplier = 1.50, StabSpeedMultiplier = 1.28, SlashRange = 1.20, StabRange = 1.30, HitRadius = 1.10 },
        new KnifeTierConfig { Name = "T5", Rarity = "传说/金", Reference = "暗星 S8",    SlashDamage = 63, StabDamage = 88, SlashPenetration = 38, StabPenetration = 44, SlashSpeedMultiplier = 1.43, StabSpeedMultiplier = 1.22, SlashRange = 1.05, StabRange = 1.15, HitRadius = 0.95 },
        new KnifeTierConfig { Name = "T4", Rarity = "史诗/紫", Reference = "北极星 S5",  SlashDamage = 58, StabDamage = 80, SlashPenetration = 33, StabPenetration = 39, SlashSpeedMultiplier = 1.36, StabSpeedMultiplier = 1.16, SlashRange = 0.90, StabRange = 1.00, HitRadius = 0.80 },
        new KnifeTierConfig { Name = "T3", Rarity = "稀有/蓝", Reference = "怜悯 S2",    SlashDamage = 54, StabDamage = 74, SlashPenetration = 28, StabPenetration = 34, SlashSpeedMultiplier = 1.30, StabSpeedMultiplier = 1.11, SlashRange = 0.75, StabRange = 0.85, HitRadius = 0.65 },
        new KnifeTierConfig { Name = "T2", Rarity = "精良/绿", Reference = "黑鹰 S0",    SlashDamage = 50, StabDamage = 68, SlashPenetration = 24, StabPenetration = 30, SlashSpeedMultiplier = 1.25, StabSpeedMultiplier = 1.06, SlashRange = 0.60, StabRange = 0.70, HitRadius = 0.50 },
        new KnifeTierConfig { Name = "T1", Rarity = "普通/白", Reference = "战术匕首 S0", SlashDamage = 46, StabDamage = 62, SlashPenetration = 20, StabPenetration = 26, SlashSpeedMultiplier = 1.20, StabSpeedMultiplier = 1.02, SlashRange = 0.45, StabRange = 0.55, HitRadius = 0.35 },
    };

    public static Dictionary<string, List<string>> DefaultAssignments() => new Dictionary<string, List<string>>
    {
        // 按原版武器价值（挥砍/突刺伤害）从高到低分配 6 档。
        ["T6"] = new List<string>
        {
            "63495c500c297e20065a08b1", // weapon_scythe          293/415
            "679ba90d269ddfea47012159", // weapon_labris_axe      166/199
            "6087e570b998180e9f76dc24", // weapon_hultafors_db5000 103/125
        },
        ["T5"] = new List<string>
        {
            "664a5428d5e33a713b622379", // weapon_united_cutlery_m48_kukri 38/57
            "601948682627df266209af05", // weapon_usvr_taiga      33/55
            "5bffe7930db834001b734a39", // weapon_strider_crash_axe 31/52
            "5bffdd7e0db834001b734a1a", // weapon_miller_bros_m-2 34/51
        },
        ["T4"] = new List<string>
        {
            "5bc9c1e2d4351e00367fbcf0", // weapon_valuable_wyccaxe 28/49
            "6540d2162ae6d96b540afcaf", // weapon_gf_crowbar      26/43
            "5bead2e00db834001c062938", // weapon_voentorg_mpl-50 24/43
            "670ad7f1ad195290cd00da7a", // weapon_izhmash_6x5     31/42
            "63920105a83e15700a00f168", // weapon_kiba_arms_axe   32/40
            "5c010e350db83400232feec7", // weapon_ontario_sp-8    30/40
        },
        ["T3"] = new List<string>
        {
            "674d90b55704568fe60bc8f5", // weapon_semin_push_dagger 24/39
            "5c0126f40db834002a125382", // weapon_red_rebel_axe   29/35
            "5c012ffc0db834001d23f03f", // weapon_utility_axe     32/35
            "57cd379a24597778e7682ecf", // weapon_kiba_arms_axe   25/35
        },
        ["T2"] = new List<string>
        {
            "5c07df7f0db834001b73588a", // weapon_gf_crowbar      25/34
            "5fc64ea372b0dd78d51159dc", // weapon_cultist_damask  25/32
            "65ca457b4aafb5d7fc0dcb5d", // weapon_united_cutlery_m48_kukri 27/32
            "5bffdc370db834001d23eca8", // weapon_izhmash_6x5     24/31
        },
        ["T1"] = new List<string>
        {
            "54491bb74bdc2d09088b4567", // ER Bayonet             25/27
            "57e26fc7245977162a14b800", // weapon_bars_a-2607_95x18 19/25
            "57e26ea924597715ca604a09", // weapon_bars_a-2607_damask 18/25
        },
    };

    public static List<GearTierConfig> DefaultGearTiers() => new List<GearTierConfig>
    {
        new GearTierConfig { Name = "普通", MinGearValue = 0, Multiplier = 1.0, GoldBonus = 0.0, RedBonus = 0.0 },
        new GearTierConfig { Name = "机密", MinGearValue = 150000, Multiplier = 1.6, GoldBonus = 0.35, RedBonus = 0.25 },
        new GearTierConfig { Name = "绝密", MinGearValue = 400000, Multiplier = 2.3, GoldBonus = 0.60, RedBonus = 0.50 },
    };

    /// <summary>
    /// Bot 血量八档：常规封顶 500，仅少数「肉坦首领」保留 800，其余按战力（原版真实总血量）依次下降，
    /// 且刻意拉大档间差距。每档写明目标「总血量」（七部位 max 之和），引擎按各 bot 原版总量等比缩放，幂等。
    /// 肉坦首领（800）取自原版血量最高的一群：Zryachiy 1655 / TagillaAgro 1305 / Kaban 1300 / Sanitar 1270 / Tagilla 1220。
    /// </summary>
    public static List<BotHealthTierConfig> DefaultBotHealthTiers() => new List<BotHealthTierConfig>
    {
        new BotHealthTierConfig
        {
            Name = "肉坦首领", TotalHealth = 800,
            BotTypes = new List<string>
            {
                "bosszryachiy", "ravangezryachiyevent", "bossboar", "bosstagillaagro",
                "bosssanitar", "bosstagilla",
            },
        },
        new BotHealthTierConfig
        {
            Name = "重装首领", TotalHealth = 500,
            BotTypes = new List<string>
            {
                "bossknight", "bossgluhar", "bosskolontay", "bosskillaagro", "bosspartisan",
            },
        },
        new BotHealthTierConfig
        {
            Name = "普通首领", TotalHealth = 440,
            BotTypes = new List<string>
            {
                "bosskilla", "sectantpriest", "bosskojaniy", "bossbully", "bossboarsniper",
            },
        },
        new BotHealthTierConfig
        {
            Name = "精英护卫", TotalHealth = 200,
            BotTypes = new List<string>
            {
                "followerbigpipe", "followerbirdeye", "followerboar", "followerboarclose1", "followerboarclose2",
                "followerbully", "followergluharassault", "followergluharscout", "followergluharsecurity",
                "followerkojaniy", "followerkolontayassault", "followerkolontaysecurity", "followersanitar",
                "followerzryachiy", "tagillahelperagro", "exusec", "shooterbtr", "cursedassault",
                "sectantoni", "sectantpredvestnik", "sectantprizrak", "sectantwarrior",
            },
        },
        new BotHealthTierConfig
        {
            Name = "精锐", TotalHealth = 150,
            BotTypes = new List<string>
            {
                "pmcbear", "pmcusec", "pmcbot", "bear", "usec", "peacemaker", "skier",
            },
        },
        new BotHealthTierConfig
        {
            Name = "常规", TotalHealth = 100,
            BotTypes = new List<string>
            {
                "assault", "marksman", "crazyassaultevent", "arenafighterevent", "gifter",
            },
        },
        new BotHealthTierConfig
        {
            Name = "游荡", TotalHealth = 100,
            BotTypes = new List<string>
            {
                "spiritspring", "spiritwinter",
            },
        },
        new BotHealthTierConfig
        {
            Name = "感染", TotalHealth = 100,
            BotTypes = new List<string>
            {
                "infectedassault", "infectedcivil", "infectedlaborant", "infectedpmc", "infectedtagilla",
            },
        },
    };

    /// <summary>用默认值补齐 config 里缺失的部分，并清洗非法数据。</summary>
    public static void EnsureDefaults(ModConfig cfg)
    {
        if (cfg.Knife == null)
        {
            cfg.Knife = new KnifeConfig();
        }

        if (cfg.Loot == null)
        {
            cfg.Loot = new LootConfig();
        }

        cfg.Combat ??= new CombatConfig();
        cfg.BotHealth ??= new BotHealthConfig();
        cfg.BotScatter ??= new BotScatterConfig();

        if (cfg.BotHealth.Tiers == null || cfg.BotHealth.Tiers.Count == 0)
        {
            cfg.BotHealth.Tiers = DefaultBotHealthTiers();
        }
        if (cfg.Knife.Tiers == null || cfg.Knife.Tiers.Count == 0)
        {
            cfg.Knife.Tiers = DefaultTiers();
        }

        if (cfg.Knife.Assignments == null || cfg.Knife.Assignments.Count == 0)
        {
            cfg.Knife.Assignments = DefaultAssignments();
        }

        cfg.Knife.Overrides ??= new Dictionary<string, KnifeOverride>();

        if (cfg.Loot.GearTiers == null || cfg.Loot.GearTiers.Count == 0)
        {
            cfg.Loot.GearTiers = DefaultGearTiers();
        }
        else
        {
            cfg.Loot.GearTiers = cfg.Loot.GearTiers.OrderBy(t => t.MinGearValue).ToList();
        }

        // 保证至少有一个兜底档（战备值 0 起）。
        if (cfg.Loot.GearTiers.Count > 0 && cfg.Loot.GearTiers[0].MinGearValue > 0)
        {
            cfg.Loot.GearTiers.Insert(0, new GearTierConfig { Name = "普通", MinGearValue = 0, Multiplier = 1.0 });
        }
    }
}
