using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using BepInEx.Logging;
using Il2CppInterop.Runtime.InteropTypes;
using UnityEngine;
using UnityEngine.Networking;

namespace Haavk.Looting;

internal static class ChimeClips
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

	private static readonly AudioClip[] Clips = new AudioClip[6];

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

	internal static void Start()
	{
		if (!_started)
		{
			_started = true;
			HaavkCoroutineHost.Run(WatchLoop());
		}
	}

	internal static bool TryGet(int tier, out AudioClip clip)
	{
		clip = null;
		if (tier < 0 || tier >= Clips.Length)
		{
			return false;
		}
		AudioClip audioClip = Clips[tier];
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
		for (int i = 0; i < Clips.Length; i++)
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
		for (int tier = 0; tier < Clips.Length; tier++)
		{
			AudioClip audioClip = Clips[tier];
			if (audioClip != null && SafeLength(audioClip) > 0f)
			{
				total++;
				summary.Append(" 档").Append(tier).Append('=')
					.Append(SafeLength(audioClip).ToString("F1"))
					.Append('s');
				continue;
			}
			string text = null;
			AudioType type = AudioType.UNKNOWN;
			for (int i = 0; i < Formats.Length; i++)
			{
				string text2 = Path.Combine(dir, "search_level" + (tier + 1) + Formats[i].Ext);
				if (SafeExists(text2))
				{
					text = text2;
					type = Formats[i].Type;
					break;
				}
			}
			if (text == null)
			{
				summary.Append(" 档").Append(tier).Append("=缺");
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
				summary.Append(" 档").Append(tier).Append("=解码失败");
				continue;
			}
			Clips[tier] = loaded;
			total++;
			reloaded++;
			summary.Append(" 档").Append(tier).Append('=')
				.Append(SafeLength(loaded).ToString("F1"))
				.Append('s');
		}
		_loading = false;
		if (_loggedTotal < 0 || total != _loggedTotal)
		{
			_loggedTotal = total;
			ManualLogSource log = HaavkIl2CppPlugin.Instance.Log;
			object[] buffer = new object[5];
			buffer[0] = "[Haavk|Looting]";
			buffer[1] = (object)total;
			buffer[2] = (object)Clips.Length;
			buffer[3] = summary.ToString();
			buffer[4] = dir;
			log.LogInfo(string.Format("{0} 自定义音效：{1}/{2} 档可用{3}（目录 {4}）", buffer));
		}
		else if (reloaded > 0)
		{
			HaavkIl2CppPlugin.Instance.Log.LogDebug(string.Format("{0} 自定义音效被引擎卸载后已重载 {1} 档{2}", "[Haavk|Looting]", reloaded, summary.ToString()));
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
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
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
				Warn(val.ToStringAndClear() + " —— 若是 mp3，多半是 MPEG 桌面端不支持，转成 ogg 再试");
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
		HaavkIl2CppPlugin.Instance.Log.LogWarning("[Haavk|Looting] " + msg);
	}
}
