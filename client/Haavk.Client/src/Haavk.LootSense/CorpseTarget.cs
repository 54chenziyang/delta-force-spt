using System.Collections.Generic;
using EFT;
using EFT.Interactive;
using EFT.InventoryLogic;
using Il2CppInterop.Runtime.InteropTypes;
using UnityEngine;

namespace Haavk.LootSense;

internal sealed class CorpseTarget : SenseTarget
{
	private static readonly EquipmentSlot[] Slots = new EquipmentSlot[11]
	{
		EquipmentSlot.FirstPrimaryWeapon,
		EquipmentSlot.SecondPrimaryWeapon,
		EquipmentSlot.Holster,
		EquipmentSlot.ArmorVest,
		EquipmentSlot.Headwear,
		EquipmentSlot.Earpiece,
		EquipmentSlot.Eyewear,
		EquipmentSlot.FaceCover,
		EquipmentSlot.TacticalVest,
		EquipmentSlot.Backpack,
		EquipmentSlot.Pockets
	};

	private const float AbovePelvis = 0.65999997f;

	private readonly Corpse _corpse;

	internal override bool Alive
	{
		get
		{
			if (_corpse == null)
			{
				return false;
			}
			GameObject gameObject = _corpse.gameObject;
			if (gameObject != null)
			{
				return gameObject.activeInHierarchy;
			}
			return false;
		}
	}

	internal override bool Refreshable => true;

	internal CorpseTarget(Corpse corpse)
	{
		_corpse = corpse;
	}

	internal override Vector3 Anchor()
	{
		Transform transform = _corpse.TrackableTransform;
		if (transform == null)
		{
			transform = _corpse.transform;
		}
		Vector3 position = transform.position;
		return SenseMath.Vec(position.x, position.y + 0.65999997f, position.z);
	}

	internal override SenseFacts Evaluate(Player player)
	{
		ItemController itemOwner = ((LootItem)_corpse).ItemOwner;
		InventoryEquipment inventoryEquipment = ((itemOwner == null) ? null : Il2CppCast.As<InventoryEquipment>(itemOwner.RootItem));
		if (inventoryEquipment == null)
		{
			return null;
		}
		List<Item> list = new List<Item>();
		EquipmentSlot[] slots = Slots;
		foreach (EquipmentSlot equipmentSlot in slots)
		{
			Slot slot = inventoryEquipment.GetSlot(equipmentSlot);
			Item item = ((slot == null) ? null : slot.ContainedItem);
			if (!(item == null))
			{
				if (equipmentSlot != EquipmentSlot.Pockets)
				{
					list.Add(item);
				}
				ItemWalk.CollectGridContents(item, list);
			}
		}
		return SenseFacts.BuildGroup(SideLabel(_corpse.Side), list, player);
	}

	private static string SideLabel(EPlayerSide side)
	{
		return side switch
		{
			EPlayerSide.Usec => "USEC", 
			EPlayerSide.Bear => "BEAR", 
			_ => "Scav", 
		};
	}
}
