using System;
using System.Collections.Generic;
using System.Reflection;
using SPTarkov.Reflection.Patching;
using SPTarkov.Server.Core.Generators.Loot;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Match;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Services.InRaid;
using SPTarkov.Server.Core.Utils.Collections;

namespace DeltaTune;

/// <summary>开局：按局内战备值定档（普通/机密/绝密），写入本局运行态。</summary>
internal sealed class StartRaidPatch : AbstractPatch
{
    public StartRaidPatch() : base("DeltaTune.StartRaid")
    {
    }

    protected override MethodBase GetTargetMethod() =>
        typeof(LocationLifecycleService).GetMethod(
            "StartLocalRaidAsync",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

    [PatchPrefix]
    private static void Prefix(object[] __args)
    {
        try
        {
            RaidState.Reset();

            if (__args == null || __args.Length < 2)
            {
                return;
            }

            if (__args[0] is not MongoId sessionId)
            {
                return;
            }

            StartLocalRaidRequestData? request = __args[1] as StartLocalRaidRequestData;
            ModConfig? cfg = PatchContext.Config;
            if (cfg?.Loot == null || !cfg.Loot.Enabled)
            {
                return;
            }

            bool isPmc = request != null
                && string.Equals(request.PlayerSide, "pmc", StringComparison.OrdinalIgnoreCase);

            var pmc = PatchContext.Profiles?.GetPmcProfile(sessionId);

            RaidState.Active = true;
            RaidState.SessionId = sessionId.ToString();

            if (pmc == null)
            {
                return;
            }

            double gear = isPmc ? PatchContext.ComputeGearValue(pmc) : 0.0;
            double luck = isPmc ? LuckStore.Get(RaidState.SessionId) : 0.0;

            GearTierConfig tier = cfg.Loot.GearTiers[0];
            foreach (GearTierConfig t in cfg.Loot.GearTiers)
            {
                if (gear >= t.MinGearValue && t.MinGearValue >= tier.MinGearValue)
                {
                    tier = t;
                }
            }

            double gearMult = isPmc ? tier.Multiplier : 1.0;
            double general = gearMult * (1.0 + luck * cfg.Loot.LooseLuckFactor);

            RaidState.TierName = isPmc ? tier.Name : "普通";
            RaidState.GearValue = gear;
            RaidState.Luck = luck;
            RaidState.LooseMultiplier = general;
            RaidState.StaticMultiplier = general;
            RaidState.GoldFactor = isPmc
                ? Math.Min(cfg.Loot.MaxRareWeightFactor, 1.0 + tier.GoldBonus + luck * cfg.Loot.RareLuckFactor)
                : 1.0;
            RaidState.RedFactor = isPmc
                ? Math.Min(cfg.Loot.MaxRareWeightFactor, 1.0 + tier.RedBonus + luck * cfg.Loot.RareLuckFactor)
                : 1.0;
            RaidState.StartKills = PatchContext.GetLifetimeKills(pmc);
            RaidState.StartVictims = PatchContext.CountVictims(pmc);

            PatchContext.Logger?.Info(
                $"[DeltaTune] 本局档位={RaidState.TierName} 战备={gear:F0} 幸运={luck:F2} " +
                $"倍率={general:F2} 金={RaidState.GoldFactor:F2} 红={RaidState.RedFactor:F2}");
        }
        catch (Exception ex)
        {
            PatchContext.Logger?.Warning("[DeltaTune] StartRaid 前置补丁异常: " + ex.Message);
        }
    }
}

/// <summary>散落点数量倍率。</summary>
internal sealed class LooseMultiplierPatch : AbstractPatch
{
    public LooseMultiplierPatch() : base("DeltaTune.LooseMultiplier")
    {
    }

    protected override MethodBase GetTargetMethod() =>
        typeof(LocationLootGenerator).GetMethod(
            "GetLooseLootMultiplierForLocation",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

    [PatchPostfix]
    private static void Postfix(ref double __result)
    {
        if (RaidState.Active)
        {
            __result *= RaidState.LooseMultiplier;
        }
    }
}

/// <summary>容器内件数倍率。</summary>
internal sealed class StaticMultiplierPatch : AbstractPatch
{
    public StaticMultiplierPatch() : base("DeltaTune.StaticMultiplier")
    {
    }

    protected override MethodBase GetTargetMethod() =>
        typeof(LocationLootGenerator).GetMethod(
            "GetStaticLootMultiplierForLocation",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

    [PatchPostfix]
    private static void Postfix(ref double __result)
    {
        if (RaidState.Active)
        {
            __result *= RaidState.StaticMultiplier;
        }
    }
}

/// <summary>容器抽奖池：提高金/红藏品被抽中的权重。</summary>
internal sealed class ContainerLootPatch : AbstractPatch
{
    public ContainerLootPatch() : base("DeltaTune.ContainerLoot")
    {
    }

    protected override MethodBase GetTargetMethod() =>
        typeof(LocationLootGenerator).GetMethod(
            "GetPossibleLootItemsForContainer",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

    [PatchPostfix]
    private static void Postfix(ref ProbabilityObjectArray<MongoId, float?> __result)
    {
        try
        {
            if (!RaidState.Active || __result == null)
            {
                return;
            }

            foreach (ProbabilityObject<MongoId, float?> po in __result)
            {
                if (po?.RelativeProbability == null)
                {
                    continue;
                }

                double f = PatchContext.FactorForPrice(PatchContext.PriceOfMongo(po.Key));
                if (f > 1.0)
                {
                    po.RelativeProbability = po.RelativeProbability.Value * f;
                }
            }
        }
        catch (Exception ex)
        {
            PatchContext.Logger?.Warning("[DeltaTune] 容器抽奖池加权异常: " + ex.Message);
        }
    }
}

/// <summary>散落点物品分布：提高金/红藏品被抽中的权重（幂等，跨局复用同一实例）。</summary>
internal sealed class DynamicLootPatch : AbstractPatch
{
    public DynamicLootPatch() : base("DeltaTune.DynamicLoot")
    {
    }

    protected override MethodBase GetTargetMethod() =>
        typeof(LocationLootGenerator).GetMethod(
            "GenerateDynamicLoot",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

    [PatchPrefix]
    private static void Prefix(object[] __args)
    {
        try
        {
            if (!RaidState.Active || __args == null || __args.Length < 1)
            {
                return;
            }

            if (__args[0] is LooseLoot dist)
            {
                DynamicLootWeights.Reweight(dist);
            }
        }
        catch (Exception ex)
        {
            PatchContext.Logger?.Warning("[DeltaTune] 散落点加权异常: " + ex.Message);
        }
    }
}

/// <summary>结算：按本局击杀数 + 战损累计持久幸运值，作用于后续局。</summary>
internal sealed class EndRaidPatch : AbstractPatch
{
    public EndRaidPatch() : base("DeltaTune.EndRaid")
    {
    }

    protected override MethodBase GetTargetMethod() =>
        typeof(LocationLifecycleService).GetMethod(
            "EndLocalRaidAsync",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

    [PatchPrefix]
    private static void Prefix(object[] __args)
    {
        try
        {
            ModConfig? cfg = PatchContext.Config;
            if (cfg?.Loot == null || !cfg.Loot.Enabled)
            {
                RaidState.Reset();
                return;
            }

            if (__args == null || __args.Length < 2)
            {
                RaidState.Reset();
                return;
            }

            if (__args[0] is not MongoId sessionId)
            {
                RaidState.Reset();
                return;
            }

            EndLocalRaidRequestData? request = __args[1] as EndLocalRaidRequestData;
            var profile = request?.Results?.Profile;
            if (profile == null)
            {
                RaidState.Reset();
                return;
            }

            double kills = Math.Max(0.0, PatchContext.GetLifetimeKills(profile) - RaidState.StartKills);
            int victimDelta = PatchContext.CountVictims(profile) - RaidState.StartVictims;
            if (victimDelta > kills)
            {
                kills = victimDelta;
            }

            ExitStatus? result = request!.Results!.Result;
            bool died = result == ExitStatus.KILLED || result == ExitStatus.MISSINGINACTION;
            double loss = died ? RaidState.GearValue : 0.0;

            string key = sessionId.ToString();
            double luck = LuckStore.Get(key);
            luck = (luck * (1.0 - cfg.Loot.LuckDecayPerRaid))
                 + (kills * cfg.Loot.KillLuck)
                 + ((loss / 1000000.0) * cfg.Loot.LossLuckPerMillion);
            luck = Math.Max(0.0, Math.Min(cfg.Loot.LuckCap, luck));

            LuckStore.Set(key, luck);
            LuckStore.Save();

            PatchContext.Logger?.Info(
                $"[DeltaTune] 本局结算 击杀={kills:F0} 战损={loss:F0} 幸运值→{luck:F2}");
        }
        catch (Exception ex)
        {
            PatchContext.Logger?.Warning("[DeltaTune] EndRaid 前置补丁异常: " + ex.Message);
        }
        finally
        {
            RaidState.Reset();
        }
    }
}

/// <summary>对散落点物品分布的权重做幂等加权重算。</summary>
internal static class DynamicLootWeights
{
    // LooseLootItemDistribution 是 record（值相等），但我们在原地改 RelativeProbability；
    // 用引用相等比较器，保证同一次生成过程中键稳定。
    private static readonly Dictionary<LooseLootItemDistribution, double> Originals =
        new Dictionary<LooseLootItemDistribution, double>(ReferenceEqualityComparer.Instance);

    private const int MaxEntries = 200000;

    public static void Reweight(LooseLoot dist)
    {
        if (dist?.Spawnpoints == null)
        {
            return;
        }

        if (Originals.Count > MaxEntries)
        {
            Originals.Clear();
        }

        foreach (Spawnpoint sp in dist.Spawnpoints)
        {
            if (sp?.ItemDistribution == null || sp.Template?.Items == null)
            {
                continue;
            }

            Dictionary<string, MongoId> keyToTpl = new Dictionary<string, MongoId>(StringComparer.Ordinal);
            foreach (SptLootItem si in sp.Template.Items)
            {
                if (si == null || string.IsNullOrEmpty(si.ComposedKey))
                {
                    continue;
                }

                keyToTpl[si.ComposedKey] = si.Template;
            }

            foreach (LooseLootItemDistribution d in sp.ItemDistribution)
            {
                if (d?.ComposedKey?.Key == null || d.RelativeProbability == null)
                {
                    continue;
                }

                if (!keyToTpl.TryGetValue(d.ComposedKey.Key, out MongoId tpl))
                {
                    continue;
                }

                if (!Originals.TryGetValue(d, out double orig))
                {
                    orig = d.RelativeProbability.Value;
                    Originals[d] = orig;
                }

                double f = PatchContext.FactorForPrice(PatchContext.PriceOfMongo(tpl));
                d.RelativeProbability = f > 1.0 ? orig * f : orig;
            }
        }
    }
}
