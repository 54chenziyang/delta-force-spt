using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using SPTarkov.Common.Models.Logging;
using SPTarkov.Server.Core.Helpers.Commerce;
using SPTarkov.Server.Core.Helpers.Profile;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.ItemEvent;
using SPTarkov.Server.Core.Models.Eft.Trade;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Utils;

namespace Haavk.TraderFill;

internal static class TraderFillPatch
{
    private const string RoublesTpl = "5449016a4bdc2d6f028b456f";
    private const string LogTag = "[Haavk.TraderFill]";

    private static readonly HashSet<string> CurrencyTpls = new()
    {
        RoublesTpl,
        "5696686a4bdc2da3298b456a",
        "569668774bdc2da2298b4568",
        "5d235b4d86f7742e017bc88a"
    };

    private static Harmony _harmony;
    private static bool _enabled;

    internal static ISptLogger<TraderFillMod> Logger;
    internal static TradersTable TradersTable;
    internal static HandbookHelper HandbookHelper;
    internal static HttpResponseUtil HttpResponseUtil;

    internal static void Enable()
    {
        if (_enabled) return;
        try
        {
            System.Reflection.MethodInfo target = AccessTools.Method(typeof(TradeHelper), nameof(TradeHelper.BuyItem));
            if (target == null)
            {
                Logger?.Warning(LogTag + " TradeHelper.BuyItem not found, auto top-up disabled");
                return;
            }

            _harmony = new Harmony("com.haavk.traderfill");
            _harmony.Patch(target, new HarmonyMethod(typeof(TraderFillPatch), nameof(Prefix)));
            _enabled = true;
            Logger?.Info(LogTag + " trader barter auto top-up enabled");
        }
        catch (Exception ex)
        {
            Logger?.Error(LogTag + " enable failed: " + ex);
        }
    }

    private static bool Prefix(PmcData pmcData, ProcessBuyTradeRequestData buyRequestData, MongoId sessionId,
        ItemEventRouterResponse output)
    {
        try
        {
            return Process(pmcData, buyRequestData, sessionId, output);
        }
        catch (Exception ex)
        {
            Logger?.Error(LogTag + " prefix error, falling back to vanilla: " + ex);
            return true;
        }
    }

    private static bool Process(PmcData pmcData, ProcessBuyTradeRequestData buyRequestData, MongoId sessionId,
        ItemEventRouterResponse output)
    {
        if (pmcData?.Inventory?.Items == null || buyRequestData == null || output == null) return true;
        if (!string.Equals(buyRequestData.Type, "buy_from_trader", StringComparison.OrdinalIgnoreCase)) return true;

        Trader trader = TradersTable?.GetTrader(buyRequestData.TransactionId);
        if (trader?.Assort?.BarterScheme == null) return true;
        if (!trader.Assort.BarterScheme.TryGetValue(buyRequestData.ItemId, out List<List<BarterScheme>> variants)) return true;
        if (variants == null || variants.Count == 0) return true;

        int schemeIdx = buyRequestData.SchemeId ?? 0;
        if (schemeIdx < 0 || schemeIdx >= variants.Count) schemeIdx = 0;
        List<BarterScheme> scheme = variants[schemeIdx];
        if (scheme == null || scheme.Count == 0) return true;

        int quantity = buyRequestData.Count ?? 1;
        if (quantity < 1) quantity = 1;

        var required = new Dictionary<MongoId, int>();
        foreach (BarterScheme requirement in scheme)
        {
            if (requirement == null) continue;
            double rawCount = requirement.Count ?? 0d;
            int need = (int)Math.Ceiling(rawCount * quantity);
            if (need <= 0) continue;
            if (required.TryGetValue(requirement.Template, out int existing)) required[requirement.Template] = existing + need;
            else required[requirement.Template] = need;
        }

        buyRequestData.SchemeItems ??= new List<IdWithCount>();
        var provided = new Dictionary<MongoId, int>();
        long submittedRoubles = 0;
        foreach (IdWithCount entry in buyRequestData.SchemeItems)
        {
            if (entry == null) continue;
            Item inventoryItem = pmcData.Inventory.Items.FirstOrDefault(x => x != null && x.Id == entry.Id);
            if (inventoryItem == null) continue;
            int count = (int)Math.Round(entry.Count ?? 0d);
            if (count <= 0) continue;
            if (provided.TryGetValue(inventoryItem.Template, out int existing)) provided[inventoryItem.Template] = existing + count;
            else provided[inventoryItem.Template] = count;
            if (inventoryItem.Template == new MongoId(RoublesTpl)) submittedRoubles += count;
        }

        long cost = 0;
        bool hasMissingItem = false;
        foreach (KeyValuePair<MongoId, int> pair in required)
        {
            int have = provided.TryGetValue(pair.Key, out int prepared) ? prepared : 0;
            int missing = pair.Value - have;
            if (missing <= 0) continue;

            double price;
            try { price = HandbookHelper?.GetTemplatePrice(pair.Key) ?? 0d; }
            catch { price = 0d; }
            long unit = (long)Math.Ceiling(price < 1d ? 1d : price);
            cost += unit * missing;
            if (!CurrencyTpls.Contains(pair.Key.ToString())) hasMissingItem = true;
        }

        if (!hasMissingItem || cost <= 0) return true;

        long unlocked = 0;
        long all = 0;
        foreach (Item item in pmcData.Inventory.Items)
        {
            if (item == null || item.Template != new MongoId(RoublesTpl)) continue;
            long stack = (long)Math.Floor(item.Upd?.StackObjectsCount ?? 1d);
            if (stack <= 0) continue;
            all += stack;
            if (item.Upd?.PinLockState != PinLockState.Locked) unlocked += stack;
        }

        long available = unlocked > 0 ? unlocked : all;
        long need2 = submittedRoubles + cost;
        if (available < need2)
        {
            string message = $"商人自动补全失败：需要 {need2:N0} 卢布，你当前可用 {available:N0} 卢布。";
            HttpResponseUtil?.AppendErrorToOutput(output, message);
            Logger?.Warning(LogTag + $" not enough roubles for top-up: need {need2}, available {available}");
            return false;
        }

        buyRequestData.SchemeItems.Add(new IdWithCount { Id = new MongoId(RoublesTpl), Count = cost });
        Logger?.Info(LogTag + $" barter top-up: {cost:N0} RUB for {buyRequestData.ItemId} x{quantity}");
        return true;
    }
}
