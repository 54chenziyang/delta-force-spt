using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Comfort.Common;
using EFT.InventoryLogic;
using EFT.Trading;
using EFT.UI;
using EFT.UI.DragAndDrop;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DeltaQuickMove
{
    /// <summary>
    /// Delta-style quick move: a plain double-click on an item moves it to the appropriate
    /// container (stash / rig / backpack / sell table), replacing the slow drag workflow.
    ///
    /// Interception point is ItemView's explicit implementation of
    /// UnityEngine.EventSystems.IPointerClickHandler.OnPointerClick. No ItemView subclass in the
    /// assembly overrides OnPointerClick, so this single patch covers every item view:
    /// stash/backpack grids (GridItemView) as well as the trader screen (TradingItemView /
    /// TradingPlayerItemView), which never calls base.OnClick and therefore could not be caught
    /// by patching ItemView.OnClick.
    /// </summary>
    [BepInPlugin("com.haavk.deltaquickmove", "Delta Quick Move", "1.1.0")]
    public class DeltaQuickMovePlugin : BaseUnityPlugin
    {
        internal static DeltaQuickMovePlugin Instance;
        internal static ManualLogSource Log;

        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<bool> PreserveQuickUse;
        internal static ConfigEntry<bool> PreserveContainerOpen;
        internal static ConfigEntry<bool> CloseTooltip;
        internal static ConfigEntry<bool> VerboseLog;

        private Harmony _harmony;

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            Enabled = Config.Bind("Delta Quick Move", "Enabled", true,
                "Master switch for double-click quick move.");
            PreserveQuickUse = Config.Bind("Delta Quick Move", "Preserve Quick Use", false,
                "If true, double-clicking meds / food still uses them instead of moving them.");
            PreserveContainerOpen = Config.Bind("Delta Quick Move", "Preserve Container Open", false,
                "If true, double-clicking a container still opens it instead of moving it.");
            CloseTooltip = Config.Bind("Delta Quick Move", "Close Tooltip", true,
                "Close the item tooltip after a quick move.");
            VerboseLog = Config.Bind("Delta Quick Move", "Verbose Log", true,
                "Log every quick-move attempt with its result (useful for diagnosing).");

            try
            {
                _harmony = new Harmony("com.haavk.deltaquickmove");

                var target = AccessTools.Method(typeof(ItemView),
                    "UnityEngine.EventSystems.IPointerClickHandler.OnPointerClick");
                Log.LogInfo("DeltaQuickMove: OnPointerClick resolved = " +
                            (target != null ? target.ToString() : "NULL"));

                _harmony.PatchAll(typeof(QuickMovePatch));
                Log.LogInfo("DeltaQuickMove: patched ItemView.OnPointerClick");
            }
            catch (Exception e)
            {
                Log.LogError("DeltaQuickMove: patch failed: " + e);
            }

            Log.LogInfo("Delta Quick Move v1.1.0 loaded");
        }

        internal static void Verbose(string message)
        {
            if (VerboseLog == null || VerboseLog.Value) Log.LogInfo(message);
        }

        private void OnDestroy()
        {
            if (_harmony != null) _harmony.UnpatchSelf();
        }
    }

    [HarmonyPatch(typeof(ItemView), "UnityEngine.EventSystems.IPointerClickHandler.OnPointerClick")]
    internal static class QuickMovePatch
    {
        [HarmonyPrefix]
        private static bool Prefix(ItemView __instance, PointerEventData eventData,
            ItemUiContext ___ItemUiContext, ItemController ___ItemController)
        {
            try
            {
                if (!DeltaQuickMovePlugin.Enabled.Value) return true;
                if (eventData == null) return true;
                if (eventData.button != PointerEventData.InputButton.Left) return true;
                if (eventData.clickCount % 2 != 0) return true;          // only the double click
                if (HeldCtrl() || HeldShift() || HeldAlt()) return true; // plain click only
                if (___ItemUiContext == null || ___ItemController == null) return true;
                if (!__instance.IsSearched) return true;

                Item item = __instance.Item;
                if (item == null) return true;

                if (__instance is TradingItemView trading)
                    return HandleTrading(trading, item);

                if (DeltaQuickMovePlugin.PreserveQuickUse.Value && (item is FoodDrink || item is Meds)) return true;
                if (DeltaQuickMovePlugin.PreserveContainerOpen.Value && item.IsContainer) return true;

                return HandleInventory(__instance, item, ___ItemUiContext, ___ItemController);
            }
            catch (Exception e)
            {
                DeltaQuickMovePlugin.Log.LogError("DeltaQuickMove exception: " + e);
                return true;
            }
        }

        private static bool HandleInventory(ItemView view, Item item, ItemUiContext itemUiContext,
            ItemController itemController)
        {
            var result = itemUiContext.QuickFindAppropriatePlace(view.ItemContext, itemController);
            if (result.Failed)
            {
                DeltaQuickMovePlugin.Verbose("DeltaQuickMove: quick-find failed for '" + item.Name +
                    "' in " + view.GetType().Name + " (" + ErrorOf(result) + ") -> vanilla fallback");
                return true;
            }
            if (!itemController.CanExecute(result.Value))
            {
                DeltaQuickMovePlugin.Verbose("DeltaQuickMove: cannot execute move for '" + item.Name +
                    "' in " + view.GetType().Name + " -> vanilla fallback");
                return true;
            }
            if (result.Value is IPossibleDestroyResult destroy && destroy.ItemsDestroyRequired)
            {
                DeltaQuickMovePlugin.Verbose("DeltaQuickMove: skipped '" + item.Name +
                    "' (move would destroy items) -> vanilla fallback");
                return true;
            }

            SimpleTooltip tooltip = itemUiContext.Tooltip;
            itemController.RunNetworkTransaction(result.Value);
            if (tooltip != null && DeltaQuickMovePlugin.CloseTooltip.Value) tooltip.Close();
            PlayPickupSound(item);
            DeltaQuickMovePlugin.Verbose("DeltaQuickMove: moved '" + item.Name + "' (" +
                view.GetType().Name + ")");
            return false;
        }

        private static bool HandleTrading(TradingItemView view, Item item)
        {
            Assortment assortment = GetField<Assortment>(view, "_traderAssortment");
            object modeRaw = GetField(view, "_tradeMode");
            object typeRaw = GetField(view, "_itemViewType");

            if (assortment == null || !(modeRaw is ETradeMode) || !(typeRaw is ETradingItemViewType))
            {
                DeltaQuickMovePlugin.Verbose("DeltaQuickMove: trading fields unavailable for '" +
                    item.Name + "' -> vanilla fallback");
                return true;
            }

            ETradeMode mode = (ETradeMode)modeRaw;
            ETradingItemViewType viewType = (ETradingItemViewType)typeRaw;

            // Item currently sitting on the sell table: double click takes it back (move out).
            if (viewType == ETradingItemViewType.TradingTable)
            {
                if (mode == ETradeMode.Sale)
                {
                    view.HideTooltip();
                    assortment.UnprepareSellItem(item);
                    DeltaQuickMovePlugin.Verbose("DeltaQuickMove: removed from sell table '" + item.Name + "'");
                    return false;
                }
                return true;
            }

            // Own inventory item while the sell tab is open: double click puts it on the table (move in).
            if (mode == ETradeMode.Sale)
            {
                try
                {
                    if (assortment.QuickFindTradingAppropriatePlace(item))
                    {
                        CloseDependentWindows(view.ItemContext);
                        view.HideTooltip();
                        PlayPickupSound(item);
                        DeltaQuickMovePlugin.Verbose("DeltaQuickMove: put on sell table '" + item.Name + "'");
                        return false;
                    }
                    DeltaQuickMovePlugin.Verbose("DeltaQuickMove: sell-table refused '" + item.Name + "'");
                }
                catch (Exception e)
                {
                    DeltaQuickMovePlugin.Log.LogWarning("DeltaQuickMove: trading move failed for '" +
                        item.Name + "': " + e.Message);
                }
                return true;
            }

            DeltaQuickMovePlugin.Verbose("DeltaQuickMove: trading no-op '" + item.Name + "' (" +
                viewType + "/" + mode + ") -> vanilla fallback");
            return true;
        }

        private static string ErrorOf(object result)
        {
            if (result == null) return "null result";
            try
            {
                var prop = result.GetType().GetProperty("Error");
                object error = prop != null ? prop.GetValue(result, null) : null;
                return error != null ? error.ToString() : "no error detail";
            }
            catch
            {
                return "error unavailable";
            }
        }

        private static object GetField(object instance, string name)
        {
            try
            {
                var field = AccessTools.Field(instance.GetType(), name);
                return field != null ? field.GetValue(instance) : null;
            }
            catch
            {
                return null;
            }
        }

        private static T GetField<T>(object instance, string name) where T : class
        {
            return GetField(instance, name) as T;
        }

        private static void CloseDependentWindows(object itemContext)
        {
            if (itemContext == null) return;
            try
            {
                var method = AccessTools.Method(itemContext.GetType(), "CloseDependentWindows", Type.EmptyTypes);
                if (method != null) method.Invoke(itemContext, null);
            }
            catch (Exception e)
            {
                DeltaQuickMovePlugin.Log.LogWarning("DeltaQuickMove: CloseDependentWindows failed: " + e.Message);
            }
        }

        private static void PlayPickupSound(Item item)
        {
            try
            {
                GUISounds sounds = Singleton<GUISounds>.Instance;
                if (sounds != null && item != null)
                    sounds.PlayItemSound(item.ItemSound, EInventorySoundType.pickup);
            }
            catch (Exception e)
            {
                DeltaQuickMovePlugin.Log.LogWarning("DeltaQuickMove: pickup sound failed: " + e.Message);
            }
        }

        private static bool HeldCtrl()
        {
            return Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        }

        private static bool HeldShift()
        {
            return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        }

        private static bool HeldAlt()
        {
            return Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
        }
    }
}
