using System;
using Comfort.Common;
using EFT.UI;
using Haavk.Core;
using UnityEngine;

namespace Haavk.Combat;

internal static class CombatSpeaker
{
	private static AudioSource _speaker;

	internal static int Played;

	internal static int Missing;

	internal static void Play(CombatSound sound)
	{
		try
		{
			if (!CombatClips.TryGet(sound, out var clip))
			{
				Missing++;
			}
			else if (Singleton<GUISounds>.Instantiated)
			{
				AudioSource audioSource = EnsureSpeaker();
				if (!(audioSource == null))
				{
					audioSource.outputAudioMixerGroup = Singleton<GUISounds>.Instance.GetCommonSoundsMixerGroup();
					audioSource.PlayOneShot(clip, Mathf.Clamp(CombatFeature.Volume, 0f, 2f));
					Played++;
				}
			}
		}
		catch (Exception e)
		{
			CombatFeature.WarnOnce("放战斗音效失败", e);
		}
	}

	internal static void ResetCounters()
	{
		Played = 0;
		Missing = 0;
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
