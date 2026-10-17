using System;
using System.Threading.Tasks;
using Diz.LanguageExtensions;
using EFT.InventoryLogic;
using EFT.UI;
using EFT.UI.DragAndDrop;
using HarmonyLib;
using UnityEngine.EventSystems;

namespace DeltaInventory
{
    /// <summary>
    /// Feature 1 + 2: a plain double-click equips rig / backpack / helmet / armor / weapon /
    /// knife (and pistol) into the matching equipment slot.
    ///
    /// When the target slot is already taken by a cheaper item, the cheaper item is swapped out
    /// instead (weapons and knives are never auto-replaced). Containers that took part in the
    /// swap are sorted afterwards.
    ///
    /// Patch priority is Priority.First (800) so it wins over DeltaQuickMove's default (400)
    /// quick-move patch, which would otherwise send the item to the stash.
    /// </summary>
    [HarmonyPatch(typeof(ItemView), "UnityEngine.EventSystems.IPointerClickHandler.OnPointerClick")]
    [HarmonyPriority(Priority.First)]
    internal static class DoubleClickEquipPatch
    {
        /// <summary>
        /// Slots a double-click may target, most specific first so that e.g. a pistol lands in the
        /// holster instead of an empty primary weapon slot.
        /// </summary>
        private static readonly EquipmentSlot[] TargetSlots =
        {
            EquipmentSlot.Scabbard,           // knife
            EquipmentSlot.Holster,            // pistol
            EquipmentSlot.FirstPrimaryWeapon,
            EquipmentSlot.SecondPrimaryWeapon,
            EquipmentSlot.Headwear,           // helmet / hat
            EquipmentSlot.ArmorVest,          // body armor
            EquipmentSlot.TacticalVest,       // chest rig
            EquipmentSlot.Backpack
        };

        [HarmonyPrefix]
        private static bool Prefix(ItemView __instance, PointerEventData eventData,
            ItemUiContext ___ItemUiContext, ItemController ___ItemController)
        {
            try
            {
                if (DeltaInventoryPlugin.Instance == null) return true;
                if (!DeltaInventoryPlugin.Enabled.Value) return true;
                if (!DeltaInventoryPlugin.DoubleClickEquip.Value) return true;
                if (eventData == null) return true;
                if (eventData.button != PointerEventData.InputButton.Left) return true;
                if (eventData.clickCount == 0 || eventData.clickCount % 2 != 0) return true;
                if (InventoryOps.HeldCtrl() || InventoryOps.HeldShift() || InventoryOps.HeldAlt()) return true;
                if (___ItemUiContext == null) return true;
                if (!__instance.IsSearched) return true;
                if (__instance is TradingItemView) return true;

                Item item = __instance.Item;
                if (item == null) return true;

                InventoryController controller = InventoryOps.AsInventoryController(___ItemController);
                if (controller == null) return true;

                InventoryEquipment equipment = InventoryOps.GetEquipment(___ItemController);
                if (equipment == null) return true;

                if (controller.IsItemEquipped(item)) return true;
                if (item.CurrentAddress == null) return true;

                Slot emptyTarget = null;
                Slot replaceTarget = null;
                float bestOldPrice = float.MaxValue;
                bool allowReplace = DeltaInventoryPlugin.AutoReplaceBetter.Value &&
                                    !InventoryOps.IsWeaponOrKnife(item);

                foreach (EquipmentSlot slotName in TargetSlots)
                {
                    Slot slot = equipment.GetSlot(slotName);
                    if (slot == null) continue;
                    if (!controller.IsAllowedToSeeSlot(slot, slotName)) continue;
                    if (!slot.CheckCompatibility(item)) continue;

                    if (slot.ContainedItem == null)
                    {
                        emptyTarget = slot;
                        break;
                    }

                    if (!allowReplace) continue;

                    Item oldItem = slot.ContainedItem;
                    float newPrice = InventoryOps.GetPrice(item);
                    float oldPrice = InventoryOps.GetPrice(oldItem);
                    if (newPrice > oldPrice && oldPrice < bestOldPrice)
                    {
                        bestOldPrice = oldPrice;
                        replaceTarget = slot;
                    }
                }

                Slot chosen = emptyTarget ?? replaceTarget;
                if (chosen == null) return true;
                if (chosen.ContainedItem != null && !chosen.CanReplace(item)) return true;

                Item replaced = chosen.ContainedItem;
                DeltaInventoryPlugin.Verbose("DeltaInventory: double-click equip '" + item.Name +
                    "' -> " + chosen.ID + (replaced != null ? " (replacing '" + replaced.Name + "')" : " (empty)"));

                _ = EquipAsync(chosen, item, replaced, controller);
                return false;
            }
            catch (Exception e)
            {
                DeltaInventoryPlugin.Log.LogError("DeltaInventory: double-click equip exception: " + e);
                return true;
            }
        }

        private static async Task EquipAsync(Slot slot, Item item, Item replaced, InventoryController controller)
        {
            try
            {
                ItemAddress slotAddress = slot.CreateItemAddress();

                // 1) Empty slot: a plain move.
                if (replaced == null)
                {
                    OperationResult<MoveResult> move =
                        ItemManipulator.Move(item, slotAddress, controller, simulate: true);
                    if (move.Failed)
                    {
                        DeltaInventoryPlugin.Verbose("DeltaInventory: equip move refused: " + ErrorOf(move));
                        return;
                    }
                    if (!await InventoryOps.Submit(controller, move)) return;
                    InventoryOps.PlayPickupSound(item);
                    await SortAsync(controller, item);
                    return;
                }

                // 2) Occupied slot: swap the new item in and the old item into the spot the new
                //    item came from. This is atomic, so the old item keeps its contents and finds
                //    a place even when it is bigger than the newcomer.
                ItemAddress from = item.CurrentAddress;
                if (from != null)
                {
                    OperationResult<SwapResult> swap =
                        ItemManipulator.Swap(item, slotAddress, replaced, from, controller, simulate: true);
                    if (!swap.Failed && await InventoryOps.Submit(controller, swap))
                    {
                        InventoryOps.PlayPickupSound(item);
                        await SortAsync(controller, replaced, item);
                        return;
                    }
                    DeltaInventoryPlugin.Verbose("DeltaInventory: equip swap refused (" + ErrorOf(swap) +
                        ") -> EquipItemInSlot fallback");
                }

                // 3) Fallback: the vanilla "equip into slot, displaced item goes to the first free
                //    stash cell" primitive (exactly what the vanilla click-to-equip uses).
                OperationResult<IOperationResult> equip =
                    ItemManipulator.EquipItemInSlot(slot, item, controller, simulate: true);
                if (equip.Failed)
                {
                    DeltaInventoryPlugin.Verbose("DeltaInventory: equip replacement refused: " + ErrorOf(equip));
                    return;
                }
                if (!await InventoryOps.Submit(controller, equip)) return;
                InventoryOps.PlayPickupSound(item);
                await SortAsync(controller, replaced, item);
            }
            catch (Exception e)
            {
                DeltaInventoryPlugin.Log.LogError("DeltaInventory: equip exception: " + e);
            }
        }

        /// <summary>Auto-sorts every container that took part in the swap.</summary>
        private static async Task SortAsync(InventoryController controller, params Item[] items)
        {
            if (!DeltaInventoryPlugin.AutoSortReplacedContainer.Value) return;
            if (items == null) return;

            foreach (Item item in items)
            {
                if (!(item is CompoundItem compound)) continue;
                try
                {
                    if (!controller.IsAllowedToSort(compound)) continue;
                    OperationResult<ApplySortItemsPositionResult> sort =
                        ItemManipulator.Sort(compound, controller, simulate: true);
                    if (sort.Failed) continue;
                    await InventoryOps.Submit(controller, sort);
                }
                catch (Exception e)
                {
                    DeltaInventoryPlugin.Verbose("DeltaInventory: sort '" + item.Name + "' failed: " + e.Message);
                }
            }
        }

        private static string ErrorOf(object result)
        {
            if (result == null) return "null result";
            try
            {
                var property = result.GetType().GetProperty("Error");
                object error = property != null ? property.GetValue(result, null) : null;
                return error != null ? error.ToString() : "no error detail";
            }
            catch
            {
                return "error unavailable";
            }
        }
    }
}
