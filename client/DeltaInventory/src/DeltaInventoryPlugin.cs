using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using EFT.UI.DragAndDrop;
using HarmonyLib;

namespace DeltaInventory
{
    /// <summary>
    /// Delta-style inventory interactions, split into three features:
    ///
    /// 1. Double-click equip  - a plain double-click equips rig / backpack / helmet / armor /
    ///    weapon / knife into its matching equipment slot, optionally replacing a lower value
    ///    item (weapons and knives excluded from replacement) and auto-sorting the containers
    ///    that took part in the swap.
    /// 2. Drag swap           - dropping an item on top of other items swaps their grid
    ///    positions whenever both sides fit (1-to-1 and 1-to-many).
    /// 3. Drag discard        - dragging an item to the right edge reveals a red lane with a
    ///    trash can glyph; releasing over it discards the item.
    ///
    /// Harmony patch priority ordering (descending = executed first):
    ///   TrashZone patch      Priority.First    (800)  -> discard wins over swap
    ///   DragSwap patch       Priority.VeryHigh (700)
    ///   DeltaQuickMove patch default           (400)
    /// </summary>
    [BepInPlugin("com.haavk.deltainventory", "Delta Inventory", "1.0.0")]
    public class DeltaInventoryPlugin : BaseUnityPlugin
    {
        internal static DeltaInventoryPlugin Instance;
        internal static ManualLogSource Log;

        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<bool> DoubleClickEquip;
        internal static ConfigEntry<bool> AutoReplaceBetter;
        internal static ConfigEntry<bool> AutoSortReplacedContainer;
        internal static ConfigEntry<bool> DragSwapEnabled;
        internal static ConfigEntry<bool> DragDiscardEnabled;
        internal static ConfigEntry<int> TrashZoneWidth;
        internal static ConfigEntry<bool> VerboseLog;

        private Harmony _harmony;

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            Enabled = Config.Bind("Delta Inventory", "Enabled", true,
                "Master switch for every Delta Inventory feature.");
            DoubleClickEquip = Config.Bind("Delta Inventory", "Double Click Equip", true,
                "Double-click equips rig / backpack / helmet / armor / weapon / knife into its slot.");
            AutoReplaceBetter = Config.Bind("Delta Inventory", "Auto Replace Better", true,
                "When equipping onto a full slot, replace the lower value item (weapons and knives excluded).");
            AutoSortReplacedContainer = Config.Bind("Delta Inventory", "Auto Sort Replaced Container", true,
                "Auto-sort the containers involved in an equip replacement.");
            DragSwapEnabled = Config.Bind("Delta Inventory", "Drag Swap", true,
                "Dragging an item onto items that overlap the drop area swaps positions when both fit.");
            DragDiscardEnabled = Config.Bind("Delta Inventory", "Drag Discard", true,
                "Dragging an item to the right edge trashes it (Delta-style red lane).");
            TrashZoneWidth = Config.Bind("Delta Inventory", "Trash Zone Width", 110,
                "Width in pixels of the drag-to-discard strip on the right edge.");
            VerboseLog = Config.Bind("Delta Inventory", "Verbose Log", true,
                "Log every takeover with its result (useful for diagnosing).");

            try
            {
                _harmony = new Harmony("com.haavk.deltainventory");

                var click = AccessTools.Method(typeof(ItemView),
                    "UnityEngine.EventSystems.IPointerClickHandler.OnPointerClick");
                var process = AccessTools.Method(typeof(ItemView), nameof(ItemView.ProcessItem));
                Log.LogInfo("DeltaInventory: OnPointerClick resolved = " +
                            (click != null ? click.ToString() : "NULL"));
                Log.LogInfo("DeltaInventory: ProcessItem resolved = " +
                            (process != null ? process.ToString() : "NULL"));

                _harmony.PatchAll();
                Log.LogInfo("DeltaInventory: patches applied");
            }
            catch (Exception e)
            {
                Log.LogError("DeltaInventory: patch failed: " + e);
            }

            Log.LogInfo("Delta Inventory v1.0.0 loaded");
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
}
