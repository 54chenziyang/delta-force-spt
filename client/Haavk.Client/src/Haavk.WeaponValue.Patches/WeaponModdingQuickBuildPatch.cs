using System;
using System.Collections.Generic;
using System.Reflection;
using EFT;
using EFT.Communications;
using EFT.InventoryLogic;
using EFT.UI;
using EFT.UI.Builds;
using EFT.UI.Ragfair;
using EFT.UI.WeaponModding;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Haavk.WeaponValue.Patches;

/// <summary>
/// 改枪界面「一键补齐」按钮（复刻三角洲行动的付费买齐）。
/// 改枪界面本身没有目标构建，空插槽也不含 templateId，无法推断该买什么，
/// 因此点按钮后先找这把枪已保存的武器构建：唯一一个直接走，多个弹选择列表，
/// 一个都没有则提示去「编辑构建」界面先存一个。
/// 选定构建后复用原版链路：BuildItemSearchValue + CancellableFilters.Store/Clear
/// + ExternalRagfairSearch(EFilterType.BuildItems) → 跳蚤自动筛出并全选缺失件。
/// </summary>
internal static class WeaponModdingQuickBuildPatch
{
	private const string ButtonName = "HaavkQuickBuildButton";

	private const string ListName = "HaavkQuickBuildList";

	private const float ButtonWidth = 104f;

	private const float ButtonHeight = 30f;

	private const float RowHeight = 30f;

	private const float RowGap = 4f;

	private const float PanelWidth = 260f;

	internal static void Enable(Harmony harmony)
	{
		try
		{
			MethodInfo methodInfo = AccessTools.DeclaredMethod(typeof(WeaponModdingScreen), "Show", new Type[3]
			{
				typeof(Item),
				typeof(InventoryController),
				typeof(CompoundItem[])
			}, (Type[])null);
			if (methodInfo == (MethodInfo)null)
			{
				HaavkIl2CppPlugin.Instance.Log.LogError("[Haavk|WpnValue] 找不到 WeaponModdingScreen.Show(Item,InventoryController,CompoundItem[])，改枪一键补齐不启用");
				return;
			}
			harmony.Patch((MethodBase)(object)methodInfo, (HarmonyMethod)null, new HarmonyMethod(typeof(WeaponModdingQuickBuildPatch).GetMethod("Postfix", (BindingFlags)40)), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
			HaavkIl2CppPlugin.Instance.Log.LogInfo("[Haavk|WpnValue] 改枪一键补齐已启用");
		}
		catch (Exception ex)
		{
			HaavkIl2CppPlugin.Instance.Log.LogError("[Haavk|WpnValue] 改枪一键补齐挂载失败: " + ex);
		}
	}

	private static void Postfix(WeaponModdingScreen __instance, Item item)
	{
		try
		{
			if ((UnityEngine.Object)(object)__instance != null && item is Weapon)
			{
				Attach(__instance, item);
			}
		}
		catch (Exception ex)
		{
			HaavkIl2CppPlugin.Instance.Log.LogError("[Haavk|WpnValue] 改枪一键补齐按钮创建失败: " + ex);
		}
	}

	private static void Attach(WeaponModdingScreen screen, Item item)
	{
		RectTransform rootRect = ((Component)(object)screen).GetComponent<RectTransform>();
		if (rootRect == null)
		{
			HaavkIl2CppPlugin.Instance.Log.LogWarning("[Haavk|WpnValue] 一键补齐：根 RectTransform 缺失，跳过");
			return;
		}
		DestroyChild(rootRect, ButtonName);
		DestroyChild(rootRect, ListName);
		int layer = ((Component)(object)rootRect).gameObject.layer;
		GameObject val = new GameObject(ButtonName);
		RectTransform rectTransform = val.AddComponent<RectTransform>();
		Image image = val.AddComponent<Image>();
		image.color = ParseHex("#c8a24a");
		image.raycastTarget = true;
		LayoutElement layoutElement = val.AddComponent<LayoutElement>();
		layoutElement.ignoreLayout = true;
		rectTransform.SetParent((Transform)(object)rootRect, worldPositionStays: false);
		rectTransform.anchorMin = new Vector2(0f, 1f);
		rectTransform.anchorMax = new Vector2(0f, 1f);
		rectTransform.pivot = new Vector2(0f, 0.5f);
		rectTransform.localScale = Vector3.one;
		LayoutRebuilder.ForceRebuildLayoutImmediate(rootRect);
		Vector2 vector = new Vector2((0f - rootRect.pivot.x) * rootRect.rect.width, (1f - rootRect.pivot.y) * rootRect.rect.height);
		float num;
		float num2;
		if (screen._headerLabel != null)
		{
			RectTransform component = ((Component)(object)screen._headerLabel).GetComponent<RectTransform>();
			Vector3[] array = new Vector3[4];
			component.GetWorldCorners(array);
			Vector3 vector2 = rootRect.InverseTransformPoint(array[2]);
			Vector3 vector3 = rootRect.InverseTransformPoint(array[3]);
			num = vector2.x + 10f;
			num2 = (vector2.y + vector3.y) * 0.5f;
		}
		else
		{
			num = vector.x + 20f;
			num2 = vector.y - 30f;
		}
		rectTransform.sizeDelta = new Vector2(ButtonWidth, ButtonHeight);
		rectTransform.anchoredPosition = new Vector2(num - vector.x, num2 - vector.y);
		rectTransform.SetAsLastSibling();
		val.layer = layer;
		AddLabel(rectTransform, FindTemplate(rootRect), "一键补齐", ParseHex("#1a1204"), layer);
		Button button = val.AddComponent<Button>();
		button.targetGraphic = (Graphic)(object)image;
		UnityAction call = () => OnClick(screen, item);
		button.onClick.AddListener(call);
		HaavkIl2CppPlugin.Instance.Log.LogInfo("[Haavk|WpnValue] 一键补齐按钮已挂到改枪界面 anchor=" + rectTransform.anchoredPosition + " size=" + rectTransform.sizeDelta);
	}

	private static void OnClick(WeaponModdingScreen screen, Item item)
	{
		try
		{
			if (!(item is Weapon weapon))
			{
				return;
			}
			ItemUiContext itemUiContext = ItemUiContext.Instance;
			if ((UnityEngine.Object)(object)itemUiContext == null)
			{
				Notify("界面未就绪，稍后再试");
				return;
			}
			IEftSession session = itemUiContext.Session;
			if (session == null)
			{
				Notify("会话未就绪，稍后再试");
				return;
			}
			WeaponBuildsStorage weaponBuildsStorage = session.WeaponBuildsStorage;
			if (weaponBuildsStorage == null)
			{
				Notify("构建存储未就绪，稍后再试");
				return;
			}
			List<WeaponBuild> list = new List<WeaponBuild>();
			foreach (WeaponBuild value in weaponBuildsStorage.WeaponBuilds.Values)
			{
				if (value != null && value.Item != null && value.Item.TemplateId == weapon.TemplateId)
				{
					list.Add(value);
				}
			}
			if (list.Count == 0)
			{
				Notify("该武器没有已保存的构建，请先在「编辑构建」界面保存一个");
			}
			else if (list.Count == 1)
			{
				RunPurchase(list[0], weapon, session);
			}
			else
			{
				ShowList(screen, weapon, list, session);
			}
		}
		catch (Exception ex)
		{
			HaavkIl2CppPlugin.Instance.Log.LogError("[Haavk|WpnValue] 一键补齐执行失败: " + ex);
		}
	}

	private static void ShowList(WeaponModdingScreen screen, Weapon weapon, List<WeaponBuild> builds, IEftSession session)
	{
		RectTransform rootRect = ((Component)(object)screen).GetComponent<RectTransform>();
		if (rootRect == null)
		{
			return;
		}
		Transform val = ((Transform)(object)rootRect).Find(ListName);
		if ((UnityEngine.Object)(object)val != null)
		{
			UnityEngine.Object.Destroy(((Component)(object)val).gameObject);
			return;
		}
		int layer = ((Component)(object)rootRect).gameObject.layer;
		GameObject val2 = new GameObject(ListName);
		RectTransform rectTransform = val2.AddComponent<RectTransform>();
		Image image = val2.AddComponent<Image>();
		image.color = new Color(0f, 0f, 0f, 0.85f);
		LayoutElement layoutElement = val2.AddComponent<LayoutElement>();
		layoutElement.ignoreLayout = true;
		rectTransform.SetParent((Transform)(object)rootRect, worldPositionStays: false);
		rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
		rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
		rectTransform.pivot = new Vector2(0.5f, 0.5f);
		rectTransform.localScale = Vector3.one;
		rectTransform.sizeDelta = new Vector2(PanelWidth, (float)builds.Count * RowHeight + (float)(builds.Count - 1) * RowGap + 16f);
		rectTransform.anchoredPosition = Vector2.zero;
		rectTransform.SetAsLastSibling();
		val2.layer = layer;
		TextMeshProUGUI textMeshProUGUI = FindTemplate(rootRect);
		for (int i = 0; i < builds.Count; i++)
		{
			WeaponBuild weaponBuild = builds[i];
			GameObject val3 = new GameObject("HaavkQuickBuildRow" + i);
			RectTransform rectTransform2 = val3.AddComponent<RectTransform>();
			Image image2 = val3.AddComponent<Image>();
			image2.color = ParseHex("#2b2b2b");
			image2.raycastTarget = true;
			rectTransform2.SetParent((Transform)(object)rectTransform, worldPositionStays: false);
			rectTransform2.anchorMin = new Vector2(0f, 1f);
			rectTransform2.anchorMax = new Vector2(1f, 1f);
			rectTransform2.pivot = new Vector2(0.5f, 1f);
			rectTransform2.offsetMin = new Vector2(8f, 0f);
			rectTransform2.offsetMax = new Vector2(-8f, 0f);
			rectTransform2.sizeDelta = new Vector2(rectTransform2.sizeDelta.x, RowHeight);
			rectTransform2.anchoredPosition = new Vector2(0f, 0f - (8f + (float)i * (RowHeight + RowGap)));
			rectTransform2.localScale = Vector3.one;
			val3.layer = layer;
			AddLabel(rectTransform2, textMeshProUGUI, BuildLabel(weaponBuild), ParseHex("#e8e8e8"), layer);
			Button button = val3.AddComponent<Button>();
			button.targetGraphic = (Graphic)(object)image2;
			WeaponBuild picked = weaponBuild;
			UnityAction call = delegate
			{
				UnityEngine.Object.Destroy(val2);
				RunPurchase(picked, weapon, session);
			};
			button.onClick.AddListener(call);
		}
	}

	private static void RunPurchase(WeaponBuild build, Weapon weapon, IEftSession session)
	{
		Dictionary<string, int> dictionary = new Dictionary<string, int>();
		HashSet<string> hashSet = new HashSet<string>();
		if (weapon is ContainerCollection containerCollection)
		{
			foreach (Item containedItem in containerCollection.GetAllItemsFromCollection())
			{
				if (containedItem is Mod && !(containedItem is Magazine))
				{
					hashSet.Add(containedItem.StringTemplateId);
				}
			}
		}
		string text = ((object)weapon.TemplateId).ToString();
		if (build.IncludedItems != null)
		{
			foreach (string includedItem in build.IncludedItems)
			{
				if (string.IsNullOrEmpty(includedItem) || includedItem == text || hashSet.Contains(includedItem))
				{
					continue;
				}
				if (dictionary.ContainsKey(includedItem))
				{
					dictionary[includedItem]++;
				}
				else
				{
					dictionary[includedItem] = 1;
				}
			}
		}
		if (dictionary.Count == 0)
		{
			Notify("该枪已配齐，无需补齐");
			return;
		}
		BuildItemSearchValue buildItemSearchValue = new BuildItemSearchValue
		{
			BuildName = (build.HandbookName ?? string.Empty),
			BuildItems = dictionary,
			BuildCount = 1
		};
		session.RagFair.CancellableFilters.Store();
		session.RagFair.CancellableFilters.Clear();
		session.RagFair.ExternalRagfairSearch(new RagfairSearch(EFilterType.BuildItems, buildItemSearchValue));
		Notify("已按构建「" + BuildLabel(build) + "」筛出 " + dictionary.Count + " 种缺失配件");
		HaavkIl2CppPlugin.Instance.Log.LogInfo("[Haavk|WpnValue] 一键补齐：构建=" + BuildLabel(build) + " 缺失种类=" + dictionary.Count);
	}

	private static string BuildLabel(WeaponBuild build)
	{
		if (build == null)
		{
			return "构建";
		}
		if (!string.IsNullOrEmpty(build.HandbookName))
		{
			return build.HandbookName;
		}
		return "未命名构建";
	}

	private static void Notify(string message)
	{
		try
		{
			NotificationManager.DisplayMessageNotification(message);
		}
		catch (Exception ex)
		{
			HaavkIl2CppPlugin.Instance.Log.LogWarning("[Haavk|WpnValue] 一键补齐提示失败: " + ex.Message);
		}
	}

	private static void DestroyChild(RectTransform root, string name)
	{
		Transform val = ((Transform)(object)root).Find(name);
		if ((UnityEngine.Object)(object)val != null)
		{
			UnityEngine.Object.Destroy(((Component)(object)val).gameObject);
		}
	}

	private static void AddLabel(RectTransform parent, TextMeshProUGUI template, string text, Color color, int layer)
	{
		GameObject val = new GameObject("Label");
		RectTransform rectTransform = val.AddComponent<RectTransform>();
		TextMeshProUGUI textMeshProUGUI = val.AddComponent<TextMeshProUGUI>();
		if (template != null)
		{
			textMeshProUGUI.font = template.font;
			textMeshProUGUI.fontSharedMaterial = template.fontSharedMaterial;
			textMeshProUGUI.fontSize = Mathf.Clamp(template.fontSize, 12f, 16f);
		}
		else
		{
			try
			{
				textMeshProUGUI.font = TMP_Settings.defaultFontAsset;
			}
			catch (Exception)
			{
			}
			textMeshProUGUI.fontSize = 14f;
		}
		textMeshProUGUI.text = text;
		textMeshProUGUI.color = color;
		textMeshProUGUI.alignment = TextAlignmentOptions.Center;
		textMeshProUGUI.richText = false;
		textMeshProUGUI.raycastTarget = false;
		rectTransform.SetParent((Transform)(object)parent, worldPositionStays: false);
		rectTransform.anchorMin = Vector2.zero;
		rectTransform.anchorMax = Vector2.one;
		rectTransform.offsetMin = Vector2.zero;
		rectTransform.offsetMax = Vector2.zero;
		rectTransform.localScale = Vector3.one;
		val.layer = layer;
	}

	private static TextMeshProUGUI FindTemplate(Transform parent)
	{
		TextMeshProUGUI[] componentsInChildren = ((Component)(object)parent).GetComponentsInChildren<TextMeshProUGUI>(includeInactive: true);
		if (componentsInChildren != null && componentsInChildren.Length != 0)
		{
			return componentsInChildren[0];
		}
		return null;
	}

	private static Color ParseHex(string hex)
	{
		Color color;
		ColorUtility.TryParseHtmlString(hex, out color);
		return color;
	}
}
