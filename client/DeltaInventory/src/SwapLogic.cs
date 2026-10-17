using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Diz.LanguageExtensions;
using EFT.InventoryLogic;
using EFT.UI.DragAndDrop;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DeltaInventory
{
    /// <summary>
    /// Feature 3: dropping a dragged item onto cells occupied by other items swaps the two sides
    /// whenever both fit (Delta behaviour).
    ///
    ///   * 1-to-1  - a single atomic ItemManipulator.Swap.
    ///   * 1-to-N  - the dragged item is parked, the overlapped items are re-packed inside the
    ///               area the dragged item originally occupied and finally the dragged item is
    ///               moved to the drop position. Every step is a real network transaction; if a
    ///               step fails, the already executed steps are undone in reverse order.
    ///
    /// The vanilla behaviour is always probed first (merge / transfer / put-into-container) and
    /// wins whenever it can accept the drop. Priority is VeryHigh (700) so it runs after the
    /// trash-zone patch (800) but before DeltaQuickMove (400).
    /// </summary>
    [HarmonyPatch(typeof(ItemView), nameof(ItemView.ProcessItem))]
    [HarmonyPriority(Priority.VeryHigh)]
    internal static class DragProcessPatch
    {
        private const int MaxSwapGroup = 6;

        private static readonly ItemRotation[] RotationOrder =
        {
            ItemRotation.Horizontal,
            ItemRotation.Vertical
        };

        private struct MoveRecord
        {
            public Item Item;
            public ItemAddress From;
        }

        [HarmonyPrefix]
        private static bool Prefix(ItemView __instance, DragItemContext dragItemContext,
            PointerEventData eventData, ItemController ___ItemController)
        {
            try
            {
                if (DeltaInventoryPlugin.Instance == null) return true;
                if (!DeltaInventoryPlugin.Enabled.Value) return true;
                if (!DeltaInventoryPlugin.DragSwapEnabled.Value) return true;
                if (dragItemContext == null || eventData == null) return true;
                if (dragItemContext.Cancelled) return true;

                Item srcItem = dragItemContext.Item;
                if (srcItem == null) return true;

                if (eventData.pointerEnter == null) return true;
                Transform hover = eventData.pointerEnter.transform;
                if (hover == null) return true;

                ItemContext targetContext = null;
                ItemView targetView = hover.GetComponentInParent<ItemView>();
                if (targetView != null) targetContext = targetView.ItemContext;

                // Never fight the vanilla action: merge, partial transfer, put-into-container and
                // equipment slots all go through IContainerView.CanAccept.
                IContainerView genericView = hover.GetComponentInParent<IContainerView>();
                if (genericView != null)
                {
                    bool accepted;
                    try
                    {
                        accepted = genericView.CanAccept(dragItemContext, targetContext, out _);
                    }
                    catch
                    {
                        accepted = false;
                    }
                    if (accepted) return true;
                }

                if (InventoryOps.HeldCtrl()) return true;

                InventoryController controller = InventoryOps.AsInventoryController(___ItemController);
                if (controller == null) return true;

                // Equipment / weapon-mod slots: vanilla only accepts an empty slot, so a slot that
                // is already occupied is swapped here instead of silently rejecting the drop.
                SlotView slotView = genericView as SlotView;
                if (slotView != null) return SwapSlot(srcItem, dragItemContext, controller, slotView);

                GridView destinationView = hover.GetComponentInParent<GridView>();
                if (destinationView == null) return true;
                Grid destinationGrid = destinationView.Grid;
                if (destinationGrid == null) return true;

                // The source may come from a grid cell, an equipment slot or a weapon-mod slot;
                // the engine swap works with any pair of addresses, the grid is only needed for
                // the 1-to-N repacking shortcut.
                ItemAddress sourceItemAddress = dragItemContext.ItemAddress;
                if (sourceItemAddress == null) return true;
                GridItemAddress sourceAddress = sourceItemAddress as GridItemAddress;
                Grid sourceGrid = sourceAddress != null ? sourceAddress.Grid : null;
                LocationInGrid sourceLocation = sourceAddress != null ? sourceAddress.LocationInGrid : null;

                LocationInGrid raw = destinationView.CalculateItemLocation(dragItemContext);
                if (raw == null) return true;

                IntVec2 sourceSize = srcItem.CalculateRotatedSize(dragItemContext.ItemRotation);
                if (sourceSize.X <= 0 || sourceSize.Y <= 0) return true;
                if (sourceSize.X > destinationGrid.GridWidth || sourceSize.Y > destinationGrid.GridHeight) return true;

                // GridView.CalculateItemLocation clamps to the grid bounds, not to the item size.
                int maxX = Mathf.Max(0, destinationGrid.GridWidth - sourceSize.X);
                int maxY = Mathf.Max(0, destinationGrid.GridHeight - sourceSize.Y);
                int dropX = Mathf.Clamp(raw.x, 0, maxX);
                int dropY = Mathf.Clamp(raw.y, 0, maxY);
                var dropLocation = new LocationInGrid(dropX, dropY, dragItemContext.ItemRotation);

                // Dropping exactly back onto the original cell: nothing to do.
                if (sourceGrid == destinationGrid && sourceLocation.x == dropX && sourceLocation.y == dropY &&
                    sourceLocation.r == dropLocation.r)
                    return true;

                // Collect every item that overlaps the drop area.
                var overlapped = new List<KeyValuePair<Item, LocationInGrid>>();
                foreach (KeyValuePair<Item, LocationInGrid> pair in destinationGrid.ContainedItems)
                {
                    if (pair.Key == srcItem) continue;
                    LocationInGrid location = pair.Value;
                    if (location == null) continue;
                    IntVec2 size = pair.Key.CalculateRotatedSize(location.r);
                    if (Overlaps(location.x, location.y, size.X, size.Y, dropX, dropY, sourceSize.X, sourceSize.Y))
                        overlapped.Add(pair);
                }

                if (overlapped.Count == 0) return true;

                if (overlapped.Count == 1)
                    return SwapSingle(srcItem, dragItemContext, controller,
                        sourceItemAddress, sourceGrid, sourceLocation, destinationGrid, dropLocation, overlapped[0]);

                if (overlapped.Count > MaxSwapGroup) return true;
                if (sourceGrid == null || sourceLocation == null) return true;

                return SwapGroup(srcItem, dragItemContext, controller,
                    sourceGrid, sourceLocation, sourceSize, destinationGrid, dropLocation, overlapped);
            }
            catch (Exception e)
            {
                DeltaInventoryPlugin.Log.LogError("DeltaInventory: drag swap exception: " + e);
                return true;
            }
        }

        /// <summary>
        /// Swap the dragged item with the item currently sitting in an equipment or weapon-mod
        /// slot. Vanilla refuses occupied slots (ItemAlreadyThereError), so the exchange is
        /// rebuilt from the engine's atomic swap. Filter / blockage / conflict rules are still
        /// enforced by ItemManipulator.Add, an incompatible item therefore falls back to vanilla.
        /// </summary>
        private static bool SwapSlot(Item srcItem, DragItemContext dragItemContext,
            InventoryController controller, SlotView slotView)
        {
            Slot slot = slotView.Slot;
            if (slot == null) return true;
            if (slot.Locked) return true;

            Item occupant = slot.ContainedItem;
            if (occupant == null || occupant == srcItem) return true;
            if (slot.ParentItem == srcItem) return true;

            ItemAddress from = dragItemContext.ItemAddress;
            if (from == null) return true;
            if (from.Container == slot) return true;

            try
            {
                if (!slot.CheckCompatibility(srcItem)) return true;
            }
            catch
            {
                return true;
            }

            ItemAddress to = slot.CreateItemAddress();
            if (to == null) return true;

            OperationResult<SwapResult> swap = ItemManipulator.Swap(srcItem, to, occupant, from, controller, simulate: true);
            if (swap.Failed)
            {
                DeltaInventoryPlugin.Verbose("DeltaInventory: slot swap refused (" + slot.ID + "): " + ErrorOf(swap));
                return true;
            }

            DeltaInventoryPlugin.Verbose("DeltaInventory: slot swap '" + srcItem.Name + "' <-> '" + occupant.Name +
                                         "' (" + slot.ID + ")");
            dragItemContext.DragCancelled();
            _ = SubmitAsync(controller, swap, srcItem);
            return false;
        }

        /// <summary>Simple 1-to-1 exchange using the engine's atomic swap operation.</summary>
        private static bool SwapSingle(Item srcItem, DragItemContext dragItemContext,
            InventoryController controller, ItemAddress sourceAddress, Grid sourceGrid, LocationInGrid sourceLocation,
            Grid destinationGrid, LocationInGrid dropLocation,
            KeyValuePair<Item, LocationInGrid> other)
        {
            Item otherItem = other.Key;
            LocationInGrid otherLocation = other.Value;
            IntVec2 otherSize = otherItem.CalculateRotatedSize(otherLocation.r);

            // When the dragged item came from a grid the other item has to fit into exactly that
            // footprint. From a slot there is no footprint to test - the engine decides.
            if (sourceGrid != null && sourceLocation != null)
            {
                if (!FitsInGrid(sourceGrid, sourceLocation.x, sourceLocation.y, otherSize.X, otherSize.Y)) return true;
                if (OverlapsAny(sourceGrid, srcItem, otherItem,
                        sourceLocation.x, sourceLocation.y, otherSize.X, otherSize.Y)) return true;
            }

            ItemAddress from = sourceAddress;
            if (from == null && sourceGrid != null && sourceLocation != null)
                from = sourceGrid.CreateItemAddress(sourceLocation);
            if (from == null) return true;

            OperationResult<SwapResult> swap = ItemManipulator.Swap(
                srcItem, destinationGrid.CreateItemAddress(dropLocation),
                otherItem, from,
                controller, simulate: true);

            if (swap.Failed)
            {
                DeltaInventoryPlugin.Verbose("DeltaInventory: swap 1-1 refused: " + ErrorOf(swap));
                return true;
            }

            DeltaInventoryPlugin.Verbose("DeltaInventory: swap 1-1 '" + srcItem.Name + "' <-> '" + otherItem.Name + "'");
            dragItemContext.DragCancelled();
            _ = SubmitAsync(controller, swap, srcItem);
            return false;
        }

        /// <summary>
        /// 1-to-N exchange. The dragged item is parked out of the way, all overlapped items are
        /// re-packed into the area the dragged item occupied, then the dragged item is moved to
        /// the drop position. Each move is a real transaction; on failure everything is undone.
        /// </summary>
        private static bool SwapGroup(Item srcItem, DragItemContext dragItemContext,
            InventoryController controller, Grid sourceGrid, LocationInGrid sourceLocation,
            IntVec2 sourceSize, Grid destinationGrid, LocationInGrid dropLocation,
            List<KeyValuePair<Item, LocationInGrid>> overlapped)
        {
            // Region available for re-packing: exactly the area the dragged item occupied. When
            // source and destination are the same grid, the part that the dragged item will take
            // over again is excluded.
            int regionWidth = sourceSize.X;
            int regionHeight = sourceSize.Y;
            var allowed = new bool[regionWidth, regionHeight];
            for (int y = 0; y < regionHeight; y++)
            {
                for (int x = 0; x < regionWidth; x++)
                {
                    bool free = true;
                    if (sourceGrid == destinationGrid)
                    {
                        int absoluteX = sourceLocation.x + x;
                        int absoluteY = sourceLocation.y + y;
                        if (absoluteX >= dropLocation.x && absoluteX < dropLocation.x + sourceSize.X &&
                            absoluteY >= dropLocation.y && absoluteY < dropLocation.y + sourceSize.Y)
                            free = false;
                    }
                    allowed[x, y] = free;
                }
            }

            var items = new List<Item>(overlapped.Count);
            foreach (KeyValuePair<Item, LocationInGrid> pair in overlapped) items.Add(pair.Key);
            items.Sort((a, b) => Area(b).CompareTo(Area(a)));

            List<LocationInGrid> packed = TryPack(items, regionWidth, regionHeight, allowed);
            if (packed == null)
            {
                DeltaInventoryPlugin.Verbose("DeltaInventory: swap 1-N not possible (items do not fit) -> vanilla");
                return true;
            }

            // Parking spot: outside the source footprint so the re-packing area stays empty.
            LocationInGrid park = FindParkSpot(destinationGrid, srcItem,
                sourceGrid == destinationGrid ? sourceLocation : null, sourceSize);
            Grid parkGrid = destinationGrid;
            if (park == null)
            {
                park = FindParkSpot(sourceGrid, srcItem, sourceLocation, sourceSize);
                parkGrid = sourceGrid;
            }
            if (park == null)
            {
                DeltaInventoryPlugin.Verbose("DeltaInventory: swap 1-N not possible (nowhere to park) -> vanilla");
                return true;
            }

            dragItemContext.DragCancelled();
            _ = ExecuteGroupAsync(controller, srcItem, parkGrid, park, sourceGrid, sourceLocation,
                items, packed, destinationGrid, dropLocation);
            return false;
        }

        private static async Task ExecuteGroupAsync(InventoryController controller, Item srcItem,
            Grid parkGrid, LocationInGrid park, Grid sourceGrid, LocationInGrid sourceLocation,
            List<Item> items, List<LocationInGrid> packed,
            Grid destinationGrid, LocationInGrid dropLocation)
        {
            var executed = new List<MoveRecord>();
            try
            {
                bool ok = await TryMoveAsync(srcItem, parkGrid.CreateItemAddress(park), controller, executed);

                for (int i = 0; ok && i < items.Count; i++)
                {
                    LocationInGrid local = packed[i];
                    var absolute = new LocationInGrid(sourceLocation.x + local.x, sourceLocation.y + local.y, local.r);
                    ok = await TryMoveAsync(items[i], sourceGrid.CreateItemAddress(absolute), controller, executed);
                }

                if (ok)
                    ok = await TryMoveAsync(srcItem, destinationGrid.CreateItemAddress(dropLocation), controller, executed);

                if (ok)
                {
                    InventoryOps.PlayPickupSound(srcItem);
                    DeltaInventoryPlugin.Verbose("DeltaInventory: swap 1-" + items.Count + " done for '" + srcItem.Name + "'");
                    return;
                }

                DeltaInventoryPlugin.Verbose("DeltaInventory: swap 1-N step failed, rolling back " + executed.Count + " move(s)");
                for (int i = executed.Count - 1; i >= 0; i--)
                {
                    MoveRecord record = executed[i];
                    OperationResult<MoveResult> back = ItemManipulator.Move(record.Item, record.From, controller, simulate: true);
                    if (!back.Failed) await InventoryOps.Submit(controller, back);
                }
            }
            catch (Exception e)
            {
                DeltaInventoryPlugin.Log.LogError("DeltaInventory: swap 1-N exception: " + e);
            }
        }

        private static async Task<bool> TryMoveAsync(Item item, ItemAddress to, InventoryController controller,
            List<MoveRecord> executed)
        {
            if (item == null || to == null) return false;
            ItemAddress from = item.CurrentAddress;
            if (from == null) return false;

            OperationResult<MoveResult> move = ItemManipulator.Move(item, to, controller, simulate: true);
            if (move.Failed) return false;
            if (!await InventoryOps.Submit(controller, move)) return false;

            executed.Add(new MoveRecord { Item = item, From = from });
            return true;
        }

        private static async Task SubmitAsync(InventoryController controller, OperationResult operation, Item item)
        {
            if (await InventoryOps.Submit(controller, operation)) InventoryOps.PlayPickupSound(item);
        }

        /// <summary>
        /// Finds a place for <paramref name="item"/> in <paramref name="grid"/> that never overlaps
        /// the rectangle (<paramref name="forbid"/>, <paramref name="forbidSize"/>). The forbidden
        /// rectangle is the area the dragged item came from, which must stay clear so the
        /// overlapped items can be re-packed into it.
        /// </summary>
        private static LocationInGrid FindParkSpot(Grid grid, Item item,
            LocationInGrid forbid, IntVec2 forbidSize)
        {
            if (grid == null || item == null) return null;

            foreach (ItemRotation rotation in RotationOrder)
            {
                IntVec2 size = item.CalculateRotatedSize(rotation);
                if (size.X <= 0 || size.Y <= 0) continue;
                if (size.X > grid.GridWidth || size.Y > grid.GridHeight) continue;

                for (int y = 0; y <= grid.GridHeight - size.Y; y++)
                {
                    for (int x = 0; x <= grid.GridWidth - size.X; x++)
                    {
                        if (OverlapsRect(forbid, forbidSize, x, y, size.X, size.Y)) continue;
                        if (OverlapsAny(grid, item, null, x, y, size.X, size.Y)) continue;
                        return new LocationInGrid(x, y, rotation);
                    }
                }
            }

            return null;
        }

        /// <summary><paramref name="rect"/> is the top-left cell of the forbidden rectangle.</summary>
        private static bool OverlapsRect(LocationInGrid rect, IntVec2 rectSize,
            int x, int y, int w, int h)
        {
            if (rect == null || rectSize.X <= 0 || rectSize.Y <= 0) return false;
            return Overlaps(rect.x, rect.y, rectSize.X, rectSize.Y, x, y, w, h);
        }

        private static List<LocationInGrid> TryPack(List<Item> items, int regionWidth, int regionHeight, bool[,] allowed)
        {
            var used = new bool[regionWidth, regionHeight];
            var result = new List<LocationInGrid>(items.Count);
            if (Pack(items, 0, regionWidth, regionHeight, allowed, used, result)) return result;
            return null;
        }

        private static bool Pack(List<Item> items, int index, int regionWidth, int regionHeight,
            bool[,] allowed, bool[,] used, List<LocationInGrid> accumulated)
        {
            if (index >= items.Count) return true;

            Item item = items[index];
            foreach (ItemRotation rotation in RotationOrder)
            {
                IntVec2 size = item.CalculateRotatedSize(rotation);
                if (size.X <= 0 || size.Y <= 0) continue;
                if (size.X > regionWidth || size.Y > regionHeight) continue;

                for (int y = 0; y <= regionHeight - size.Y; y++)
                {
                    for (int x = 0; x <= regionWidth - size.X; x++)
                    {
                        if (!RegionFree(x, y, size.X, size.Y, allowed, used)) continue;

                        Mark(used, x, y, size.X, size.Y, true);
                        accumulated.Add(new LocationInGrid(x, y, rotation));
                        if (Pack(items, index + 1, regionWidth, regionHeight, allowed, used, accumulated)) return true;
                        accumulated.RemoveAt(accumulated.Count - 1);
                        Mark(used, x, y, size.X, size.Y, false);
                    }
                }
            }

            return false;
        }

        private static bool RegionFree(int x, int y, int w, int h, bool[,] allowed, bool[,] used)
        {
            for (int j = 0; j < h; j++)
                for (int i = 0; i < w; i++)
                    if (!allowed[x + i, y + j] || used[x + i, y + j]) return false;
            return true;
        }

        private static void Mark(bool[,] cells, int x, int y, int w, int h, bool value)
        {
            for (int j = 0; j < h; j++)
                for (int i = 0; i < w; i++)
                    cells[x + i, y + j] = value;
        }

        private static bool FitsInGrid(Grid grid, int x, int y, int w, int h)
        {
            return w > 0 && h > 0 && x >= 0 && y >= 0 &&
                   x + w <= grid.GridWidth && y + h <= grid.GridHeight;
        }

        private static bool OverlapsAny(Grid grid, Item ignoreFirst, Item ignoreSecond, int x, int y, int w, int h)
        {
            foreach (KeyValuePair<Item, LocationInGrid> pair in grid.ContainedItems)
            {
                if (pair.Key == ignoreFirst || pair.Key == ignoreSecond) continue;
                LocationInGrid location = pair.Value;
                if (location == null) continue;
                IntVec2 size = pair.Key.CalculateRotatedSize(location.r);
                if (Overlaps(location.x, location.y, size.X, size.Y, x, y, w, h)) return true;
            }
            return false;
        }

        private static bool Overlaps(int ax, int ay, int aw, int ah, int bx, int by, int bw, int bh)
        {
            return ax < bx + bw && bx < ax + aw && ay < by + bh && by < ay + ah;
        }

        private static int Area(Item item)
        {
            IntVec2 size = item.CalculateCellSize();
            return size.X * size.Y;
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
