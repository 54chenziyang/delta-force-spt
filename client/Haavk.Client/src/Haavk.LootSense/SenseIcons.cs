using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using SPT.Common.Http;
using UnityEngine;

namespace Haavk.LootSense;

internal static class SenseIcons
{
	[CompilerGenerated]
	private sealed class __c__DisplayClass5_0
	{
		public string path;

		public byte[] bytes;

		internal void _Fetch_b__0()
		{
			Build(path, bytes);
		}
	}

	[StructLayout(LayoutKind.Auto)]
	[CompilerGenerated]
	private struct _Fetch_d__5 : IAsyncStateMachine
	{
		public int __1__state;

		public AsyncVoidMethodBuilder __t__builder;

		public string path;

		private __c__DisplayClass5_0 __8__1;

		private TaskAwaiter<byte[]> __u__1;

		public void MoveNext()
		{
			//IL_0079: Unknown result type (might be due to invalid IL or missing references)
			//IL_007e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0085: Unknown result type (might be due to invalid IL or missing references)
			//IL_0046: Unknown result type (might be due to invalid IL or missing references)
			//IL_004b: Unknown result type (might be due to invalid IL or missing references)
			//IL_005f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0060: Unknown result type (might be due to invalid IL or missing references)
			int num = __1__state;
			try
			{
				if (num != 0)
				{
					__8__1 = new __c__DisplayClass5_0();
					__8__1.path = path;
					__8__1.bytes = null;
				}
				try
				{
					TaskAwaiter<byte[]> val;
					if (num != 0)
					{
						val = RequestHandler.GetDataAsync(__8__1.path).GetAwaiter();
						if (!val.IsCompleted)
						{
							num = (__1__state = 0);
							__u__1 = val;
							__t__builder.AwaitUnsafeOnCompleted<TaskAwaiter<byte[]>, _Fetch_d__5>(ref val, ref this);
							return;
						}
					}
					else
					{
						val = __u__1;
						__u__1 = default;
						num = (__1__state = -1);
					}
					byte[] result = val.GetResult();
					__8__1.bytes = result;
				}
				catch (Exception ex)
				{
					WarnOnce(__8__1.path, ((MemberInfo)ex.GetType()).Name + ": " + ex.Message);
					goto end_IL_0007;
				}
				var __hc = __8__1;
				HaavkCoroutineHost.Post(() =>
				{
					Build(__hc.path, __hc.bytes);
				});
				end_IL_0007:;
			}
			catch (Exception exception)
			{
				__1__state = -2;
				__8__1 = null;
				__t__builder.SetException(exception);
				return;
			}
			__1__state = -2;
			__8__1 = null;
			__t__builder.SetResult();
		}

		[DebuggerHidden]
		public void SetStateMachine(IAsyncStateMachine stateMachine)
		{
			__t__builder.SetStateMachine(stateMachine);
		}
	}

	internal const float WorldUnits = 1.6f;

	private static readonly Dictionary<string, Sprite> Loaded = new Dictionary<string, Sprite>();

	private static readonly HashSet<string> Requested = new HashSet<string>();

	private static bool _failWarned;

	internal static Sprite Get(string path)
	{
		if (string.IsNullOrEmpty(path))
		{
			return null;
		}
		if (Loaded.TryGetValue(path, out var value))
		{
			if (value != null)
			{
				return value;
			}
			Loaded.Remove(path);
			Requested.Remove(path);
		}
		if (Requested.Add(path))
		{
			Fetch(path);
		}
		return null;
	}

	[AsyncStateMachine(typeof(_Fetch_d__5))]
	private static void Fetch(string path)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Unknown result type (might be due to invalid IL or missing references)
		_Fetch_d__5 obj = default;
		obj.__t__builder = AsyncVoidMethodBuilder.Create();
		obj.path = path;
		obj.__1__state = -1;
		obj.__t__builder.Start<_Fetch_d__5>(ref obj);
	}

	private static void Build(string path, byte[] bytes)
	{
		if (bytes == null || bytes.Length == 0)
		{
			WarnOnce(path, "服务端返回空");
			return;
		}
		try
		{
			Texture2D texture2D = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false);
			texture2D.hideFlags = HideFlags.HideAndDontSave;
			if (!ImageConversion.LoadImage(texture2D, bytes))
			{
				UnityEngine.Object.Destroy(texture2D);
				WarnOnce(path, "不是能解码的图片（" + bytes.Length + " 字节）");
				return;
			}
			texture2D.wrapMode = TextureWrapMode.Clamp;
			texture2D.filterMode = FilterMode.Bilinear;
			float pixelsPerUnit = (float)Math.Max(texture2D.width, texture2D.height) / 1.6f;
			Sprite sprite = Sprite.Create(texture2D, new Rect(0f, 0f, texture2D.width, texture2D.height), new Vector2(0.5f, 0.5f), pixelsPerUnit, 0u, SpriteMeshType.FullRect);
			sprite.hideFlags = HideFlags.HideAndDontSave;
			Loaded[path] = sprite;
		}
		catch (Exception ex)
		{
			WarnOnce(path, ex.ToString());
		}
	}

	private static void WarnOnce(string path, string reason)
	{
		string data = "[Haavk|Sense] 图标下载失败 " + path + "：" + reason + "（标记照常显示，只是没有图标）";
		if (_failWarned)
		{
			HaavkIl2CppPlugin.Instance.Log.LogDebug(data);
			return;
		}
		_failWarned = true;
		HaavkIl2CppPlugin.Instance.Log.LogError(data);
	}
}
