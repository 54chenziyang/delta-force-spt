using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using BepInEx.Core.Logging.Interpolation;
using BepInEx.Logging;
using Comfort.Common;
using EFT.InventoryLogic;
using EFT.UI;
using Haavk.Core;
using Haavk.WeaponValue;
using Il2CppInterop.Runtime.InteropTypes;
using UnityEngine;

namespace Haavk.Looting;

internal static class ChimeSound
{
	private struct Pending
	{
		internal AudioClip Clip;

		internal int Tier;

		internal string When;

		internal string Label;
	}

	internal static int PrimaryHits;

	private static bool _anyContainerFinished;

	private static bool _itemSeenThisContainer;

	private static bool _fallbackAnnounced;

	private static AudioSource _speaker;

	private static string _lastFallbackItemId;

	private const float GapSeconds = 0.25f;

	private const int MaxQueued = 8;

	private static readonly Queue<Pending> Queue = new Queue<Pending>();

	private static Coroutine _pump;

	private static bool _dropWarned;

	internal static void OnDiscovered(Item item)
	{
		PrimaryHits++;
		TryPlayFor(item, "揭示时");
	}

	internal static void Fallback(Item item, int tier)
	{
		if (PrimaryHits > 0 || item == null)
		{
			return;
		}
		_itemSeenThisContainer = true;
		if (_anyContainerFinished && !(item.Id == _lastFallbackItemId))
		{
			_lastFallbackItemId = item.Id;
			if (!_fallbackAnnounced)
			{
				_fallbackAnnounced = true;
				HaavkIl2CppPlugin.Instance.Log.LogWarning("[Haavk|Looting] 稀有音效降级到挂点②（TryFindUnknownItem） —— ①被内联了。**音效会比物品出现早一拍**（0.9 或 1.9 秒）");
			}
			PlayIfRare(tier, "预看时");
		}
	}

	internal static void ContainerFinished()
	{
		if (_itemSeenThisContainer)
		{
			_anyContainerFinished = true;
		}
		_itemSeenThisContainer = false;
		_lastFallbackItemId = null;
	}

	private static void TryPlayFor(Item item, string when)
	{
		if (!WeaponValueFeature.TryTier(item, out var tier))
		{
			if (ChimeRule.UnknownTierChimes)
			{
				PlayIfRare(0, when);
			}
		}
		else
		{
			PlayIfRare(tier, when);
		}
	}

	private static void PlayIfRare(int tier, string when)
	{
		//IL_007a: Unknown result type (might be due to invalid IL or missing references)
		//IL_007f: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			if (!LootingFeature.RareItemChime || !ChimeRule.ShouldChime(tier, LootingFeature.ChimeMinTier))
			{
				return;
			}
			EUISoundType soundType = (ChimeRule.IsTopTier(tier, 6) ? EUISoundType.AchievementCompleted : EUISoundType.QuestSubTrackComplete);
			if (!Singleton<GUISounds>.Instantiated)
			{
				return;
			}
			GUISounds instance = Singleton<GUISounds>.Instance;
			bool flag = ChimeClips.TryGet(tier, out var clip);
			if (!flag)
			{
				UISoundsWrapper uISounds = PrivAccess.GuiSoundsUISounds(instance);
				if (uISounds != null)
				{
					clip = uISounds.GetUIClip(soundType);
				}
			}
			if (clip == null)
			{
				instance.PlayUISound(soundType);
				DefaultInterpolatedStringHandler val = new DefaultInterpolatedStringHandler(33, 2);
				val.AppendLiteral("稀有音效（");
				val.AppendFormatted(when);
				val.AppendLiteral("）档位 ");
				val.AppendFormatted<int>(tier);
				val.AppendLiteral("：clip 取不到，退回 PlayUISound");
				LootingFeature.Trace(val.ToStringAndClear());
			}
			else
			{
				string label;
				if (flag)
				{
					label = "自定义音";
				}
				else
				{
					label = (ChimeRule.IsTopTier(tier, 6) ? "内置顶档音" : "内置普通档音");
				}
				Enqueue(clip, tier, when, label);
			}
		}
		catch (Exception t)
		{
			ManualLogSource log = HaavkIl2CppPlugin.Instance.Log;
			BepInExErrorLogInterpolatedStringHandler bepInExErrorLogInterpolatedStringHandler = new BepInExErrorLogInterpolatedStringHandler(11, 2, out var isEnabled);
			if (isEnabled)
			{
				bepInExErrorLogInterpolatedStringHandler.AppendFormatted("[Haavk|Looting]");
				bepInExErrorLogInterpolatedStringHandler.AppendLiteral(" 播放稀有音效失败: ");
				bepInExErrorLogInterpolatedStringHandler.AppendFormatted(t);
			}
			log.LogError(bepInExErrorLogInterpolatedStringHandler);
		}
	}

	private static void Enqueue(AudioClip clip, int tier, string when, string label)
	{
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		if (Queue.Count >= 8)
		{
			if (!_dropWarned)
			{
				_dropWarned = true;
				DefaultInterpolatedStringHandler val = new DefaultInterpolatedStringHandler(19, 1);
				val.AppendLiteral("稀有音效队列满（");
				val.AppendFormatted<int>(8);
				val.AppendLiteral("），后面的这一批不响了");
				LootingFeature.Trace(val.ToStringAndClear() + " —— 一次揭示太多件时这是正常的，不是故障");
			}
		}
		else
		{
			Queue.Enqueue(new Pending
			{
				Clip = clip,
				Tier = tier,
				When = when,
				Label = label
			});
			if (_pump == null)
			{
				_pump = HaavkCoroutineHost.Run(Pump());
			}
		}
	}

	private static IEnumerator Pump()
	{
		while (Queue.Count > 0)
		{
			PlayNow(Queue.Dequeue());
			yield return new WaitForSeconds(0.25f);
		}
		_pump = null;
	}

	private static void PlayNow(Pending p)
	{
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			if (!(p.Clip == null) && Singleton<GUISounds>.Instantiated)
			{
				AudioSource audioSource = EnsureSpeaker();
				if (!(audioSource == null))
				{
					audioSource.outputAudioMixerGroup = Singleton<GUISounds>.Instance.GetCommonSoundsMixerGroup();
					audioSource.PlayOneShot(p.Clip, Mathf.Clamp01(LootingFeature.ChimeVolume));
					DefaultInterpolatedStringHandler val = new DefaultInterpolatedStringHandler(22, 5);
					val.AppendLiteral("稀有音效（");
					val.AppendFormatted(p.When);
					val.AppendLiteral("）档位 ");
					val.AppendFormatted<int>(p.Tier);
					val.AppendLiteral(" → ");
					val.AppendFormatted(p.Label);
					val.AppendLiteral("，音量 ");
					val.AppendFormatted<float>(LootingFeature.ChimeVolume, "F2");
					val.AppendLiteral("，队列还剩 ");
					val.AppendFormatted<int>(Queue.Count);
					LootingFeature.Trace(val.ToStringAndClear());
				}
			}
		}
		catch (Exception t)
		{
			ManualLogSource log = HaavkIl2CppPlugin.Instance.Log;
			BepInExErrorLogInterpolatedStringHandler bepInExErrorLogInterpolatedStringHandler = new BepInExErrorLogInterpolatedStringHandler(11, 2, out var isEnabled);
			if (isEnabled)
			{
				bepInExErrorLogInterpolatedStringHandler.AppendFormatted("[Haavk|Looting]");
				bepInExErrorLogInterpolatedStringHandler.AppendLiteral(" 播放稀有音效失败: ");
				bepInExErrorLogInterpolatedStringHandler.AppendFormatted(t);
			}
			log.LogError(bepInExErrorLogInterpolatedStringHandler);
		}
	}

	private static AudioSource EnsureSpeaker()
	{
		if (_speaker != null)
		{
			return _speaker;
		}
		GameObject hostObject = HaavkCoroutineHost.HostObject;
		if (hostObject == null)
		{
			return null;
		}
		_speaker = hostObject.AddComponent<AudioSource>();
		_speaker.playOnAwake = false;
		_speaker.loop = false;
		_speaker.spatialBlend = 0f;
		return _speaker;
	}
}
