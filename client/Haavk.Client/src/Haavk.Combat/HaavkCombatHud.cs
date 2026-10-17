using System;
using UnityEngine;
using BepInEx;

namespace Haavk.Combat;

internal class HaavkCombatHud : MonoBehaviour
{
	private static HaavkCombatHud _instance;

	private const int TickBudget = 5;

	private static int _tickErrors;

	private static bool _tickTripped;

	internal static void Install()
	{
		if (!(_instance != null))
		{
			_instance = ((BasePlugin)HaavkIl2CppPlugin.Instance).gameObject.AddComponent<HaavkCombatHud>();
		}
	}

	private void Update()
	{
		if (_tickTripped)
		{
			return;
		}
		try
		{
			CombatRuntime.Tick();
			_tickErrors = 0;
		}
		catch (Exception e)
		{
			CombatFeature.WarnOnce("宿主 Update 出错", e);
			if (++_tickErrors >= 5)
			{
				_tickTripped = true;
				HaavkIl2CppPlugin.Instance.Log.LogError("[Haavk|Combat] 宿主 Update 连抛 " + 5 + " 次，战斗反馈停用到重启游戏（熔断）");
			}
		}
	}

	private void OnGUI()
	{
		if (_tickTripped)
		{
			return;
		}
		try
		{
			CombatRuntime.Draw();
		}
		catch (Exception e)
		{
			CombatFeature.WarnOnce("宿主 OnGUI 出错", e);
		}
	}
}
