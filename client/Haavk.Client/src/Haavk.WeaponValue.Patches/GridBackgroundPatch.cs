using System;
using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using BepInEx.Logging;
using EFT.InventoryLogic;
using EFT.UI;
using EFT.UI.DragAndDrop;
using Haavk.Core;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using JsonType;
using UnityEngine;

namespace Haavk.WeaponValue.Patches;

internal static class GridBackgroundPatch
{
	private const float BackgroundAlpha = 77f / 255f;

	private static readonly Color RepairBuffColour = new Color32(209, 153, 45, 32);

	private static bool _enabled;

	private static bool _repaintPending;

	internal static void Enable(Harmony harmony)
	{
		MethodInfo val = AccessTools.DeclaredMethod(typeof(ItemView), "UpdateStaticInfo", (Type[])null, (Type[])null);
		MethodInfo val2 = AccessTools.DeclaredMethod(typeof(GridItemView), "UpdateInfo", (Type[])null, (Type[])null);
		if (val == (MethodInfo)null || val2 == (MethodInfo)null)
		{
			HaavkIl2CppPlugin.Instance.Log.LogError("[Haavk|WpnValue] 底色挂点解析失败，格子底色不启用");
			return;
		}
		harmony.Patch((MethodBase)(object)val, (HarmonyMethod)null, new HarmonyMethod(typeof(GridBackgroundPatch).GetMethod("AfterStaticInfo", (BindingFlags)40)), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
		harmony.Patch((MethodBase)(object)val2, (HarmonyMethod)null, new HarmonyMethod(typeof(GridBackgroundPatch).GetMethod("AfterUpdateInfo", (BindingFlags)40)), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
		MethodInfo val3 = AccessTools.DeclaredMethod(typeof(ItemView), "UpdateColor", (Type[])null, (Type[])null);
		if (val3 != (MethodInfo)null)
		{
			harmony.Patch((MethodBase)(object)val3, (HarmonyMethod)null, new HarmonyMethod(typeof(GridBackgroundPatch).GetMethod("AfterUpdateColor", (BindingFlags)40)), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
		}
		else
		{
			HaavkIl2CppPlugin.Instance.Log.LogWarning("[Haavk|WpnValue] 未找到 ItemView.UpdateColor，跳蚤挂单等界面底色可能缺失");
		}
		_enabled = true;
	}

	private static void AfterStaticInfo(ItemView __instance)
	{
		try
		{
			if (TryColour(__instance.Item, out var colour))
			{
				PrivAccess.SetItemViewBg(__instance, colour);
				PrivAccess.SetItemViewObg(__instance, colour);
				__instance.UpdateColor();
				ItemViewAnimation animator = __instance.Animator;
				if ((UnityEngine.Object)(object)animator != null)
				{
					animator.Init(colour);
				}
			}
		}
		catch (Exception ex)
		{
			HaavkIl2CppPlugin.Instance.Log.LogError("[Haavk|WpnValue] 写格子底色失败: " + ex);
		}
	}

	private static void AfterUpdateInfo(GridItemView __instance)
	{
		try
		{
			Color backgroundColor = PrivAccess.ItemViewBg((ItemView)__instance);
			Color originalBackgroundColor = PrivAccess.ItemViewObg((ItemView)__instance);
			if ((!(backgroundColor != originalBackgroundColor) || !(backgroundColor != RepairBuffColour)) && TryColour(__instance.Item, out var colour) && (!(backgroundColor == colour) || !(originalBackgroundColor == colour)))
			{
				PrivAccess.SetItemViewBg((ItemView)__instance, colour);
				PrivAccess.SetItemViewObg((ItemView)__instance, colour);
				__instance.UpdateColor();
			}
		}
		catch (Exception ex)
		{
			HaavkIl2CppPlugin.Instance.Log.LogError("[Haavk|WpnValue] 刷新格子底色失败: " + ex);
		}
	}

	// 兜底：部分 ItemView 子类（跳蚤挂单 RagfairOfferItemView、可选项 SelectableItemView 等）重写了
	// UpdateStaticInfo / UpdateInfo 却不调用 base，virtual 派发直接绕开上面两个 postfix，这些界面上
	// 弹药等 pinned 物品就丢了品质底色（挂单界面还隐藏了物品名，底色是唯一线索）。UpdateColor 是所有
	// ItemView 都会走的公共出口（非虚），在这里补上 pinned 档位色即可覆盖全部界面。
	private static void AfterUpdateColor(ItemView __instance)
	{
		try
		{
			if (__instance == null || (UnityEngine.Object)(object)__instance.ColorPanel == null)
			{
				return;
			}
			if (!WeaponValueFeature.EnableBackground.Value)
			{
				return;
			}
			Color backgroundColor = PrivAccess.ItemViewBg(__instance);
			Color color = __instance.ColorPanel.color;
			if (color != backgroundColor || backgroundColor == RepairBuffColour)
			{
				// 高亮态（ColorPanel 显示 SelectedColor）或修复 buff 底色，不覆盖。
				return;
			}
			if (!WeaponValueFeature.TryPinnedTier(__instance.Item, out var tier))
			{
				return;
			}
			PerSlotColouring colouring = WeaponValueFeature.Colouring;
			if (colouring == null || !TryPinnedColour(colouring, tier, out var colour))
			{
				return;
			}
			if (backgroundColor == colour)
			{
				return;
			}
			PrivAccess.SetItemViewBg(__instance, colour);
			PrivAccess.SetItemViewObg(__instance, colour);
			__instance.ColorPanel.color = colour;
			ItemViewAnimation animator = __instance.Animator;
			if ((UnityEngine.Object)(object)animator != null)
			{
				animator.Init(colour);
			}
		}
		catch (Exception ex)
		{
			HaavkIl2CppPlugin.Instance.Log.LogError("[Haavk|WpnValue] 通用底色补齐失败: " + ex);
		}
	}

	internal static void RequestRepaint()
	{
		if (!_enabled || _repaintPending)
		{
			return;
		}
		_repaintPending = true;
		try
		{
			if (HaavkCoroutineHost.Run(RepaintAtEndOfFrame()) == null)
			{
				_repaintPending = false;
			}
		}
		catch (Exception ex)
		{
			_repaintPending = false;
			HaavkIl2CppPlugin.Instance.Log.LogError("[Haavk|WpnValue] 排重刷协程失败: " + ex);
		}
	}

	private static IEnumerator RepaintAtEndOfFrame()
	{
		yield return null;
		_repaintPending = false;
		RepaintOpenViews();
	}

	private static void RepaintOpenViews()
	{
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b1: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			ItemView[] il2CppArrayBase = UnityEngine.Object.FindObjectsByType<ItemView>(FindObjectsSortMode.None);
			int num = 0;
			int num2 = 0;
			for (int i = 0; i < il2CppArrayBase.Length; i++)
			{
				try
				{
					if (RepaintOne(il2CppArrayBase[i]))
					{
						num++;
					}
				}
				catch
				{
					num2++;
				}
			}
			ManualLogSource log = HaavkIl2CppPlugin.Instance.Log;
			object[] buffer = new object[4];
			buffer[0] = "[Haavk|WpnValue]";
			buffer[1] = (object)num;
			buffer[2] = (object)il2CppArrayBase.Length;
			buffer[3] = ((num2 == 0) ? "" : ("（" + num2 + " 个失败）"));
			log.LogDebug(string.Format("{0} F12 改色，就地重刷 {1}/{2} 个格子{3}", buffer));
		}
		catch (Exception ex)
		{
			HaavkIl2CppPlugin.Instance.Log.LogError("[Haavk|WpnValue] 就地重刷底色失败: " + ex);
		}
	}

	private static bool RepaintOne(ItemView view)
	{
		if (view == null)
		{
			return false;
		}
		Color backgroundColor = PrivAccess.ItemViewBg(view);
		Color originalBackgroundColor = PrivAccess.ItemViewObg(view);
		if (backgroundColor != originalBackgroundColor && backgroundColor != RepairBuffColour)
		{
			return false;
		}
		if (!TryColour(view.Item, out var colour))
		{
			return false;
		}
		if (backgroundColor == colour && originalBackgroundColor == colour)
		{
			return false;
		}
		PrivAccess.SetItemViewBg(view, colour);
		PrivAccess.SetItemViewObg(view, colour);
		view.UpdateColor();
		ItemViewAnimation animator = view.Animator;
		if ((UnityEngine.Object)(object)animator != null)
		{
			animator.Init(colour);
		}
		return true;
	}

	private static bool TryColour(Item item, out Color colour)
	{
		colour = default;
		if (item == null)
		{
			return false;
		}
		if (!WeaponValueFeature.EnableBackground.Value)
		{
			return false;
		}
		PerSlotColouring colouring = WeaponValueFeature.Colouring;
		if (colouring == null)
		{
			return false;
		}
		if (WeaponValueFeature.TryPinnedTier(item, out var pinnedTier))
		{
			// 弹药等按「品质档位」上色：名字颜色由服务端 locale 上色，这里补格子底色。
			return TryPinnedColour(colouring, pinnedTier, out colour);
		}
		Weapon weapon = Il2CppCast.As<Weapon>(item);
		if (weapon != null)
		{
			return TryWeaponColour(weapon, colouring, out colour);
		}
		if (Il2CppCast.As<Ammo>(item) != null)
		{
			return TryPerSlotColour(item, colouring, out colour);
		}
		if (!WeaponValueFeature.IsTraderSource && TryBannedRecolour(item, colouring, out colour))
		{
			return true;
		}
		return TryTierColour(item, colouring, out colour);
	}

	private static bool TryPinnedColour(PerSlotColouring rule, int tier, out Color colour)
	{
		colour = default;
		var palette = rule.Palette;
		if (palette == null || tier < 0 || tier >= palette.Count)
		{
			return false;
		}
		string hex = palette[tier];
		if (string.IsNullOrEmpty(hex) || !ColorUtility.TryParseHtmlString(hex, out colour))
		{
			return false;
		}
		colour.a = 77f / 255f;
		return true;
	}

	internal static bool TryCellColour(Item item, out Color colour)
	{
		colour = default;
		if (item == null)
		{
			return false;
		}
		if (!TryColour(item, out colour))
		{
			if (!TryTemplateHex(item, out var hex))
			{
				return false;
			}
			if (!ColorUtility.TryParseHtmlString(hex, out colour))
			{
				return false;
			}
		}
		colour.a = 1f;
		return true;
	}

	private static bool TryWeaponColour(Weapon weapon, PerSlotColouring rule, out Color colour)
	{
		colour = default;
		IntVec2 intVec = weapon.CalculateCellSize();
		double total = WeaponTotalCalculator.Sum(weapon);
		bool canSellOnFlea = ((ItemTemplate)weapon.Template).CanSellOnRagfair || !WeaponValueFeature.BannedColourEnabled.Value;
		string text = rule.Colour(total, intVec.X, intVec.Y, canSellOnFlea);
		if (text == null)
		{
			return false;
		}
		if (rule.UsesBannedColour(canSellOnFlea))
		{
			colour = WeaponValueFeature.FleaBannedColour.Value;
		}
		else if (!ColorUtility.TryParseHtmlString(text, out colour))
		{
			return false;
		}
		colour.a = 77f / 255f;
		return true;
	}

	private static bool TryBannedRecolour(Item item, PerSlotColouring rule, out Color colour)
	{
		colour = default;
		string bannedColour = rule.BannedColour;
		if (bannedColour == null)
		{
			return false;
		}
		if (!TryTemplateHex(item, out var hex))
		{
			return false;
		}
		if (!ColorUtility.TryParseHtmlString(bannedColour, out var color))
		{
			return false;
		}
		if (!ColorUtility.TryParseHtmlString(hex, out var color2))
		{
			return false;
		}
		if (!Mathf.Approximately(color.r, color2.r) || !Mathf.Approximately(color.g, color2.g) || !Mathf.Approximately(color.b, color2.b))
		{
			return false;
		}
		if (WeaponValueFeature.BannedColourEnabled.Value)
		{
			colour = WeaponValueFeature.FleaBannedColour.Value;
		}
		else if (!TryTierColour(item, rule, out colour))
		{
			colour = color;
		}
		colour.a = 77f / 255f;
		return true;
	}

	private static bool TryArmourColour(Item item, out Color colour)
	{
		colour = default;
		if (!WeaponValueFeature.ColourArmourBackground)
		{
			return false;
		}
		ArmourClassColouring armourColouring = WeaponValueFeature.ArmourColouring;
		if (armourColouring == null)
		{
			return false;
		}
		if (!ArmourReader.TryMaxArmourClass(item, out var armourClass))
		{
			return false;
		}
		string text = armourColouring.Colour(armourClass);
		if (text == null)
		{
			return false;
		}
		if (!ColorUtility.TryParseHtmlString(text, out colour))
		{
			return false;
		}
		colour.a = 77f / 255f;
		return true;
	}

	private static bool TryTierColour(Item item, PerSlotColouring rule, out Color colour)
	{
		if (!TryArmourColour(item, out colour))
		{
			return TryPerSlotColour(item, rule, out colour);
		}
		return true;
	}

	private static bool TryPerSlotColour(Item item, PerSlotColouring rule, out Color colour)
	{
		colour = default;
		if (!((Il2CppCast.As<Key>(item) != null) ? WeaponValueFeature.ColourKeyBackground : WeaponValueFeature.ColourItemBackground))
		{
			return false;
		}
		IntVec2 intVec = item.CalculateCellSize();
		string text = rule.DensityColour(WeaponTotalCalculator.Total(item), intVec.X, intVec.Y);
		if (text == null)
		{
			return false;
		}
		if (!ColorUtility.TryParseHtmlString(text, out colour))
		{
			return false;
		}
		colour.a = 77f / 255f;
		return true;
	}

	private static bool TryTemplateHex(Item item, out string hex)
	{
		hex = null;
		ItemTemplate template = item.Template;
		if (template == null)
		{
			return false;
		}
		TaxonomyColor backgroundColor = template.BackgroundColor;
		if (Enum.IsDefined(typeof(TaxonomyColor), backgroundColor))
		{
			return false;
		}
		return TaxonomyColourCodec.TryDecodeCustomHex((int)backgroundColor, TaxonomyColourMapping.Offset, out hex);
	}
}
