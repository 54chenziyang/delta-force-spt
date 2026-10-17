using System;
using System.Collections.Generic;
using System.Linq;
using SPTarkov.Common.Models.Logging;
using SPTarkov.Server.Core.Helpers.Profile;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Tables;

namespace DeltaTune;

/// <summary>
/// 补丁与配置之间的共享上下文：配置、日志、ProfileHelper、价格索引。
/// </summary>
public static class PatchContext
{
    public static ModConfig? Config { get; private set; }

    public static ISptLogger<DeltaTuneMod>? Logger { get; private set; }

    public static ProfileHelper? Profiles { get; private set; }

    private static readonly Dictionary<string, double> Prices =
        new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

    public static void Init(ModConfig cfg, ISptLogger<DeltaTuneMod> logger, ProfileHelper profiles, TemplateTable table)
    {
        Config = cfg;
        Logger = logger;
        Profiles = profiles;
        BuildPriceIndex(table);
    }

    // ---------------------------------------------------------------- 价格

    /// <summary>用模板价格 + 手册价（取较大者）建立 tpl -> 价格 索引。</summary>
    public static void BuildPriceIndex(TemplateTable table)
    {
        Prices.Clear();
        try
        {
            if (table?.Prices != null)
            {
                foreach (KeyValuePair<MongoId, double> kv in table.Prices)
                {
                    Prices[kv.Key.ToString()] = kv.Value;
                }
            }

            if (table?.Handbook?.Items != null)
            {
                foreach (HandbookItem hi in table.Handbook.Items)
                {
                    if (hi?.Id == null)
                    {
                        continue;
                    }

                    string key = hi.Id.ToString();
                    double p = hi.Price ?? 0.0;
                    if (p > 0.0 && (!Prices.TryGetValue(key, out double old) || p > old))
                    {
                        Prices[key] = p;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Logger?.Warning("[DeltaTune] 价格索引构建失败: " + ex.Message);
        }
    }

    public static double PriceOfMongo(MongoId id) => PriceOf(id.ToString());

    public static double PriceOf(string? id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return 0.0;
        }

        return Prices.TryGetValue(id!, out double p) ? p : 0.0;
    }

    /// <summary>按价格给出稀有度权重倍率（普通=1，金/红来自当前局档位）。</summary>
    public static double FactorForPrice(double price)
    {
        LootConfig? loot = Config?.Loot;
        if (loot == null)
        {
            return 1.0;
        }

        if (price >= loot.RedPriceThreshold)
        {
            return Math.Max(1.0, RaidState.RedFactor);
        }

        if (price >= loot.GoldPriceThreshold)
        {
            return Math.Max(1.0, RaidState.GoldFactor);
        }

        return 1.0;
    }

    // ---------------------------------------------------------------- 战备

    /// <summary>
    /// 以 Equipment 根节点为起点 DFS 累加装备价值（含改装件/弹匣内物），
    /// 安全箱（SlotId = SecuredContainer）整棵剪枝。
    /// </summary>
    public static double ComputeGearValue(PmcData? pmc)
    {
        try
        {
            List<Item>? items = pmc?.Inventory?.Items;
            if (items == null || items.Count == 0)
            {
                return 0.0;
            }

            MongoId? equip = pmc!.Inventory!.Equipment;
            if (equip == null || equip.Value.IsEmpty)
            {
                return 0.0;
            }

            string rootId = equip.Value.ToString();

            Dictionary<string, List<Item>> children = new Dictionary<string, List<Item>>(StringComparer.Ordinal);
            foreach (Item it in items)
            {
                if (it?.ParentId == null)
                {
                    continue;
                }

                if (!children.TryGetValue(it.ParentId, out List<Item>? list))
                {
                    list = new List<Item>();
                    children[it.ParentId] = list;
                }

                list.Add(it);
            }

            Item? root = items.FirstOrDefault(i => i != null && i.Id.ToString() == rootId);
            if (root == null)
            {
                return 0.0;
            }

            double total = 0.0;
            Stack<Item> stack = new Stack<Item>();
            stack.Push(root);

            while (stack.Count > 0)
            {
                Item cur = stack.Pop();
                if (cur == null)
                {
                    continue;
                }

                // 安全箱不计入战备。
                if (string.Equals(cur.SlotId, "SecuredContainer", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                total += PriceOf(cur.Template.ToString()) * (cur.Upd?.StackObjectsCount ?? 1.0);

                if (children.TryGetValue(cur.Id.ToString(), out List<Item>? kids))
                {
                    foreach (Item k in kids)
                    {
                        stack.Push(k);
                    }
                }
            }

            return total;
        }
        catch (Exception ex)
        {
            Logger?.Warning("[DeltaTune] 战备估值失败: " + ex.Message);
            return 0.0;
        }
    }

    // ---------------------------------------------------------------- 统计

    /// <summary>终身击杀数（OverallCounters 的 "Kills"）。</summary>
    public static double GetLifetimeKills(PmcData? pmc)
    {
        try
        {
            List<CounterKeyValue>? counters = pmc?.Stats?.Eft?.OverallCounters?.Items;
            if (counters == null)
            {
                return 0.0;
            }

            foreach (CounterKeyValue c in counters)
            {
                if (c?.Key == null)
                {
                    continue;
                }

                if (c.Key.Count == 1 && c.Key.Contains("Kills"))
                {
                    return c.Value ?? 0;
                }
            }
        }
        catch
        {
            // 忽略：统计缺失不影响主流程。
        }

        return 0.0;
    }

    /// <summary>本档案记录的击杀受害者数量（兜底统计）。</summary>
    public static int CountVictims(PmcData? pmc)
    {
        try
        {
            IEnumerable<Victim>? victims = pmc?.Stats?.Eft?.Victims;
            return victims == null ? 0 : victims.Count();
        }
        catch
        {
            return 0;
        }
    }
}
