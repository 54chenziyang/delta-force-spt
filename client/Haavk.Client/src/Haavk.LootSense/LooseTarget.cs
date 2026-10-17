using System.Collections.Generic;
using EFT;
using EFT.Interactive;
using EFT.InventoryLogic;
using UnityEngine;

namespace Haavk.LootSense;

internal sealed class LooseTarget : SenseTarget
{
	private readonly ObservedLootItem _loot;

	private readonly Collider _collider;

	private readonly Item _item;

	private readonly bool _hasGrids;

	internal override bool Alive
	{
		get
		{
			if (_loot == null || _collider == null)
			{
				return false;
			}
			GameObject gameObject = _loot.gameObject;
			if (gameObject != null)
			{
				return gameObject.activeInHierarchy;
			}
			return false;
		}
	}

	internal override bool Refreshable => _hasGrids;

	internal LooseTarget(ObservedLootItem loot, Collider collider, Item item)
	{
		_loot = loot;
		_collider = collider;
		_item = item;
		_hasGrids = ItemWalk.HasGrids(item);
	}

	internal override Vector3 Anchor()
	{
		return SenseTarget.TopOf(_collider);
	}

	internal override SenseFacts Evaluate(Player player)
	{
		if (!_hasGrids)
		{
			return SenseFacts.Build(_item, player);
		}
		List<Item> list = new List<Item>();
		ItemWalk.CollectGridContents(_item, list);
		if (list.Count == 0)
		{
			return SenseFacts.Build(_item, player);
		}
		list.Insert(0, _item);
		return SenseFacts.BuildGroup(SenseFacts.CategoryName(_item), list, player);
	}
}
