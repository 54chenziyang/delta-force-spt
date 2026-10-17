using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Logging;
using Comfort.Common;
using EFT;
using EFT.HandBook;
using EFT.InventoryLogic;
using EFT.Trading;
using EFT.UI;
using HarmonyLib;
using TMPro;

namespace Haavk.TraderFill
{
    [BepInPlugin("com.haavk.traderfill", "Trader Barter Auto-Fill", "1.0.0")]
    public class TraderFillPlugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;

        private void Awake()
        {
            Log = Logger;
            try
            {
                new Harmony("com.haavk.traderfill").PatchAll();
                Log.LogInfo("trader barter auto-fill enabled");
            }
            catch (Exception e)
            {
                Log.LogError("trader barter auto-fill patch failed: " + e);
            }
        }
    }

    /// <summary>
    /// Shared helpers: read the private Trader of the deal screen and turn every missing
    /// barter requirement into its handbook rouble value.
    /// </summary>
    internal static class TraderFillUtil
    {
        private static AccessTools.FieldRef<TraderDealScreen, Trader> _traderRef;
        private static bool _traderRefFailed;

        internal static TraderDealScreen CachedScreen;

        internal static Trader GetTrader(TraderDealScreen screen)
        {
            if (screen == null)
            {
                return null;
            }

            if (_traderRef == null && !_traderRefFailed)
            {
                try
                {
                    _traderRef = AccessTools.FieldRefAccess<TraderDealScreen, Trader>("_trader");
                }
                catch (Exception e)
                {
                    _traderRefFailed = true;
                    TraderFillPlugin.Log.LogError("cannot resolve TraderDealScreen._trader: " + e);
                }
            }

            if (_traderRef == null)
            {
                return null;
            }

            try
            {
                return _traderRef(screen);
            }
            catch
            {
                return null;
            }
        }

        internal static bool IsCurrency(MongoID templateId)
        {
            ECurrencyType type;
            return CurrencyUtil.TryGetCurrencyType(templateId, out type);
        }

        /// <summary>
        /// Adds up the handbook rouble value of everything the player did not prepare.
        /// <paramref name="hasMissingItem"/> stays false when only currencies are missing,
        /// so the vanilla auto-exchange path keeps owning those cases.
        /// </summary>
        internal static long ComputeMissingCost(List<Requisite> requisites, out bool hasMissingItem)
        {
            hasMissingItem = false;
            if (requisites == null)
            {
                return 0;
            }

            Handbook handbook = Singleton<Handbook>.Instance;
            long cost = 0;

            foreach (Requisite requisite in requisites)
            {
                if (requisite == null || requisite.RequiredItem == null)
                {
                    continue;
                }

                int missing = requisite.RequiredItemsCount - requisite.PreparedItemsCount;
                if (missing <= 0)
                {
                    continue;
                }

                MongoID templateId = requisite.RequiredItem.TemplateId;
                double price = 0d;
                if (handbook != null)
                {
                    try
                    {
                        price = handbook.GetBasePrice(templateId);
                    }
                    catch
                    {
                        price = 0d;
                    }
                }

                long unit = (long)Math.Ceiling(price < 1d ? 1d : price);
                cost += unit * missing;

                if (!IsCurrency(templateId))
                {
                    hasMissingItem = true;
                }
            }

            return cost;
        }

        internal static string MissingCostText(long cost)
        {
            return "≈ " + cost.ToString("N0") + " " + CurrencyUtil.GetCurrencyChar(ECurrencyType.RUB);
        }
    }

    /// <summary>
    /// Lets the deal button (and the click handler, which uses the same method) accept a
    /// barter whose item requirements are not fully prepared. The server charges the
    /// difference in roubles.
    /// </summary>
    [HarmonyPatch(typeof(TraderDealScreen), nameof(TraderDealScreen.IsValidDeal))]
    internal static class ValidDealPatch
    {
        [HarmonyPostfix]
        private static void Postfix(TraderDealScreen __instance, ref bool __result, ref bool withAutoExchange)
        {
            try
            {
                if (__result || __instance == null)
                {
                    return;
                }

                if (__instance.TradeMode != ETradeMode.Purchase)
                {
                    return;
                }

                Trader trader = TraderFillUtil.GetTrader(__instance);
                List<Requisite> requisites = trader?.CurrentAssortment?.CurrentRequisites;
                if (requisites == null || requisites.Count == 0)
                {
                    return;
                }

                bool hasMissingItem;
                long cost = TraderFillUtil.ComputeMissingCost(requisites, out hasMissingItem);
                if (!hasMissingItem || cost <= 0)
                {
                    return;
                }

                __result = true;
                withAutoExchange = false;
            }
            catch (Exception e)
            {
                TraderFillPlugin.Log.LogError("IsValidDeal patch error: " + e);
            }
        }
    }

    /// <summary>
    /// Vanilla hides the price label for barters, so write the rouble top-up there ourselves.
    /// </summary>
    [HarmonyPatch(typeof(TraderDealScreen), nameof(TraderDealScreen.OnPriceChanged))]
    internal static class PriceChangedPatch
    {
        private static long _lastCost = -1;

        [HarmonyPostfix]
        private static void Postfix(TraderDealScreen __instance)
        {
            try
            {
                if (__instance == null)
                {
                    return;
                }

                TraderFillUtil.CachedScreen = __instance;

                if (__instance.TradeMode != ETradeMode.Purchase)
                {
                    return;
                }

                Trader trader = TraderFillUtil.GetTrader(__instance);
                List<Requisite> requisites = trader?.CurrentAssortment?.CurrentRequisites;
                if (requisites == null || requisites.Count == 0)
                {
                    return;
                }

                bool hasMissingItem;
                long cost = TraderFillUtil.ComputeMissingCost(requisites, out hasMissingItem);
                if (!hasMissingItem || cost <= 0)
                {
                    _lastCost = -1;
                    return;
                }

                TMP_Text[] labels = __instance._equivalentSumValue;
                if (labels == null)
                {
                    return;
                }

                string text = TraderFillUtil.MissingCostText(cost);
                foreach (TMP_Text label in labels)
                {
                    if (label == null)
                    {
                        continue;
                    }

                    label.gameObject.SetActive(true);
                    label.text = text;
                }

                if (_lastCost != cost)
                {
                    _lastCost = cost;
                    TraderFillPlugin.Log.LogInfo("barter top-up cost: " + cost);
                }
            }
            catch (Exception e)
            {
                TraderFillPlugin.Log.LogError("OnPriceChanged patch error: " + e);
            }
        }
    }

    /// <summary>
    /// Preparing/removing barter items does not raise PreparedSumChanged in purchase mode,
    /// so refresh the price label by hand.
    /// </summary>
    [HarmonyPatch(typeof(Assortment), nameof(Assortment.OnRequisiteChanged))]
    internal static class RequisiteChangedPatch
    {
        [HarmonyPostfix]
        private static void Postfix(Assortment __instance)
        {
            try
            {
                TraderDealScreen screen = TraderFillUtil.CachedScreen;
                if (screen == null || __instance == null)
                {
                    return;
                }

                Trader trader = TraderFillUtil.GetTrader(screen);
                if (trader == null || trader.CurrentAssortment != __instance)
                {
                    return;
                }

                screen.OnPriceChanged();
            }
            catch (Exception e)
            {
                TraderFillPlugin.Log.LogError("OnRequisiteChanged patch error: " + e);
            }
        }
    }
}
