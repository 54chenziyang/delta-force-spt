using System;
using System.Threading.Tasks;
using Comfort.Common;
using Diz.LanguageExtensions;
using EFT.HandBook;
using EFT.InventoryLogic;
using EFT.UI;
using UnityEngine;

namespace DeltaInventory
{
    /// <summary>
    /// Shared helpers for the Delta Inventory patches: equipment lookup, value comparison,
    /// network submission of a pre-computed operation and small input helpers.
    /// </summary>
    internal static class InventoryOps
    {
        internal static InventoryController AsInventoryController(ItemController controller)
        {
            return controller as InventoryController;
        }

        internal static InventoryEquipment GetEquipment(ItemController controller)
        {
            var inventoryController = controller as InventoryController;
            if (inventoryController == null) return null;
            Inventory inventory = inventoryController.Inventory;
            return inventory == null ? null : inventory.Equipment;
        }

        /// <summary>Weapons and knives are never auto-replaced (Delta rule).</summary>
        internal static bool IsWeaponOrKnife(Item item)
        {
            return item is Weapon || item is Knife;
        }

        /// <summary>Handbook base price, used to decide which item is "better".</summary>
        internal static float GetPrice(Item item)
        {
            if (item == null) return 0f;
            try
            {
                Handbook handbook = Singleton<Handbook>.Instance;
                if (handbook == null) return 0f;
                return (float)handbook.GetBasePrice(item.TemplateId);
            }
            catch
            {
                return 0f;
            }
        }

        internal static float GetArea(Item item, ItemRotation rotation)
        {
            if (item == null) return 0f;
            IntVec2 size = item.CalculateRotatedSize(rotation);
            return size.X * size.Y;
        }

        /// <summary>
        /// Submits an operation that was produced with simulate:true, i.e. a fully built but not
        /// yet applied operation. Returns true when the server accepted it.
        /// </summary>
        internal static async Task<bool> Submit(ItemController controller, OperationResult operation)
        {
            if (controller == null || operation.Failed) return false;

            IOperationResult value = operation.Value;
            if (value == null) return false;
            if (!controller.CanExecute(value)) return false;

            try
            {
                IResult result = await controller.TryRunNetworkTransaction(operation);
                return result == null || !result.Failed;
            }
            catch (Exception e)
            {
                DeltaInventoryPlugin.Verbose("DeltaInventory: submit failed: " + e.Message);
                return false;
            }
        }

        internal static void PlayPickupSound(Item item)
        {
            try
            {
                GUISounds sounds = Singleton<GUISounds>.Instance;
                if (sounds != null && item != null)
                    sounds.PlayItemSound(item.ItemSound, EInventorySoundType.pickup);
            }
            catch (Exception e)
            {
                DeltaInventoryPlugin.Verbose("DeltaInventory: pickup sound failed: " + e.Message);
            }
        }

        internal static bool HeldCtrl()
        {
            return Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        }

        internal static bool HeldShift()
        {
            return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        }

        internal static bool HeldAlt()
        {
            return Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
        }
    }
}
