using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Logging;
using Comfort.Common;
using EFT.HandBook;
using EFT.Hideout;
using HarmonyLib;

namespace Haavk.HideoutPrice
{
    [BepInPlugin("com.haavk.hideoutprice", "Hideout Auto-Pay Price", "1.0.0")]
    public class HideoutPricePlugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;

        private void Awake()
        {
            Log = Logger;
            try
            {
                new Harmony("com.haavk.hideoutprice").PatchAll();
                Log.LogInfo("hideout price hint enabled");
            }
            catch (Exception e)
            {
                Log.LogError("hideout price hint patch failed: " + e);
            }
        }
    }

    /// <summary>
    /// 在藏身处升级需求标题后追加「自动补全 ≈ X 卢布」，与服务端扣费口径一致：
    /// 缺口数量 × ceil(max(1, handbook 基础价))。
    /// </summary>
    [HarmonyPatch(typeof(RequirementsPanel), "ShowContents")]
    internal static class RequirementsPanelPricePatch
    {
        [HarmonyPostfix]
        private static void Postfix(RequirementsPanel __instance)
        {
            try
            {
                if (__instance == null || __instance._requirementsLabel == null)
                {
                    return;
                }

                List<Requirement> requirements = __instance.Info;
                if (requirements == null || requirements.Count == 0)
                {
                    return;
                }

                Handbook handbook = Singleton<Handbook>.Instance;
                if (handbook == null)
                {
                    return;
                }

                long cost = 0;
                bool hasMissing = false;

                foreach (Requirement requirement in requirements)
                {
                    if (requirement == null)
                    {
                        continue;
                    }

                    // 自动补全只掏钱补 Item，其它需求（工具/技能/商人/区域等）不满足时无法补
                    if (requirement.Type != ERequirementType.Item)
                    {
                        if (!requirement.Fulfilled)
                        {
                            return;
                        }
                        continue;
                    }

                    ItemRequirement item = requirement as ItemRequirement;
                    if (item == null || item.Fulfilled)
                    {
                        continue;
                    }

                    int missing = item.IntCount - item.UserItemsCount;
                    if (missing <= 0)
                    {
                        continue;
                    }

                    double price;
                    try
                    {
                        price = handbook.GetBasePrice(item.TemplateId);
                    }
                    catch
                    {
                        // handbook 里没有该物品价格时，整体不显示，避免给出错误金额
                        return;
                    }

                    cost += (long)Math.Ceiling(Math.Max(1.0, price)) * missing;
                    hasMissing = true;
                }

                if (!hasMissing || cost <= 0)
                {
                    return;
                }

                __instance._requirementsLabel.text += "   ·   自动补全 ≈ " + cost.ToString("N0") + " ₽";
                HideoutPricePlugin.Log.LogInfo("hideout auto-pay cost hint: " + cost);
            }
            catch (Exception e)
            {
                HideoutPricePlugin.Log.LogError("hideout price hint error: " + e);
            }
        }
    }
}
