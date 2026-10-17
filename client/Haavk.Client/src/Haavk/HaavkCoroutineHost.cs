using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using BepInEx.Core.Logging.Interpolation;
using BepInEx.Logging;
using Il2CppInterop.Runtime.InteropTypes;
using UnityEngine;
using BepInEx;

namespace Haavk;

internal class HaavkCoroutineHost : MonoBehaviour
{
	private static HaavkCoroutineHost _instance;

	private static readonly Queue<Action> Pending = new Queue<Action>();

	private static readonly object Gate = new object();

	private static int _mainThreadId;

	internal static GameObject HostObject
	{
		get
		{
			if (!(_instance == null))
			{
				return _instance.gameObject;
			}
			return null;
		}
	}

	internal static bool OnMainThread
	{
		get
		{
			if (_mainThreadId != 0)
			{
				return Thread.CurrentThread.ManagedThreadId == _mainThreadId;
			}
			return false;
		}
	}

	internal static void Install(BasePlugin plugin)
	{
		if (!(_instance != null))
		{
			_mainThreadId = Thread.CurrentThread.ManagedThreadId;
			_instance = plugin.gameObject.AddComponent<HaavkCoroutineHost>();
		}
	}

	internal static void Post(Action work)
	{
		if (work == null)
		{
			return;
		}
		bool isEnabled;
		if (OnMainThread)
		{
			try
			{
				work();
				return;
			}
			catch (Exception t)
			{
				ManualLogSource log = HaavkIl2CppPlugin.Instance.Log;
				BepInExErrorLogInterpolatedStringHandler bepInExErrorLogInterpolatedStringHandler = new BepInExErrorLogInterpolatedStringHandler(10, 2, out isEnabled);
				if (isEnabled)
				{
					bepInExErrorLogInterpolatedStringHandler.AppendFormatted("[Haavk]");
					bepInExErrorLogInterpolatedStringHandler.AppendLiteral(" 主线程任务抛了: ");
					bepInExErrorLogInterpolatedStringHandler.AppendFormatted(t);
				}
				log.LogError(bepInExErrorLogInterpolatedStringHandler);
				return;
			}
		}
		if (_instance == null)
		{
			ManualLogSource log2 = HaavkIl2CppPlugin.Instance.Log;
			BepInExErrorLogInterpolatedStringHandler bepInExErrorLogInterpolatedStringHandler = new BepInExErrorLogInterpolatedStringHandler(20, 1, out isEnabled);
			if (isEnabled)
			{
				bepInExErrorLogInterpolatedStringHandler.AppendFormatted("[Haavk]");
				bepInExErrorLogInterpolatedStringHandler.AppendLiteral(" 协程宿主还没装上，这段主线程任务不会跑");
			}
			log2.LogError(bepInExErrorLogInterpolatedStringHandler);
			return;
		}
		lock (Gate)
		{
			Pending.Enqueue(work);
		}
	}

	private void Update()
	{
		while (true)
		{
			Action action;
			lock (Gate)
			{
				if (Pending.Count == 0)
				{
					break;
				}
				action = Pending.Dequeue();
			}
			try
			{
				action();
			}
			catch (Exception t)
			{
				ManualLogSource log = HaavkIl2CppPlugin.Instance.Log;
				BepInExErrorLogInterpolatedStringHandler bepInExErrorLogInterpolatedStringHandler = new BepInExErrorLogInterpolatedStringHandler(10, 2, out var isEnabled);
				if (isEnabled)
				{
					bepInExErrorLogInterpolatedStringHandler.AppendFormatted("[Haavk]");
					bepInExErrorLogInterpolatedStringHandler.AppendLiteral(" 主线程任务抛了: ");
					bepInExErrorLogInterpolatedStringHandler.AppendFormatted(t);
				}
				log.LogError(bepInExErrorLogInterpolatedStringHandler);
			}
		}
	}

	internal static Coroutine Run(IEnumerator routine)
	{
		if (routine == null)
		{
			return null;
		}
		bool isEnabled;
		if (_instance == null)
		{
			ManualLogSource log = HaavkIl2CppPlugin.Instance.Log;
			BepInExErrorLogInterpolatedStringHandler bepInExErrorLogInterpolatedStringHandler = new BepInExErrorLogInterpolatedStringHandler(33, 1, out isEnabled);
			if (isEnabled)
			{
				bepInExErrorLogInterpolatedStringHandler.AppendFormatted("[Haavk]");
				bepInExErrorLogInterpolatedStringHandler.AppendLiteral(" 协程宿主还没装上，这个协程不会跑（Install 没调或调晚了）");
			}
			log.LogError(bepInExErrorLogInterpolatedStringHandler);
			return null;
		}
		try
		{
			return ((MonoBehaviour)_instance).StartCoroutine(routine);
		}
		catch (Exception t)
		{
			ManualLogSource log2 = HaavkIl2CppPlugin.Instance.Log;
			BepInExErrorLogInterpolatedStringHandler bepInExErrorLogInterpolatedStringHandler = new BepInExErrorLogInterpolatedStringHandler(8, 2, out isEnabled);
			if (isEnabled)
			{
				bepInExErrorLogInterpolatedStringHandler.AppendFormatted("[Haavk]");
				bepInExErrorLogInterpolatedStringHandler.AppendLiteral(" 起协程失败: ");
				bepInExErrorLogInterpolatedStringHandler.AppendFormatted(t);
			}
			log2.LogError(bepInExErrorLogInterpolatedStringHandler);
			return null;
		}
	}

	internal static void Stop(Coroutine routine)
	{
		if (routine == null || _instance == null)
		{
			return;
		}
		try
		{
			_instance.StopCoroutine(routine);
		}
		catch (Exception t)
		{
			ManualLogSource log = HaavkIl2CppPlugin.Instance.Log;
			BepInExErrorLogInterpolatedStringHandler bepInExErrorLogInterpolatedStringHandler = new BepInExErrorLogInterpolatedStringHandler(8, 2, out var isEnabled);
			if (isEnabled)
			{
				bepInExErrorLogInterpolatedStringHandler.AppendFormatted("[Haavk]");
				bepInExErrorLogInterpolatedStringHandler.AppendLiteral(" 停协程失败: ");
				bepInExErrorLogInterpolatedStringHandler.AppendFormatted(t);
			}
			log.LogError(bepInExErrorLogInterpolatedStringHandler);
		}
	}
}
