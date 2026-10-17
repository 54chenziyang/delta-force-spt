using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Haavk.Core;
using Il2CppInterop.Runtime.InteropTypes;
using UnityEngine;
using UnityEngine.Networking;

namespace Haavk.Combat;

internal static class CombatClips
{
	private struct Format
	{
		internal readonly string Ext;

		internal readonly AudioType Type;

		internal Format(string ext, AudioType type)
		{
			Ext = ext;
			Type = type;
		}
	}

	private static readonly AudioClip[] Clips = new AudioClip[7];

	private static bool _started;

	private static bool _loading;

	private static int _loggedTotal = -1;

	private const int DecodeGuardFrames = 600;

	private static readonly Format[] Formats = new Format[3]
	{
		new Format(".ogg", AudioType.OGGVORBIS),
		new Format(".wav", AudioType.WAV),
		new Format(".mp3", AudioType.MPEG)
	};

	internal static string FirstPassSummary { get; private set; }

	internal static void Start()
	{
		if (!_started)
		{
			_started = true;
			HaavkCoroutineHost.Run(WatchLoop());
		}
	}

	internal static bool TryGet(CombatSound sound, out AudioClip clip)
	{
		clip = null;
		if (sound <= CombatSound.None || (int)sound >= Clips.Length)
		{
			return false;
		}
		AudioClip audioClip = Clips[(int)sound];
		if (audioClip == null || SafeLength(audioClip) <= 0f)
		{
			if (audioClip != null && !_loading)
			{
				HaavkCoroutineHost.Run(LoadPass());
			}
			return false;
		}
		clip = audioClip;
		return true;
	}

	private static IEnumerator WatchLoop()
	{
		yield return null;
		while (true)
		{
			if (!_loading && AnyMissing())
			{
				IEnumerator pass = LoadPass();
				while (pass.MoveNext())
				{
					yield return pass.Current;
				}
			}
			yield return new WaitForSeconds(10f);
		}
	}

	private static bool AnyMissing()
	{
		for (int i = 1; i < Clips.Length; i++)
		{
			AudioClip audioClip = Clips[i];
			if (audioClip == null || SafeLength(audioClip) <= 0f)
			{
				return true;
			}
		}
		return false;
	}

	private static IEnumerator LoadPass()
	{
		_loading = true;
		string dir = SoundsDir();
		StringBuilder summary = new StringBuilder();
		int total = 0;
		int reloaded = 0;
		for (int s = 0; s < CombatSoundFiles.Slots.Length; s++)
		{
			CombatSound combatSound = CombatSoundFiles.Slots[s];
			int i = (int)combatSound;
			string stem = CombatSoundFiles.Stem(combatSound);
			AudioClip audioClip = Clips[i];
			if (audioClip != null && SafeLength(audioClip) > 0f)
			{
				total++;
				continue;
			}
			string text = null;
			AudioType type = AudioType.UNKNOWN;
			for (int j = 0; j < Formats.Length; j++)
			{
				string text2 = Path.Combine(dir, stem + Formats[j].Ext);
				if (SafeExists(text2))
				{
					text = text2;
					type = Formats[j].Type;
					break;
				}
			}
			if (text == null)
			{
				summary.Append(' ').Append(stem).Append("=缺");
				continue;
			}
			AudioClip loaded = null;
			IEnumerator load = Load(text, type, (AudioClip c) =>
			{
				loaded = c;
			});
			while (load.MoveNext())
			{
				yield return load.Current;
			}
			if (loaded == null)
			{
				summary.Append(' ').Append(stem).Append("=解码失败");
				continue;
			}
			Clips[i] = loaded;
			total++;
			reloaded++;
		}
		_loading = false;
		string text3 = total + "/" + CombatSoundFiles.Slots.Length + ((summary.Length > 0) ? ("（" + summary.ToString().Trim() + "）") : "");
		if (_loggedTotal < 0)
		{
			_loggedTotal = total;
			FirstPassSummary = text3 + "，目录 " + dir;
		}
		else if (total != _loggedTotal)
		{
			_loggedTotal = total;
			HaavkIl2CppPlugin.Instance.Log.LogInfo("[Haavk|Combat] 战斗音效：" + text3);
		}
		else if (reloaded > 0)
		{
			HaavkIl2CppPlugin.Instance.Log.LogDebug("[Haavk|Combat] 战斗音效被引擎卸载后已重载 " + reloaded + " 个");
		}
	}

	private static IEnumerator Load(string path, AudioType type, Action<AudioClip> onDone)
	{
		UnityWebRequest req = Begin(path, type);
		if (req == null)
		{
			yield break;
		}
		if (!Send(req, path))
		{
			SafeDispose(req);
			yield break;
		}
		while (!RequestDone(req))
		{
			yield return null;
		}
		AudioClip clip = Fetch(req, path);
		if (clip == null)
		{
			SafeDispose(req);
			yield break;
		}
		int guard = 0;
		while (StillDecoding(clip) && guard < 600)
		{
			guard++;
			yield return null;
		}
		Finish(clip, path, onDone);
		SafeDispose(req);
	}

	private static UnityWebRequest Begin(string path, AudioType type)
	{
		try
		{
			return UnityWebRequestMultimedia.GetAudioClip(new Uri(path).AbsoluteUri, type);
		}
		catch (Exception ex)
		{
			Warn("建请求失败 " + path + ": " + ex.Message);
			return null;
		}
	}

	private static bool Send(UnityWebRequest req, string path)
	{
		try
		{
			req.SendWebRequest();
			return true;
		}
		catch (Exception ex)
		{
			Warn("发请求失败 " + path + ": " + ex.Message);
			return false;
		}
	}

	private static bool RequestDone(UnityWebRequest req)
	{
		try
		{
			return req.isDone;
		}
		catch (Exception)
		{
			return true;
		}
	}

	private static AudioClip Fetch(UnityWebRequest req, string path)
	{
		try
		{
			if (req.result != UnityWebRequest.Result.Success)
			{
				Warn("读不到音效 " + path + ": " + req.error);
				return null;
			}
			AudioClip content = DownloadHandlerAudioClip.GetContent(req);
			if (content == null)
			{
				Warn("音效 " + path + " 解出空 clip");
				return null;
			}
			content.LoadAudioData();
			return content;
		}
		catch (Exception ex)
		{
			Warn("取音效内容失败 " + path + ": " + ex.Message);
			return null;
		}
	}

	private static bool StillDecoding(AudioClip clip)
	{
		try
		{
			return clip.loadState == AudioDataLoadState.Loading;
		}
		catch (Exception)
		{
			return false;
		}
	}

	private static void Finish(AudioClip clip, string path, Action<AudioClip> onDone)
	{
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		try
		{
			AudioDataLoadState loadState = clip.loadState;
			if (loadState == AudioDataLoadState.Loaded && clip.length <= 0f)
			{
				Warn("音效 " + path + " 解码出零时长剪辑，按失败处理");
			}
			else if (loadState != AudioDataLoadState.Loaded)
			{
				DefaultInterpolatedStringHandler val = new DefaultInterpolatedStringHandler(19, 2);
				val.AppendLiteral("音效解码失败 ");
				val.AppendFormatted(path);
				val.AppendLiteral("（loadState=");
				val.AppendFormatted<AudioDataLoadState>(loadState);
				val.AppendLiteral("）");
				Warn(val.ToStringAndClear());
			}
			else
			{
				onDone(clip);
			}
		}
		catch (Exception ex)
		{
			Warn("收尾音效 " + path + " 失败: " + ex.Message);
		}
	}

	private static string SoundsDir()
	{
		try
		{
			string directoryName = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
			return Path.Combine(string.IsNullOrEmpty(directoryName) ? "." : directoryName, "sounds");
		}
		catch (Exception)
		{
			return "sounds";
		}
	}

	private static float SafeLength(AudioClip clip)
	{
		try
		{
			return clip.length;
		}
		catch (Exception)
		{
			return 0f;
		}
	}

	private static bool SafeExists(string path)
	{
		try
		{
			return File.Exists(path);
		}
		catch (Exception)
		{
			return false;
		}
	}

	private static void SafeDispose(UnityWebRequest req)
	{
		if (req == null)
		{
			return;
		}
		try
		{
			req.Dispose();
		}
		catch (Exception)
		{
		}
	}

	private static void Warn(string msg)
	{
		HaavkIl2CppPlugin.Instance.Log.LogWarning("[Haavk|Combat] " + msg);
	}
}
