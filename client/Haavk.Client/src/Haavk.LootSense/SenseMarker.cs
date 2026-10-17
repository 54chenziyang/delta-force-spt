using System;
using Haavk.Core;
using TMPro;
using UnityEngine;

namespace Haavk.LootSense;

internal sealed class SenseMarker
{
	private const float SizeClamp = 3f;

	private const float BaseSize = 0.5f;

	private const float IconScale = 0.1f;

	private const float TextGap = 0.03f;

	private const float RefreshSeconds = 1f;

	private const float RefreshRadius = 6f;

	private const float AliveEvery = 0.2f;

	private const float ScaleEpsilon = 0.02f;

	private const float LayerStep = 0.4f;

	internal readonly int Id;

	internal readonly SenseTarget Target;

	internal readonly SenseTimeline Timeline;

	private SenseFacts _facts;

	private float _nextRefresh;

	private Vector3 _anchor;

	private bool _anchorDirty = true;

	private bool _alive = true;

	private float _nextAliveCheck;

	internal bool LightAllowed = true;

	private float _appliedAlpha = -1f;

	private float _appliedIntensity = -1f;

	private float _appliedRange = -1f;

	private float _appliedScale = -1f;

	private float _appliedSize = -1f;

	private bool _lightOn;

	private bool _iconOn;

	private bool _textOn;

	private bool _hasSprite;

	private float _appliedLayerOffset = -1f;

	private readonly GameObject _root;

	private readonly Transform _rootTransform;

	private readonly Transform _visual;

	private readonly Light _light;

	private readonly SpriteRenderer _icon;

	private readonly TextMeshPro _text;

	internal int Layer { get; private set; }

	internal SenseFacts Facts => _facts;

	internal Vector3 AnchorPosition => _anchor;

	private SenseMarker(int id, SenseTarget target, SenseFacts facts, SenseTimeline timeline, GameObject root, Transform visual, Light light, SpriteRenderer icon, TextMeshPro text)
	{
		Id = id;
		Target = target;
		_facts = facts;
		Timeline = timeline;
		_root = root;
		_rootTransform = root.transform;
		_visual = visual;
		_light = light;
		_icon = icon;
		_text = text;
	}

	internal void SetAnchor(Vector3 anchor)
	{
		if (!SenseMath.Same(anchor, _anchor))
		{
			_anchor = anchor;
			_anchorDirty = true;
		}
	}

	internal static SenseMarker Create(int id, SenseTarget target, SenseFacts facts, SenseTimeline timeline, float now, Vector3 anchor, int layer)
	{
		GameObject gameObject = null;
		try
		{
			gameObject = new GameObject("HaavkSense");
			gameObject.transform.position = anchor;
			GameObject gameObject2 = new GameObject("Visual");
			gameObject2.transform.SetParent(gameObject.transform, worldPositionStays: false);
			GameObject gameObject3 = new GameObject("Icon");
			gameObject3.transform.SetParent(gameObject2.transform, worldPositionStays: false);
			gameObject3.transform.localScale = SenseMath.Uniform(0.1f);
			SpriteRenderer spriteRenderer = gameObject3.AddComponent<SpriteRenderer>();
			spriteRenderer.enabled = false;
			Light light = gameObject.AddComponent<Light>();
			light.type = LightType.Point;
			light.shadows = LightShadows.None;
			light.intensity = 0f;
			light.enabled = false;
			GameObject gameObject4 = new GameObject("Text");
			gameObject4.transform.SetParent(gameObject2.transform, worldPositionStays: false);
			TextMeshPro textMeshPro = gameObject4.AddComponent<TextMeshPro>();
			SenseFont.Apply(textMeshPro);
			textMeshPro.richText = true;
			textMeshPro.enableWordWrapping = false;
			textMeshPro.overflowMode = TextOverflowModes.Overflow;
			textMeshPro.alignment = TextAlignmentOptions.MidlineLeft;
			textMeshPro.fontSize = 1f;
			textMeshPro.alpha = 0f;
			textMeshPro.enabled = false;
			RectTransform rectTransform = textMeshPro.rectTransform;
			rectTransform.pivot = new Vector2(0f, 0.5f);
			rectTransform.sizeDelta = new Vector2(4f, 1f);
			rectTransform.localPosition = SenseMath.Vec(0.11000001f, 0f, 0f);
			SenseMarker senseMarker = new SenseMarker(id, target, facts, timeline, gameObject, gameObject2.transform, light, spriteRenderer, textMeshPro);
			senseMarker.WriteFacts();
			senseMarker._nextRefresh = now + 1f;
			senseMarker._anchor = anchor;
			senseMarker._anchorDirty = false;
			senseMarker._nextAliveCheck = now + 0.2f;
			senseMarker.Layer = ((layer >= 0) ? layer : 0);
			return senseMarker;
		}
		catch (Exception e)
		{
			LootSenseFeature.WarnOnce("建标记失败", e);
			if (gameObject != null)
			{
				UnityEngine.Object.Destroy(gameObject);
			}
			return null;
		}
	}

	internal bool IsAlive(float now)
	{
		if (now >= _nextAliveCheck)
		{
			_nextAliveCheck = now + 0.2f;
			_alive = _root != null && Target.Alive;
		}
		return _alive;
	}

	internal bool DueForRefresh(float now, Vector3 feet)
	{
		if (!Target.Refreshable || Timeline.Intensity <= 0f || now < _nextRefresh)
		{
			return false;
		}
		return SenseMath.SqrDist(_anchor, feet) <= 36f;
	}

	internal void Refresh(SenseFacts facts, float now)
	{
		_nextRefresh = now + 1f;
		if (facts != null)
		{
			bool flag = !string.Equals(facts.IconPath, _facts.IconPath, StringComparison.Ordinal);
			bool flag2 = !string.Equals(facts.Text, _facts.Text, StringComparison.Ordinal);
			bool flag3 = !SenseMath.Same(facts.Colour, _facts.Colour);
			_facts = facts;
			if (flag)
			{
				_icon.sprite = null;
				_hasSprite = false;
				_iconOn = false;
				_icon.enabled = false;
			}
			if (flag2 | flag3)
			{
				WriteFacts();
			}
		}
	}

	private void WriteFacts()
	{
		Color colour = _facts.Colour;
		_light.color = SenseMath.WithAlpha(colour, 1f);
		_text.text = _facts.Text ?? string.Empty;
		_appliedAlpha = -1f;
	}

	internal void Apply(bool hasCamera, Vector3 cameraPosition, Quaternion cameraRotation, bool nightVision)
	{
		float intensity = Timeline.Intensity;
		if (intensity <= 0f && _appliedAlpha == 0f)
		{
			return;
		}
		if (_anchorDirty)
		{
			_anchorDirty = false;
			_rootTransform.position = _anchor;
		}
		if (hasCamera)
		{
			_rootTransform.rotation = cameraRotation;
			float num = SenseRule.BillboardScale(SenseMath.Dist(cameraPosition, _anchor), 3f);
			if (Math.Abs(num - _appliedScale) > 0.02f)
			{
				_appliedScale = num;
				_rootTransform.localScale = SenseMath.Uniform(num);
			}
		}
		float num2 = 0.5f * LootSenseFeature.MarkerSize.Value;
		if (num2 != _appliedSize)
		{
			_appliedSize = num2;
			_visual.localScale = SenseMath.Uniform(num2);
		}
		float num3 = (float)Layer * 0.4f * num2;
		if (num3 != _appliedLayerOffset)
		{
			_appliedLayerOffset = num3;
			_visual.localPosition = SenseMath.Vec(0f, num3, 0f);
		}
		bool flag = intensity > 0f;
		Color colour = _facts.Colour;
		bool flag2 = flag && LightAllowed && LootSenseFeature.ShowLight.Value && !nightVision;
		if (flag2 != _lightOn)
		{
			_lightOn = flag2;
			_light.enabled = flag2;
		}
		if (flag2)
		{
			float num4 = LootSenseFeature.LightIntensity.Value * intensity;
			if (num4 != _appliedIntensity)
			{
				_appliedIntensity = num4;
				_light.intensity = num4;
			}
			float value = LootSenseFeature.LightRange.Value;
			if (value != _appliedRange)
			{
				_appliedRange = value;
				_light.range = value;
			}
		}
		int num5;
		if (flag)
		{
			num5 = (LootSenseFeature.ShowIcon.Value ? 1 : 0);
			if (num5 != 0 && !_hasSprite)
			{
				Sprite sprite = SenseIcons.Get(_facts.IconPath);
				if (sprite != null)
				{
					_icon.sprite = sprite;
					_hasSprite = true;
				}
			}
		}
		else
		{
			num5 = 0;
		}
		bool flag3 = num5 != 0 && _hasSprite;
		if (flag3 != _iconOn)
		{
			_iconOn = flag3;
			_icon.enabled = flag3;
		}
		bool flag4 = flag && LootSenseFeature.ShowText.Value;
		if (flag4 != _textOn)
		{
			_textOn = flag4;
			_text.enabled = flag4;
		}
		if (intensity != _appliedAlpha)
		{
			_appliedAlpha = intensity;
			_icon.color = SenseMath.WithAlpha(colour, intensity);
			_text.alpha = intensity;
		}
	}

	internal void Destroy()
	{
		try
		{
			if (_root != null)
			{
				UnityEngine.Object.Destroy(_root);
			}
		}
		catch (Exception)
		{
		}
	}
}
