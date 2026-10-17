using EFT.InventoryLogic;
using Il2CppInterop.Runtime.InteropTypes;
using System.Collections.Generic;

namespace Haavk.WeaponValue;

internal static class ArmourReader
{
	private static readonly List<ArmorComponent> _buffer = new List<ArmorComponent>();

	internal static bool TryMaxArmourClass(Item item, out int armourClass)
	{
		armourClass = 0;
		if (item == null)
		{
			return false;
		}
		bool flag = item.GetItemComponent<ArmorHolderComponent>() != null;
		_buffer.Clear();
		ItemExtensions.GetItemComponentsInChildrenNonAlloc<ArmorComponent>(item, _buffer, true);
		for (int i = 0; i < _buffer.Count; i++)
		{
			int armorClass = _buffer[i].ArmorClass;
			if (armorClass > armourClass)
			{
				armourClass = armorClass;
			}
		}
		_buffer.Clear();
		if (!flag)
		{
			// 面罩/护面即使在无防护等级（0 档、无板甲）时也按防护等级上色（最低档），与护甲保持一致
			if (armourClass <= 0 && Il2CppCast.As<FaceCover>(item) != null)
			{
				return true;
			}
			return armourClass > 0;
		}
		return true;
	}
}
