using System.Collections.Generic;
using EFT;
using EFT.Interactive;
using EFT.InventoryLogic;
using Il2CppInterop.Runtime.InteropTypes;
using UnityEngine;

namespace Haavk.LootSense;

internal sealed class ContainerTarget : SenseTarget
{
	private readonly LootableContainer _container;

	private readonly Collider _collider;

	internal override bool Alive
	{
		get
		{
			if (_container == null || _collider == null)
			{
				return false;
			}
			GameObject gameObject = _container.gameObject;
			if (gameObject != null)
			{
				return gameObject.activeInHierarchy;
			}
			return false;
		}
	}

	internal override bool Refreshable => true;

	internal ContainerTarget(LootableContainer container, Collider collider)
	{
		_container = container;
		_collider = collider;
	}

	internal override Vector3 Anchor()
	{
		return SenseTarget.TopOf(_collider);
	}

	internal override SenseFacts Evaluate(Player player)
	{
		ItemController itemOwner = _container.ItemOwner;
		Item item = ((itemOwner == null) ? null : itemOwner.RootItem);
		if (item == null)
		{
			return null;
		}
		List<Item> list = new List<Item>();
		ItemWalk.CollectGridContents(item, list);
		return SenseFacts.BuildGroup(SenseFacts.DisplayName(item), list, player);
	}
}
