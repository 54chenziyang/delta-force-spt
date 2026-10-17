using System;
using System.Reflection;
using Comfort.Common;
using EFT.HandBook;
using EFT.InventoryLogic;
using Haavk.Core;
using Il2CppInterop.Runtime.InteropTypes;

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Haavk.WeaponValue;

internal static class WeaponTotalCalculator
{
	private static bool _handbookMissWarned;

	internal static double Sum(Weapon weapon)
	{
		double num = PriceOf(weapon);
		IEnumerable<Mod> mods = weapon.Mods;
		if (mods != null)
		{
			IEnumerator<Mod> enumerator = mods.GetEnumerator();
			try
			{
				while (((IEnumerator)enumerator).MoveNext())
				{
					Mod current = enumerator.Current;
					if (!(current == null))
					{
						num += PriceOf(current);
					}
				}
			}
			finally
			{
				((IDisposable)enumerator)?.Dispose();
			}
		}
		return num;
	}

	internal static double Total(Item item)
	{
		Weapon weapon = Il2CppCast.As<Weapon>(item);
		if (weapon != null)
		{
			return Sum(weapon);
		}
		double num = PriceOf(item) * (double)StackOf(item);
		CompoundItem compoundItem = Il2CppCast.As<CompoundItem>(item);
		if (compoundItem != null)
		{
			IEnumerator<Slot> enumerator = compoundItem.AllSlots.GetEnumerator();
			try
			{
				while (((IEnumerator)enumerator).MoveNext())
				{
					Slot current = enumerator.Current;
					Item item2 = ((current != null) ? current.ContainedItem : null);
					if (!(item2 == null))
					{
						num += PriceOf(item2) * (double)StackOf(item2);
					}
				}
			}
			finally
			{
				((IDisposable)enumerator)?.Dispose();
			}
		}
		return num;
	}

	internal static double GearValue(Item equipment)
	{
		double num = 0.0;
		ContainerCollection containerCollection = Il2CppCast.As<ContainerCollection>(equipment);
		if (containerCollection != null)
		{
			Accumulate(containerCollection, ref num);
		}
		return num;
	}

	private static void Accumulate(ContainerCollection collection, ref double total)
	{
		IEnumerable<IContainer> containers = collection.Containers;
		if (containers == null)
		{
			return;
		}
		IEnumerator<IContainer> enumerator = containers.GetEnumerator();
		try
		{
			while (((IEnumerator)enumerator).MoveNext())
			{
				IContainer container = enumerator.Current;
				if (container == null || string.Equals(container.ID, "SecuredContainer", StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}
				IEnumerable<Item> items = container.Items;
				if (items == null)
				{
					continue;
				}
				IEnumerator<Item> enumerator2 = items.GetEnumerator();
				try
				{
					while (((IEnumerator)enumerator2).MoveNext())
					{
						Item item = enumerator2.Current;
						if (item == null)
						{
							continue;
						}
						total += PriceOf(item) * (double)StackOf(item);
						ContainerCollection containerCollection = Il2CppCast.As<ContainerCollection>(item);
						if (containerCollection != null)
						{
							Accumulate(containerCollection, ref total);
						}
					}
				}
				finally
				{
					((IDisposable)enumerator2)?.Dispose();
				}
			}
		}
		finally
		{
			((IDisposable)enumerator)?.Dispose();
		}
	}

	private static int StackOf(Item item)
	{
		return ValuationMath.StackMultiplier(item.StackObjectsCount, item.StackMaxSize, item.UnlimitedCount);
	}

	private static double HandbookPrice(Item item)
	{
		//IL_005d: Expected Obj, but got VT
		if (!Singleton<Handbook>.Instantiated)
		{
			return 0.0;
		}
		try
		{
			return Singleton<Handbook>.Instance.GetBasePrice(item.TemplateId);
		}
		catch (Exception ex)
		{
			if (!_handbookMissWarned)
			{
				_handbookMissWarned = true;
				HaavkIl2CppPlugin.Instance.Log.LogDebug(string.Format("{0} 手册里没有模板 {1}，按 0 计（任务件/特殊件常见，只打一次）: {2}", "[Haavk|WpnValue]", item.TemplateId, ((MemberInfo)ex.GetType()).Name));
			}
			return 0.0;
		}
	}

	private static double PriceOf(Item item)
	{
		//IL_0017: Expected Obj, but got VT
		double handbookPrice = HandbookPrice(item);
		return WeaponValueFeature.Book.Resolve(((System.Object)item.TemplateId).ToString(), item.Template.CanSellOnRagfair, handbookPrice);
	}

	internal static string Line(Item item)
	{
		double total = Total(item);
		IntVec2 intVec = item.CalculateCellSize();
		string tierColour = WeaponValueFeature.Colouring?.DensityColour(total, intVec.X, intVec.Y);
		string text = DescriptionLine.TotalAndPerSlot(total, intVec.X, intVec.Y, WeaponValueFeature.LabelTotal.Value, WeaponValueFeature.LabelPerSlot.Value, tierColour, WeaponValueFeature.PriceLine.Value, WeaponValueFeature.Style);
		if (!item.Template.CanSellOnRagfair && !WeaponValueFeature.IsTraderSource)
		{
			string arg = "#" + ColorUtility.ToHtmlStringRGB(WeaponValueFeature.FleaBannedColour.Value);
			text = $"<color={arg}>{WeaponValueFeature.LabelBanned.Value}</color> {text}";
		}
		return text;
	}
}
