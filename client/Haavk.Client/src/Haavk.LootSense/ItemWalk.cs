using System.Collections.Generic;
using EFT.InventoryLogic;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using System.Collections;
using System.Collections.Generic;

namespace Haavk.LootSense;

internal static class ItemWalk
{
	private const int MaxDepth = 6;

	internal static bool HasGrids(Item item)
	{
		CompoundItem compoundItem = Il2CppCast.As<CompoundItem>(item);
		if (compoundItem == null)
		{
			return false;
		}
		Grid[] grids = compoundItem.Grids;
		if (grids != null)
		{
			return grids.Length > 0;
		}
		return false;
	}

	internal static void CollectGridContents(Item root, System.Collections.Generic.List<Item> into)
	{
		Collect(root, into, 0);
	}

	private static void Collect(Item root, System.Collections.Generic.List<Item> into, int depth)
	{
		if (depth > 6)
		{
			return;
		}
		CompoundItem compoundItem = Il2CppCast.As<CompoundItem>(root);
		if (compoundItem == null)
		{
			return;
		}
		Grid[] grids = compoundItem.Grids;
		if (grids == null)
		{
			return;
		}
		for (int i = 0; i < grids.Length; i++)
		{
			Grid grid = grids[i];
			if (grid == null)
			{
				continue;
			}
			System.Collections.Generic.IEnumerable<Item> items = grid.Items;
			if (items == null)
			{
				continue;
			}
			IEnumerator enumerator = (items.GetEnumerator()).Il2CppAs<IEnumerator>();
			if (enumerator == null)
			{
				continue;
			}
			while (enumerator.MoveNext())
			{
				Item item = Il2CppCast.As<Item>(enumerator.Current);
				if (!(item == null))
				{
					into.Add(item);
					Collect(item, into, depth + 1);
				}
			}
		}
	}
}
