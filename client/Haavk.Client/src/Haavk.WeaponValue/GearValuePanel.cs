using System;
using System.Collections;
using System.Collections.Generic;
using EFT.InventoryLogic;
using EFT.UI;
using Haavk.Core;
using TMPro;
using UnityEngine;

namespace Haavk.WeaponValue;

/// <summary>
/// 仓库「个人面板」（EquipmentTab）顶部的一行装备总价值 + 常规/机密/绝密档位。
/// 档位阈值照搬三角洲行动的判定价值，安全箱不计入。
/// </summary>
internal static class GearValuePanel
{
	private const string RootName = "HaavkGearValueLabel";

	private const int RefreshFrameInterval = 12;

	private const double ConfidentialThreshold = 150000.0;

	private const double SecretThreshold = 400000.0;

	private const string NormalHex = "#C8C8C8";

	private const string ConfidentialHex = "#FFB347";

	private const string SecretHex = "#FF5252";

	private static readonly Dictionary<nint, Coroutine> Watchers = new Dictionary<nint, Coroutine>();

	internal static void Attach(EquipmentTab tab, InventoryEquipment equipment)
	{
		try
		{
			if ((UnityEngine.Object)(object)tab == null || equipment == null)
			{
				return;
			}
			Transform transform = ((Component)(object)tab).transform;
			if (transform == null)
			{
				return;
			}
			TextMeshProUGUI textMeshProUGUI = EnsureLabel(transform);
			if (textMeshProUGUI == null)
			{
				return;
			}
			textMeshProUGUI.text = Build(equipment);
			StartWatcher(tab, equipment, textMeshProUGUI);
		}
		catch (Exception ex)
		{
			HaavkIl2CppPlugin.Instance.Log.LogError("[Haavk|WpnValue] 仓库个人面板写装备总价值失败: " + ex);
		}
	}

	private static TextMeshProUGUI EnsureLabel(Transform parent)
	{
		Transform transform = parent.Find(RootName);
		if (transform != null)
		{
			TextMeshProUGUI component = ((Component)(object)transform).GetComponent<TextMeshProUGUI>();
			if (component != null)
			{
				((Component)(object)transform).gameObject.SetActive(true);
				transform.SetAsLastSibling();
				return component;
			}
		}
		GameObject val = new GameObject(RootName);
		RectTransform rectTransform = val.AddComponent<RectTransform>();
		TextMeshProUGUI textMeshProUGUI = val.AddComponent<TextMeshProUGUI>();
		TextMeshProUGUI textMeshProUGUI2 = FindTemplate(parent);
		if (textMeshProUGUI2 != null)
		{
			textMeshProUGUI.font = textMeshProUGUI2.font;
			textMeshProUGUI.fontSharedMaterial = textMeshProUGUI2.fontSharedMaterial;
			textMeshProUGUI.fontSize = textMeshProUGUI2.fontSize;
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
		}
		textMeshProUGUI.richText = true;
		textMeshProUGUI.raycastTarget = false;
		textMeshProUGUI.alignment = TextAlignmentOptions.TopLeft;
		rectTransform.SetParent(parent, worldPositionStays: false);
		rectTransform.anchorMin = new Vector2(0f, 1f);
		rectTransform.anchorMax = new Vector2(0f, 1f);
		rectTransform.pivot = new Vector2(0f, 1f);
		rectTransform.anchoredPosition = new Vector2(12f, -8f);
		rectTransform.sizeDelta = new Vector2(640f, 32f);
		rectTransform.localScale = Vector3.one;
		rectTransform.SetAsLastSibling();
		val.layer = ((Component)(object)parent).gameObject.layer;
		return textMeshProUGUI;
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

	private static string Build(InventoryEquipment equipment)
	{
		double num = WeaponTotalCalculator.GearValue(equipment);
		string arg = PriceBook.FormatRoubles(num, WeaponValueFeature.Style);
		string text;
		string text2;
		if (num >= SecretThreshold)
		{
			text = "绝密";
			text2 = SecretHex;
		}
		else if (num >= ConfidentialThreshold)
		{
			text = "机密";
			text2 = ConfidentialHex;
		}
		else
		{
			text = "常规";
			text2 = NormalHex;
		}
		return $"装备总价值 {arg} · <color={text2}>{text}</color>";
	}

	private static void StartWatcher(EquipmentTab tab, InventoryEquipment equipment, TextMeshProUGUI label)
	{
		nint pointer = (tab).Pointer();
		if (Watchers.TryGetValue(pointer, out var value) && value != null)
		{
			HaavkCoroutineHost.Stop(value);
		}
		Coroutine coroutine = HaavkCoroutineHost.Run(RefreshLoop(tab, equipment, label));
		if (coroutine != null)
		{
			Watchers[pointer] = coroutine;
		}
		else
		{
			Watchers.Remove(pointer);
		}
	}

	private static IEnumerator RefreshLoop(EquipmentTab tab, InventoryEquipment equipment, TextMeshProUGUI label)
	{
		nint key = (tab).Pointer();
		string text = label.text;
		while (true)
		{
			for (int i = 0; i < RefreshFrameInterval; i++)
			{
				yield return null;
			}
			if ((UnityEngine.Object)(object)tab == null || label == null || !((Component)(object)tab).gameObject.activeInHierarchy)
			{
				break;
			}
			string text2;
			try
			{
				text2 = Build(equipment);
			}
			catch (Exception ex)
			{
				HaavkIl2CppPlugin.Instance.Log.LogError("[Haavk|WpnValue] 仓库个人面板装备总价值刷新失败: " + ex);
				break;
			}
			if (!(text2 == text))
			{
				text = text2;
				label.text = text2;
			}
		}
		Watchers.Remove(key);
	}
}
