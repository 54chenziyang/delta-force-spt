using System;
using System.Reflection;
using EFT.UI;
using EFT.UI.Ragfair;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Haavk.WeaponValue.Patches;

/// <summary>
/// 跳蚤市场左侧分类面板：在搜索框正下方挂一排六档品质标签按钮。
/// 点某个品质标签时，把 FilterRule.HandbookId 设成服务端约定的哨兵值（见 Sentinel）并触发一次搜索；
/// 服务端（RagfairTierFilterPatch）会把报价列表替换成「该品质档的全部物品」，实现「点哪档只出哪档」。
/// 再点同一个标签则清空筛选，恢复「全部」。
/// 挂点选面板根节点并按搜索框实际世界坐标定位，保证既贴着搜索框、又盖在物品列表之上。
/// </summary>
internal static class FleaQualityFilterPatch
{
	private const string BarName = "HaavkFleaQualityBar";

	private const float BarHeight = 22f;

	private const float BarGap = 2f;

	private const float IdleAlpha = 0.35f;

	private const float ActiveAlpha = 1f;

	// 当前激活的品质档（-1 表示未筛选）。与按钮高亮、再点取消同步。
	private static int _activeTier = -1;

	// 顺序与 Haavk.Server 的 QualityTierWords 一致：劣质 普通 良好 优秀 精良 极品
	private static readonly string[] TierWords = { "劣质", "普通", "良好", "优秀", "精良", "极品" };

	// 填充色与服务端 config.json 的六档 hex 保持一致
	private static readonly string[] TierFillHex = { "#d8d8d8", "#58c93a", "#2f9ff0", "#b054e8", "#f5a824", "#ee4444" };

	// 按钮上的字色，按底色明暗挑，保证可读
	private static readonly string[] TierTextHex = { "#202020", "#0a1a05", "#04121f", "#ffffff", "#241500", "#ffffff" };

	internal static void Enable(Harmony harmony)
	{
		try
		{
			MethodInfo methodInfo = AccessTools.DeclaredMethod(typeof(RagfairCategoriesPanel), "Awake", (Type[])null, (Type[])null);
			if (methodInfo == (MethodInfo)null)
			{
				HaavkIl2CppPlugin.Instance.Log.LogError("[Haavk|WpnValue] 找不到 RagfairCategoriesPanel.Awake，跳蚤品质标签不启用");
				return;
			}
			harmony.Patch((MethodBase)(object)methodInfo, (HarmonyMethod)null, new HarmonyMethod(typeof(FleaQualityFilterPatch).GetMethod("Postfix", (BindingFlags)40)), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
			HaavkIl2CppPlugin.Instance.Log.LogInfo("[Haavk|WpnValue] 跳蚤品质标签已启用");
		}
		catch (Exception ex)
		{
			HaavkIl2CppPlugin.Instance.Log.LogError("[Haavk|WpnValue] 跳蚤品质标签挂载失败: " + ex);
		}
	}

	private static void Postfix(RagfairCategoriesPanel __instance)
	{
		try
		{
			Attach(__instance);
		}
		catch (Exception ex)
		{
			HaavkIl2CppPlugin.Instance.Log.LogError("[Haavk|WpnValue] 跳蚤品质标签创建失败: " + ex);
		}
	}

	private static void Attach(RagfairCategoriesPanel panel)
	{
		if ((UnityEngine.Object)(object)panel == null)
		{
			HaavkIl2CppPlugin.Instance.Log.LogWarning("[Haavk|WpnValue] 品质标签：面板为空，跳过");
			return;
		}
		TMP_InputField searchInputField = panel.SearchInputField;
		if ((UnityEngine.Object)(object)searchInputField == null)
		{
			HaavkIl2CppPlugin.Instance.Log.LogWarning("[Haavk|WpnValue] 品质标签：SearchInputField 为空，跳过");
			return;
		}
		RectTransform searchRect = ((Component)(object)searchInputField).GetComponent<RectTransform>();
		RectTransform rootRect = ((Component)(object)panel).GetComponent<RectTransform>();
		if (searchRect == null || rootRect == null)
		{
			HaavkIl2CppPlugin.Instance.Log.LogWarning("[Haavk|WpnValue] 品质标签：RectTransform 缺失，跳过");
			return;
		}
		if ((UnityEngine.Object)(object)rootRect.Find(BarName) != null)
		{
			return;
		}
		// 新面板（重新打开跳蚤）时重置当前档，避免高亮/再点取消与现实不一致。
		_activeTier = -1;
		int layer = ((Component)(object)rootRect).gameObject.layer;
		GameObject val = new GameObject(BarName);
		RectTransform bar = val.AddComponent<RectTransform>();
		Image image = val.AddComponent<Image>();
		image.color = new Color(0f, 0f, 0f, 0.6f);
		image.raycastTarget = false;
		LayoutElement layoutElement = val.AddComponent<LayoutElement>();
		layoutElement.ignoreLayout = true;
		bar.SetParent((Transform)(object)rootRect, worldPositionStays: false);
		bar.anchorMin = new Vector2(0f, 1f);
		bar.anchorMax = new Vector2(0f, 1f);
		bar.pivot = new Vector2(0f, 1f);
		bar.localScale = Vector3.one;
		LayoutRebuilder.ForceRebuildLayoutImmediate(rootRect);
		Vector3[] array = new Vector3[4];
		searchRect.GetWorldCorners(array);
		Vector3 vector = rootRect.InverseTransformPoint(array[0]);
		Vector3 vector2 = rootRect.InverseTransformPoint(array[2]);
		float num = Mathf.Abs(vector2.x - vector.x);
		if (num < 40f)
		{
			num = 300f;
		}
		Vector2 vector3 = new Vector2((0f - rootRect.pivot.x) * rootRect.rect.width, (1f - rootRect.pivot.y) * rootRect.rect.height);
		bar.sizeDelta = new Vector2(num, BarHeight);
		bar.anchoredPosition = new Vector2(vector.x - vector3.x, vector.y - vector3.y - BarGap);
		bar.SetAsLastSibling();
		val.layer = layer;
		Transform searchParent = ((Transform)(object)searchRect).parent;
		HaavkIl2CppPlugin.Instance.Log.LogInfo("[Haavk|WpnValue] 品质标签条已挂到 " + ((UnityEngine.Object)(object)rootRect).name + " rootSize=" + rootRect.rect.width + "x" + rootRect.rect.height + " rootPivot=" + rootRect.pivot + " 搜索框=" + ((UnityEngine.Object)(object)searchRect).name + " searchSize=" + searchRect.rect.width + "x" + searchRect.rect.height + " searchParent=" + ((searchParent != null) ? ((UnityEngine.Object)(object)searchParent).name : "null") + " barSize=" + bar.sizeDelta + " barAnchor=" + bar.anchoredPosition);
		TextMeshProUGUI textMeshProUGUI = FindTemplate(rootRect);
		Image[] array2 = new Image[TierWords.Length];
		for (int i = 0; i < TierWords.Length; i++)
		{
			int num2 = i;
			GameObject val2 = new GameObject("HaavkFleaQuality" + num2);
			RectTransform rectTransform = val2.AddComponent<RectTransform>();
			Image image2 = val2.AddComponent<Image>();
			Color color = ParseHex(TierFillHex[num2]);
			color.a = IdleAlpha;
			image2.color = color;
			image2.raycastTarget = true;
			rectTransform.SetParent((Transform)(object)bar, worldPositionStays: false);
			rectTransform.anchorMin = new Vector2((float)num2 / (float)TierWords.Length, 0f);
			rectTransform.anchorMax = new Vector2((float)(num2 + 1) / (float)TierWords.Length, 1f);
			rectTransform.offsetMin = new Vector2(2f, 1f);
			rectTransform.offsetMax = new Vector2(-2f, -1f);
			rectTransform.localScale = Vector3.one;
			val2.layer = layer;
			array2[num2] = image2;
			GameObject val3 = new GameObject("Label");
			RectTransform rectTransform2 = val3.AddComponent<RectTransform>();
			TextMeshProUGUI textMeshProUGUI2 = val3.AddComponent<TextMeshProUGUI>();
			if (textMeshProUGUI != null)
			{
				textMeshProUGUI2.font = textMeshProUGUI.font;
				textMeshProUGUI2.fontSharedMaterial = textMeshProUGUI.fontSharedMaterial;
				textMeshProUGUI2.fontSize = Mathf.Clamp(textMeshProUGUI.fontSize, 11f, 15f);
			}
			else
			{
				try
				{
					textMeshProUGUI2.font = TMP_Settings.defaultFontAsset;
				}
				catch (Exception)
				{
				}
				textMeshProUGUI2.fontSize = 13f;
			}
			textMeshProUGUI2.text = TierWords[num2];
			textMeshProUGUI2.color = ParseHex(TierTextHex[num2]);
			textMeshProUGUI2.alignment = TextAlignmentOptions.Center;
			textMeshProUGUI2.richText = false;
			textMeshProUGUI2.raycastTarget = false;
			rectTransform2.SetParent((Transform)(object)rectTransform, worldPositionStays: false);
			rectTransform2.anchorMin = Vector2.zero;
			rectTransform2.anchorMax = Vector2.one;
			rectTransform2.offsetMin = Vector2.zero;
			rectTransform2.offsetMax = Vector2.zero;
			rectTransform2.localScale = Vector3.one;
			val3.layer = layer;
			Button button = val2.AddComponent<Button>();
			button.targetGraphic = (Graphic)(object)image2;
			UnityAction call = () => Toggle(panel, searchInputField, array2, num2);
			button.onClick.AddListener(call);
		}
	}

	private static void Toggle(RagfairCategoriesPanel panel, TMP_InputField field, Image[] images, int index)
	{
		try
		{
			if ((UnityEngine.Object)(object)panel == null)
			{
				return;
			}
			RagFair ragfair = panel.Ragfair;
			if ((UnityEngine.Object)(object)ragfair == null)
			{
				HaavkIl2CppPlugin.Instance.Log.LogWarning("[Haavk|WpnValue] 品质标签：拿不到 RagFair，跳过");
				return;
			}
			bool flag = _activeTier == index;
			_activeTier = (flag ? (-1) : index);
			FilterRule filterRule = ragfair.FilterRule;
			filterRule.Page = 0;
			filterRule.HandbookId = ((_activeTier < 0) ? string.Empty : Sentinel(_activeTier));
			ragfair.SetFilterRule(filterRule, clear: true, updateCategories: true, RagFair.ESetFilterSource.IdSelected);
			for (int i = 0; i < images.Length; i++)
			{
				if (!((UnityEngine.Object)(object)images[i] == null))
				{
					Color color = ParseHex(TierFillHex[i]);
					color.a = ((i == _activeTier) ? ActiveAlpha : IdleAlpha);
					images[i].color = color;
				}
			}
			HaavkIl2CppPlugin.Instance.Log.LogInfo("[Haavk|WpnValue] 品质标签：" + ((_activeTier < 0) ? "已清空（全部）" : ("只看【" + TierWords[_activeTier] + "】")));
		}
		catch (Exception ex)
		{
			HaavkIl2CppPlugin.Instance.Log.LogError("[Haavk|WpnValue] 跳蚤品质筛选失败: " + ex);
		}
	}

	/// <summary>
	/// 与服务端 RagfairTierFilterPatch.Sentinel 保持一致：'a' + 档位数字 + 22 个 '0'。
	/// </summary>
	private static string Sentinel(int tier)
	{
		return "a" + (char)(48 + tier) + new string('0', 22);
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
