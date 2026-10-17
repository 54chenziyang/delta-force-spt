using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using EFT.InventoryLogic;
using EFT.UI.DragAndDrop;
using Haavk.Core;
using Haavk.Looting.Patches;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.UI;

namespace Haavk.Looting;

internal static class SearchIndicator
{
	private const float FallbackCellSize = 63f;

	private const float MinVisibleMs = 120f;

	private const int AttachRetries = 8;

	private const int AttachEveryFrames = 4;

	private static Texture2D _texture;

	private static GameObject _go;

	private static RectTransform _rt;

	private static RawImage _image;

	private static bool _active;

	private static float _angle;

	private static float _baseX;

	private static float _baseY;

	private static float _orbitRadius;

	private static bool _attached;

	private static int _attachTriesLeft;

	private static int _framesToNextTry;

	private static int _attachTry;

	private static Item _pendingItem;

	private static Grid _pendingGrid;

	private static LocationInGrid _pendingLoc;

	private static float _plannedMs;

	private static bool _lateAttachLogged;

	private static readonly Dictionary<string, GridView> ViewCache = new Dictionary<string, GridView>((IEqualityComparer<string>)StringComparer.Ordinal);

	private static bool _cellLogged;

	private static bool _failWarned;

	private static readonly Color IconTint = new Color(1f, 1f, 1f, 0.92f);

	internal static bool Begin(Item item, float plannedMs)
	{
		Reset();
		try
		{
			if (!LootingFeature.CellIndicator)
			{
				return false;
			}
			if (item == null || plannedMs < 120f)
			{
				return false;
			}
			GridItemAddress gridItemAddress = Il2CppCast.As<GridItemAddress>(item.CurrentAddress);
			if (gridItemAddress == null)
			{
				LootingFeature.Trace("指示器：这一件不在网格地址上，跳过");
				return false;
			}
			Grid grid = gridItemAddress.Grid;
			LocationInGrid locationInGrid = gridItemAddress.LocationInGrid;
			if (grid == null || locationInGrid == null)
			{
				return false;
			}
			_pendingItem = item;
			_pendingGrid = grid;
			_pendingLoc = locationInGrid;
			_plannedMs = plannedMs;
			_attachTriesLeft = 8;
			_framesToNextTry = 0;
			_attachTry = 0;
			TryAttach();
			return true;
		}
		catch (Exception e)
		{
			Warn("指示器建立失败，这一件回落成无指示等待", e);
			Reset();
			return false;
		}
	}

	private static bool TryAttach()
	{
		//IL_028e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0342: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			Item pendingItem = _pendingItem;
			Grid pendingGrid = _pendingGrid;
			LocationInGrid pendingLoc = _pendingLoc;
			if (pendingItem == null || pendingGrid == null || pendingLoc == null)
			{
				return false;
			}
			_attachTry++;
			GridView gridView = FindGridView(pendingGrid);
			if ((UnityEngine.Object)(object)gridView == null)
			{
				if (_attachTriesLeft <= 0)
				{
					LootingFeature.Trace("指示器：试了 " + _attachTry + " 次都没找到在显示的 GridView，不画");
				}
				return false;
			}
			RectTransform rectTransform = Il2CppCast.As<RectTransform>(((Component)(object)gridView).transform);
			if (rectTransform == null)
			{
				return false;
			}
			float num = CellSize(rectTransform, pendingGrid);
			IntVec2 intVec = pendingItem.CalculateRotatedSize(pendingLoc.r);
			int num2 = ((intVec.X <= 0) ? 1 : intVec.X);
			int num3 = ((intVec.Y <= 0) ? 1 : intVec.Y);
			if (!EnsureObject())
			{
				return false;
			}
			_rt.SetParent(rectTransform, worldPositionStays: false);
			_go.layer = ((Component)(object)gridView).gameObject.layer;
			_rt.anchorMin = new Vector2(0f, 1f);
			_rt.anchorMax = new Vector2(0f, 1f);
			_rt.pivot = new Vector2(0.5f, 0.5f);
			_rt.localScale = Vector3.one;
			float num4 = Mathf.Clamp(num * LootingFeature.IndicatorScale, 8f, num * 1.5f);
			_rt.sizeDelta = new Vector2(num4, num4);
			_baseX = ((float)pendingLoc.x + (float)num2 * 0.5f) * num;
			_baseY = (0f - ((float)pendingLoc.y + (float)num3 * 0.5f)) * num;
			_orbitRadius = Mathf.Clamp(num * LootingFeature.IndicatorOrbit, 0f, num * 0.5f);
			_angle = 0f;
			_rt.anchoredPosition = Orbit(0f);
			_rt.localRotation = Quaternion.identity;
			_rt.SetAsLastSibling();
			_image.color = IconTint;
			_go.SetActive(value: true);
			_attached = true;
			_active = true;
			if (_attachTry > 1 && !_lateAttachLogged)
			{
				_lateAttachLogged = true;
				LootingFeature.Trace($"指示器：第 {_attachTry} 次尝试才贴上（面板刚打开，GridView 还没建好）");
			}
			if (LootingFeature.TraceSearchTiming)
			{
				object[] buffer = new object[8];
				buffer[0] = GridKey(pendingGrid);
				buffer[1] = (object)pendingLoc.x;
				buffer[2] = (object)pendingLoc.y;
				buffer[3] = (object)num2;
				buffer[4] = (object)num3;
				buffer[5] = (object)num;
				buffer[6] = (object)_plannedMs;
				buffer[7] = ((_attachTry > 1) ? ("（第 " + _attachTry + " 次才贴上）") : "");
				SearchInstantPatch.Trace(string.Format("指示器 → 网格 {0} 格子({1},{2}) 占 {3}×{4}，边长 {5:F0}px，计划 {6:F0}ms{7}", buffer));
			}
			return true;
		}
		catch (Exception e)
		{
			Warn("指示器贴不上去，这一件回落成无指示等待", e);
			_attachTriesLeft = 0;
			return false;
		}
	}

	private static void Reset()
	{
		_active = false;
		_attached = false;
		_attachTriesLeft = 0;
		_framesToNextTry = 0;
		_attachTry = 0;
		_pendingItem = null;
		_pendingGrid = null;
		_pendingLoc = null;
	}

	internal static float Advance()
	{
		float num;
		try
		{
			num = Time.deltaTime;
		}
		catch (Exception)
		{
			return 16f;
		}
		if (!(num > 0f))
		{
			num = 0f;
		}
		if (num > 0.2f)
		{
			num = 0.2f;
		}
		if (!_attached && _attachTriesLeft > 0)
		{
			if (_framesToNextTry > 0)
			{
				_framesToNextTry--;
			}
			else
			{
				_framesToNextTry = 4;
				_attachTriesLeft--;
				TryAttach();
			}
		}
		if (_active)
		{
			try
			{
				float indicatorSpinSpeed = LootingFeature.IndicatorSpinSpeed;
				if (indicatorSpinSpeed != 0f && _orbitRadius > 0f && _rt != null)
				{
					_angle += indicatorSpinSpeed * num;
					_angle %= 360f;
					_rt.anchoredPosition = Orbit(_angle);
				}
			}
			catch (Exception e)
			{
				Warn("指示器公转失败，本次不再转", e);
				_active = false;
			}
		}
		return num * 1000f;
	}

	internal static void Hide()
	{
		Reset();
		try
		{
			if (_go != null)
			{
				_go.SetActive(value: false);
			}
		}
		catch (Exception e)
		{
			Warn("指示器隐藏失败", e);
		}
	}

	internal static void Clear()
	{
		Hide();
		try
		{
			ViewCache.Clear();
		}
		catch (Exception)
		{
		}
	}

	private static Vector2 Orbit(float degrees)
	{
		float f = degrees * ((float)Math.PI / 180f);
		return new Vector2(_baseX + _orbitRadius * Mathf.Sin(f), _baseY + _orbitRadius * Mathf.Cos(f));
	}

	private static bool EnsureObject()
	{
		if (_go != null && _rt != null && _image != null)
		{
			return true;
		}
		Texture2D texture2D = EnsureTexture();
		if (texture2D == null)
		{
			return false;
		}
		_go = new GameObject("HaavkSearchIndicator");
		_go.SetActive(value: false);
		_rt = _go.AddComponent<RectTransform>();
		_image = _go.AddComponent<RawImage>();
		if (_rt == null || _image == null)
		{
			HaavkIl2CppPlugin.Instance.Log.LogError("[Haavk|Looting] 指示器：RectTransform/RawImage 挂不上，功能不可用");
			UnityEngine.Object.Destroy(_go);
			_go = null;
			_rt = null;
			_image = null;
			return false;
		}
		_image.texture = texture2D;
		_image.raycastTarget = false;
		return true;
	}

	private static Texture2D EnsureTexture()
	{
		if (_texture != null)
		{
			return _texture;
		}
		try
		{
			int num = 96;
			byte[] array = MagnifierGlyph.Render(num);
			byte[] array2 = new byte[array.Length];
			int num2 = num * 4;
			for (int i = 0; i < num; i++)
			{
				Buffer.BlockCopy(array, i * num2, array2, (num - 1 - i) * num2, num2);
			}
			Texture2D texture2D = new Texture2D(num, num, TextureFormat.RGBA32, mipChain: false);
			texture2D.hideFlags = HideFlags.HideAndDontSave;
			texture2D.wrapMode = TextureWrapMode.Clamp;
			texture2D.filterMode = FilterMode.Bilinear;
			texture2D.LoadRawTextureData(array2);
			texture2D.Apply(updateMipmaps: false, makeNoLongerReadable: false);
			_texture = texture2D;
			LootingFeature.Trace("指示器纹理已生成（" + num + "×" + num + " RGBA）");
		}
		catch (Exception e)
		{
			Warn("指示器纹理生成失败，功能整体不可用", e);
			_texture = null;
		}
		return _texture;
	}

	private static GridView FindGridView(Grid grid)
	{
		string text = GridKey(grid);
		if (text == null)
		{
			return null;
		}
		if (ViewCache.TryGetValue(text, out var value))
		{
			if ((UnityEngine.Object)(object)value != null && ((Component)(object)value).gameObject.activeInHierarchy && text.Equals(GridKey(value.Grid), StringComparison.Ordinal))
			{
				return value;
			}
			ViewCache.Remove(text);
		}
		foreach (GridView item in UnityEngine.Object.FindObjectsOfType<GridView>())
		{
			if (!((UnityEngine.Object)(object)item == null) && ((Component)(object)item).gameObject.activeInHierarchy && text.Equals(GridKey(item.Grid), StringComparison.Ordinal))
			{
				ViewCache[text] = item;
				return item;
			}
		}
		return null;
	}

	private static string GridKey(Grid grid)
	{
		try
		{
			if (grid == null)
			{
				return null;
			}
			CompoundItem parentItem = grid.ParentItem;
			if (parentItem == null)
			{
				return null;
			}
			string id = parentItem.Id;
			if (string.IsNullOrEmpty(id))
			{
				return null;
			}
			return id + "|" + (grid.ID ?? string.Empty);
		}
		catch (Exception)
		{
			return null;
		}
	}

	private static float CellSize(RectTransform gridRt, Grid grid)
	{
		float num = 63f;
		float num2 = -1f;
		try
		{
			int gridWidth = grid.GridWidth;
			float width = gridRt.rect.width;
			if (gridWidth > 0 && width > 1f)
			{
				num2 = (width - 1f) / (float)gridWidth;
				if (num2 >= 16f && num2 <= 256f)
				{
					num = num2;
				}
			}
		}
		catch (Exception)
		{
		}
		if (!_cellLogged)
		{
			_cellLogged = true;
			LootingFeature.Trace($"指示器：格子边长 反推 {num2:F2}px，采用 {num:F2}px（出厂 {63f:F0}）");
		}
		return num;
	}

	private static void Warn(string what, Exception e)
	{
		string data = "[Haavk|Looting] " + what + ": " + e;
		if (_failWarned)
		{
			HaavkIl2CppPlugin.Instance.Log.LogDebug(data);
			return;
		}
		_failWarned = true;
		HaavkIl2CppPlugin.Instance.Log.LogError(data);
	}
}
